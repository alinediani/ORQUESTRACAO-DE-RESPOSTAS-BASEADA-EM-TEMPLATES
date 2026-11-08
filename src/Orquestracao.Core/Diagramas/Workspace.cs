using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Orquestracao.Core.Diagramas;

public sealed record DiagramItem(string Slug, string Name, int Nodes, int Edges, string CreatedAt, string UpdatedAt);

/// <summary>
/// Vários diagramas, um por pasta (equivale ao <c>createDiagrams</c> de legado/server/diagrams.ts):
/// <code>
/// diagrams/
///   fluxo-de-vendas/  meta.json  diagram.json
///   .removidos/       diagramas apagados
/// </code>
/// </summary>
public sealed partial class Workspace : IDisposable
{
    public const string Meta = "meta.json";
    public const string Lixeira = ".removidos";
    public const string PrimeiroNome = "Meu diagrama";

    /// <summary>Limite do arquivo importado (.zip ou .mmd).</summary>
    public const int MaxImport = 10 * 1024 * 1024;

    private static readonly JsonSerializerOptions Indentado = new(Json.Opcoes) { WriteIndented = true };
    private static readonly CompareInfo PtBr = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;

    [GeneratedRegex("^[a-z0-9][a-z0-9-]*$")]
    private static partial Regex SlugRe();

    private readonly string _dir;
    private readonly ConcurrentDictionary<string, DiagramaArmazenado> _abertos = new();
    private readonly Lock _trava = new();
    private Timer? _avisoAgendado;

    /// <summary>Quem ouve a lista de diagramas (o menu ☰, pelo SSE de /api/events).</summary>
    public Assinantes Ouvintes { get; } = new();

    public string Pasta => _dir;

    /// <summary>Um retângulo foi removido (slug, id): a persona dele vai para a lixeira.</summary>
    public Action<string, string>? AoRemoverNo { get; set; }

    /// <summary>O diagrama foi fechado (renomeado ou apagado): as conversas dele param.</summary>
    public Action<string>? AoFechar { get; set; }

    /// <param name="dir">Pasta dos diagramas.</param>
    /// <param name="arquivoAntigo">O diagram.json solto de antes dos vários diagramas: vira o primeiro, se não há nenhum.</param>
    public Workspace(string dir, string? arquivoAntigo = null)
    {
        _dir = Path.GetFullPath(dir);
        Directory.CreateDirectory(_dir);
        if (Slugs().Count > 0) return;
        var antigoMmd = arquivoAntigo is null ? null : Path.ChangeExtension(arquivoAntigo, ".mmd");
        if (arquivoAntigo is not null && (File.Exists(arquivoAntigo) || File.Exists(antigoMmd)))
        {
            var slug = SlugUnico("Diagrama");
            Directory.CreateDirectory(PastaDe(slug));
            EscreverMeta(slug, new MetaInfo("Diagrama", Agora()));
            if (File.Exists(arquivoAntigo)) File.Copy(arquivoAntigo, Path.Combine(PastaDe(slug), "diagram.json"));
            else File.Copy(antigoMmd!, Path.Combine(PastaDe(slug), "diagram.mmd"));
        }
        else
        {
            Criar(PrimeiroNome);
        }
    }

    private sealed record MetaInfo(string Name, string CreatedAt);

    // ---- nomes e pastas ------------------------------------------------------

    /// <summary>Nome da pasta: minúsculas, sem acento, palavras separadas por hífen.</summary>
    public static string Slugify(string nome)
    {
        var semAcento = new StringBuilder();
        foreach (var c in nome.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) semAcento.Append(c);
        var s = Regex.Replace(semAcento.ToString().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (s.Length > 48) s = s[..48];
        s = s.TrimEnd('-');
        return s.Length > 0 ? s : "diagrama";
    }

    private static string Agora() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public string PastaDe(string slug) => Path.Combine(_dir, slug);

    public bool Existe(string slug) =>
        SlugRe().IsMatch(slug) && new[] { Meta, "diagram.json", "diagram.mmd" }.Any(f => File.Exists(Path.Combine(PastaDe(slug), f)));

    private List<string> Slugs() =>
        Directory.EnumerateDirectories(_dir).Select(Path.GetFileName).OfType<string>().Where(Existe).ToList();

    private MetaInfo LerMeta(string slug)
    {
        try
        {
            var meta = Json.Ler<MetaInfo>(File.ReadAllText(Path.Combine(PastaDe(slug), Meta)));
            if (meta is { Name.Length: > 0 }) return meta;
        }
        catch (Exception e) when (e is IOException or JsonException) { }
        var nome = slug.Replace('-', ' ');
        return new MetaInfo(char.ToUpperInvariant(nome[0]) + nome[1..], Iso(Directory.GetCreationTimeUtc(PastaDe(slug))));
    }

    private void EscreverMeta(string slug, MetaInfo meta) =>
        File.WriteAllText(Path.Combine(PastaDe(slug), Meta), JsonSerializer.Serialize(meta, Indentado) + "\n");

    /// <summary>Pasta livre para <paramref name="nome"/> (ignorando <paramref name="exceto"/>, ao renomear).</summary>
    private string SlugUnico(string nome, string exceto = "")
    {
        var baseSlug = Slugify(nome);
        for (var i = 1; ; i++)
        {
            var slug = i == 1 ? baseSlug : $"{baseSlug}-{i}";
            if (slug == exceto || !Directory.Exists(PastaDe(slug))) return slug;
        }
    }

    private static OpError NaoEncontrado(string slug) => new(404, $"Diagrama \"{slug}\" não encontrado.");

    // ---- cada diagrama -------------------------------------------------------

    /// <summary>O diagrama aberto (carregado na primeira vez).</summary>
    public DiagramaArmazenado Diagrama(string slug)
    {
        if (_abertos.TryGetValue(slug, out var aberto)) return aberto;
        lock (_trava)
        {
            if (_abertos.TryGetValue(slug, out aberto)) return aberto;
            if (!Existe(slug)) throw NaoEncontrado(slug);
            aberto = new DiagramaArmazenado(Path.Combine(PastaDe(slug), "diagram.json"))
            {
                AoMudar = AvisarEmBreve,
                AoRemoverNo = id => AoRemoverNo?.Invoke(slug, id),
            };
            _abertos[slug] = aberto;
            return aberto;
        }
    }

    /// <summary>O primeiro diagrama da lista (para as rotas antigas, sem prefixo).</summary>
    public DiagramaArmazenado Primeiro() => Diagrama(Listar()[0].Slug);

    private void Fechar(string slug)
    {
        AoFechar?.Invoke(slug);
        if (_abertos.TryRemove(slug, out var aberto)) aberto.Fechar();
    }

    // ---- lista e avisos ------------------------------------------------------

    public List<DiagramItem> Listar() =>
        Slugs()
            .Select(slug =>
            {
                var meta = LerMeta(slug);
                var arquivo = Path.Combine(PastaDe(slug), "diagram.json");
                int nodes = 0, edges = 0;
                if (_abertos.TryGetValue(slug, out var aberto)) (nodes, edges, _) = aberto.Info();
                else
                {
                    try
                    {
                        var d = JsonNode.Parse(File.ReadAllText(arquivo))?["diagram"];
                        nodes = d?["nodes"]?.AsArray().Count ?? 0;
                        edges = d?["edges"]?.AsArray().Count ?? 0;
                    }
                    catch (Exception e) when (e is IOException or JsonException or InvalidOperationException) { }
                }
                var updatedAt = File.Exists(arquivo) ? Iso(File.GetLastWriteTimeUtc(arquivo)) : meta.CreatedAt;
                return new DiagramItem(slug, meta.Name, nodes, edges, meta.CreatedAt, updatedAt);
            })
            .OrderBy(d => d.Name, Comparer<string>.Create((a, b) => PtBr.Compare(a, b, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace)))
            .ToList();

    private string MensagemDaLista() =>
        new JsonObject { ["type"] = "diagrams", ["diagrams"] = JsonSerializer.SerializeToNode(Listar(), Json.Opcoes) }.ToJsonString(Json.Opcoes);

    /// <summary>Assina a lista; a primeira mensagem é a lista atual.</summary>
    public Assinatura Assinar() => Ouvintes.Assinar(MensagemDaLista());

    private void Avisar() => Ouvintes.Publicar(MensagemDaLista());

    /// <summary>Mudanças seguidas (ex.: arrastar um retângulo) viram um aviso só.</summary>
    private void AvisarEmBreve()
    {
        lock (_trava)
            _avisoAgendado ??= new Timer(_ =>
            {
                lock (_trava)
                {
                    _avisoAgendado?.Dispose();
                    _avisoAgendado = null;
                }
                Avisar();
            }, null, 300, Timeout.Infinite);
    }

    // ---- ações ---------------------------------------------------------------

    public DiagramItem Criar(string? nome, Diagram? diagrama = null)
    {
        nome = nome?.Trim() ?? "";
        if (nome.Length == 0) throw new OpError(400, "\"name\" não pode ser vazio.");
        string slug;
        lock (_trava)
        {
            slug = SlugUnico(nome);
            Directory.CreateDirectory(PastaDe(slug));
            EscreverMeta(slug, new MetaInfo(nome, Agora()));
            File.WriteAllText(Path.Combine(PastaDe(slug), "diagram.json"), DiagramaArmazenado.Serializar(0, diagrama ?? new Diagram()));
        }
        Avisar();
        return Listar().First(d => d.Slug == slug);
    }

    public DiagramItem Renomear(string slug, string? nome)
    {
        nome = nome?.Trim() ?? "";
        if (!Existe(slug)) throw NaoEncontrado(slug);
        if (nome.Length == 0) throw new OpError(400, "\"name\" não pode ser vazio.");
        string novo;
        lock (_trava)
        {
            var meta = LerMeta(slug);
            novo = SlugUnico(nome, slug);
            if (novo != slug)
            {
                // As abas abertas no slug antigo são desconectadas e voltam pelo menu.
                Fechar(slug);
                Directory.Move(PastaDe(slug), PastaDe(novo));
            }
            EscreverMeta(novo, meta with { Name = nome });
        }
        Avisar();
        return Listar().First(d => d.Slug == novo);
    }

    /// <summary>Move o diagrama para <c>.removidos/</c>. Nunca fica sem diagrama nenhum.</summary>
    public void Apagar(string slug)
    {
        if (!Existe(slug)) throw NaoEncontrado(slug);
        lock (_trava)
        {
            Fechar(slug);
            var lixeira = Path.Combine(_dir, Lixeira);
            Directory.CreateDirectory(lixeira);
            Directory.Move(PastaDe(slug), Path.Combine(lixeira, $"{slug}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"));
        }
        if (Slugs().Count == 0) Criar(PrimeiroNome);
        Avisar();
    }

    /// <summary>O diagrama atual (do aberto, se houver; senão do arquivo).</summary>
    public Diagram Atual(string slug)
    {
        if (!Existe(slug)) throw NaoEncontrado(slug);
        if (_abertos.TryGetValue(slug, out var aberto)) return aberto.Diagrama;
        var arquivo = Path.Combine(PastaDe(slug), "diagram.json");
        if (!File.Exists(arquivo)) return Diagrama(slug).Diagrama;
        var raiz = Entrada.Ler(File.ReadAllText(arquivo));
        return raiz.TryGetProperty("diagram", out var d) && d.ValueKind == JsonValueKind.Object ? Entrada.Diagrama(d) : new Diagram();
    }

    public DiagramItem Duplicar(string slug)
    {
        var item = Criar($"{LerMeta(slug).Name} (cópia)", Atual(slug));
        // As personas vão junto (a memória e as conversas, não).
        foreach (var (nome, texto) in ArquivosDePersona(slug)) GravarPersona(item.Slug, nome, texto);
        return item;
    }

    /// <summary>Os .md das personas de um diagrama (personas/&lt;id&gt;.md e &lt;id&gt;.saida.md).</summary>
    private IEnumerable<(string Nome, string Texto)> ArquivosDePersona(string slug)
    {
        var pasta = Path.Combine(PastaDe(slug), "personas");
        if (!Directory.Exists(pasta)) yield break;
        foreach (var f in Directory.EnumerateFiles(pasta, "*.md").Order(StringComparer.Ordinal))
            yield return (Path.GetFileName(f), File.ReadAllText(f));
    }

    [GeneratedRegex(@"^(?:[^/]+/)?personas/([A-Za-z][A-Za-z0-9_-]*)(\.saida)?\.md$")]
    private static partial Regex PersonaZipRe();

    /// <summary>Grava o .md de uma persona vindo de fora (só o id validado vira nome de arquivo; a persona é validada).</summary>
    private void GravarPersona(string slug, string nome, string texto)
    {
        var m = PersonaZipRe().Match("personas/" + nome);
        if (!m.Success) return;
        var id = m.Groups[1].Value;
        try
        {
            if (!m.Groups[2].Success) Conversas.Personas.Ler(id, texto, id);
            else if (string.IsNullOrWhiteSpace(texto)) return;
        }
        catch (OpError)
        {
            return;
        }
        var pasta = Path.Combine(PastaDe(slug), "personas");
        Directory.CreateDirectory(pasta);
        File.WriteAllText(Path.Combine(pasta, m.Groups[2].Success ? $"{id}.saida.md" : $"{id}.md"), texto);
    }

    public string NomeDoArquivo(string slug) => Slugify(LerMeta(slug).Name);

    /// <summary>O .zip exportado: meta.json + diagram.json + diagram.mmd.</summary>
    public byte[] ExportarZip(string slug)
    {
        var d = Atual(slug);
        var meta = LerMeta(slug);
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Escrever(zip, Meta, JsonSerializer.Serialize(meta, Indentado) + "\n");
            Escrever(zip, "diagram.json", DiagramaArmazenado.Serializar(0, d));
            Escrever(zip, "diagram.mmd", Mermaid.Gerar(d) + "\n");
            foreach (var (nome, texto) in ArquivosDePersona(slug)) Escrever(zip, $"personas/{nome}", texto);
        }
        return ms.ToArray();
    }

    private static void Escrever(ZipArchive zip, string nome, string texto)
    {
        using var s = zip.CreateEntry(nome).Open();
        s.Write(Encoding.UTF8.GetBytes(texto));
    }

    private static readonly string[] Lidos = [Meta, "diagram.json", "diagram.mmd"];

    /// <summary>Importa um .zip exportado (só meta.json, diagram.json e diagram.mmd são lidos) ou um .mmd.</summary>
    public DiagramItem Importar(string? nome, byte[] dados)
    {
        nome = nome?.Trim() ?? "";
        if (dados.Length > MaxImport) throw new OpError(400, $"O arquivo passa do limite de {MaxImport / 1024 / 1024} MB.");
        var ehZip = dados.Length > 1 && dados[0] == 0x50 && dados[1] == 0x4b;
        if (!ehZip)
        {
            var (diagram, errors) = Mermaid.Ler(Encoding.UTF8.GetString(dados));
            if (errors.Count > 0) throw new OpError(400, $"Código Mermaid inválido: {string.Join(" · ", errors.Take(3))}");
            return Criar(nome.Length > 0 ? nome : "Importado", diagram);
        }

        // Só os arquivos que interessam são lidos: nada do zip é gravado com o nome que traz.
        var arquivos = new Dictionary<string, string>();
        var personas = new List<(string Nome, string Texto)>();
        try
        {
            using var zip = new ZipArchive(new MemoryStream(dados), ZipArchiveMode.Read);
            foreach (var entrada in zip.Entries.OrderBy(e => e.FullName.Length))
            {
                var baseNome = entrada.FullName.Split('/')[^1];
                if (PersonaZipRe().IsMatch(entrada.FullName) && entrada.Length <= MaxImport)
                {
                    using var lp = new StreamReader(entrada.Open(), Encoding.UTF8);
                    personas.Add((baseNome, lp.ReadToEnd()));
                    continue;
                }
                if (!Lidos.Contains(baseNome) || entrada.Length > MaxImport || arquivos.ContainsKey(baseNome)) continue;
                using var leitor = new StreamReader(entrada.Open(), Encoding.UTF8);
                arquivos[baseNome] = leitor.ReadToEnd();
            }
        }
        catch (InvalidDataException e)
        {
            throw new OpError(400, $"Arquivo não é um .zip válido: {e.Message}");
        }

        Diagram diagrama;
        if (arquivos.TryGetValue("diagram.json", out var json))
        {
            try
            {
                var raiz = Entrada.Ler(json);
                diagrama = Entrada.Diagrama(raiz.TryGetProperty("diagram", out var d) ? d : raiz);
            }
            catch (OpError e)
            {
                throw new OpError(400, $"diagram.json inválido: {e.Message}");
            }
        }
        else if (arquivos.TryGetValue("diagram.mmd", out var mmd))
        {
            var lido = Mermaid.Ler(mmd);
            if (lido.Errors.Count > 0) throw new OpError(400, $"diagram.mmd inválido: {string.Join(" · ", lido.Errors.Take(3))}");
            diagrama = lido.Diagram;
        }
        else
        {
            throw new OpError(400, "O .zip precisa de um diagram.json ou diagram.mmd (exportado pelo menu ☰).");
        }

        string? nomeDoMeta = null;
        try
        {
            if (arquivos.TryGetValue(Meta, out var meta)) nomeDoMeta = Json.Ler<MetaInfo>(meta)?.Name;
        }
        catch (JsonException) { }
        var item = Criar(nome.Length > 0 ? nome : nomeDoMeta is { Length: > 0 } ? nomeDoMeta : "Importado", diagrama);
        foreach (var (n, t) in personas) GravarPersona(item.Slug, n, t);
        return item;
    }

    public void Dispose()
    {
        foreach (var slug in _abertos.Keys) Fechar(slug);
        Ouvintes.Fechar();
        _avisoAgendado?.Dispose();
    }
}
