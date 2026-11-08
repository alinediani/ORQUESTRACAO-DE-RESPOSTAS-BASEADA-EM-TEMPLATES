using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Orquestracao.Core.Diagramas;

namespace Orquestracao.Core.Conversas;

/// <summary>As personas de um diagrama, uma por arquivo (<c>personas/&lt;id&gt;.md</c>). Removidas vão para <c>.removidos/</c>.</summary>
public sealed class PersonaStore(string dir)
{
    public string Pasta { get; } = dir;

    private string Arquivo(string id)
    {
        if (!Personas.IdRe().IsMatch(id)) throw new OpError(400, $"Id de retângulo inválido: \"{id}\".");
        return Path.Combine(Pasta, $"{id}.md");
    }

    private string ArquivoTemplate(string id)
    {
        Arquivo(id);
        return Path.Combine(Pasta, $"{id}.saida.md");
    }

    public bool Existe(string id) => File.Exists(Arquivo(id));

    /// <summary>O .md da persona; sem arquivo, o modelo padrão.</summary>
    public string LerMd(string id, string nome) =>
        File.Exists(Arquivo(id)) ? File.ReadAllText(Arquivo(id)) : Personas.Escrever(Personas.Padrao(id, nome));

    public Persona Ler(string id, string nome) => Personas.Ler(id, LerMd(id, nome), nome);

    /// <summary>Grava o .md exatamente como veio, depois de validar.</summary>
    public Persona GravarMd(string id, string md)
    {
        var p = Personas.Ler(id, md, id);
        Directory.CreateDirectory(Pasta);
        File.WriteAllText(Arquivo(id), md);
        return p;
    }

    public void Gravar(Persona p) => GravarMd(p.Id, Personas.Escrever(p));

    public bool TemTemplate(string id) => File.Exists(ArquivoTemplate(id));

    /// <summary>O template de saída (<c>personas/&lt;id&gt;.saida.md</c>); sem arquivo, o padrão.</summary>
    public string LerTemplate(string id) => TemTemplate(id) ? File.ReadAllText(ArquivoTemplate(id)) : Personas.TemplateSaidaPadrao;

    /// <summary>Grava só o template: a persona (<c>&lt;id&gt;.md</c>) não muda.</summary>
    public void GravarTemplate(string id, string md)
    {
        if (string.IsNullOrWhiteSpace(md)) throw new OpError(400, "O template de saída não pode ficar vazio.");
        Directory.CreateDirectory(Pasta);
        File.WriteAllText(ArquivoTemplate(id), md);
    }

    public void Remover(string id)
    {
        var lixo = Path.Combine(Pasta, ".removidos");
        var quando = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var (src, nome) in new[] { (Arquivo(id), $"{id}-{quando}.md"), (ArquivoTemplate(id), $"{id}-{quando}.saida.md") })
        {
            if (!File.Exists(src)) continue;
            Directory.CreateDirectory(lixo);
            File.Move(src, Path.Combine(lixo, nome));
        }
    }

    /// <summary>Ids com arquivo (para exportar e duplicar).</summary>
    public List<string> Ids() => Listar(".md").Where(id => !id.EndsWith(".saida")).ToList();

    public List<string> IdsComTemplate() => Listar(".saida.md");

    private List<string> Listar(string fim) =>
        !Directory.Exists(Pasta)
            ? []
            : Directory.EnumerateFiles(Pasta, "*" + fim).Select(Path.GetFileName).OfType<string>()
                .Select(f => f[..^fim.Length]).Where(id => Personas.IdRe().IsMatch(id) || id.EndsWith(".saida")).Order(StringComparer.Ordinal).ToList();
}

/// <summary>Um papel da paleta (<c>templates/&lt;nome&gt;.md</c>): o mesmo papel pode nascer em qualquer motor.</summary>
public sealed record TemplatePapel
{
    public string? Especial { get; init; }
    public JsonObject Extras { get; init; } = [];
    public string? TemplateSaida { get; init; }
    public required string Nome { get; init; }
    public required string Titulo { get; init; }
    public string Descricao { get; init; } = "";
    public string Papel { get; init; } = "debatedora";
    public string Cor { get; init; } = "#2563eb";
    public string Esforco { get; init; } = "medium";
    /// <summary>O modelo de cada motor: é aqui que se configura o padrão.</summary>
    public Dictionary<string, string> Modelos { get; init; } = [];
    public string Prompt { get; init; } = "";
}

public static class TemplatesDePapel
{
    private static readonly string[] CamposEspeciais =
        ["memoria", "escuta", "quando", "limite", "resumoPara", "curadoria", "humana", "prazo", "aoVencer", "saida", "arquivo", "depois", "fallback"];

    public static List<TemplatePapel> Carregar(string dir)
    {
        if (!Directory.Exists(dir)) return [];
        return Directory.EnumerateFiles(dir, "*.md").Order(StringComparer.Ordinal).Select(arquivo =>
        {
            var f = Path.GetFileName(arquivo);
            var md = File.ReadAllText(arquivo);
            var (yaml, prompt) = Personas.Partes(md);
            var y = Personas.LerYaml(yaml);
            // Os campos especiais passam pela mesma validação de uma persona.
            var p = Personas.Ler("template", md, f);
            var pj = System.Text.Json.JsonSerializer.SerializeToNode(p, Json.Opcoes)!.AsObject();
            var extras = new JsonObject();
            foreach (var k in CamposEspeciais.Where(k => y[k] is not null)) extras[k] = pj[k]?.DeepClone();
            var especial = p.Memoria ? "memoria" : p.Humana ? "humana" : p.Saida ? "saida" : null;
            var arqSaida = Path.Combine(dir, "saidas", f);
            var modelos = new Dictionary<string, string>(Personas.ModeloPadrao);
            if (y["modelos"] is JsonObject mo)
                foreach (var (k, v) in mo)
                    if (v is JsonValue jv && jv.TryGetValue<string>(out var s)) modelos[k] = s;
            var nome = f[..^3];
            return new TemplatePapel
            {
                Especial = especial,
                Extras = extras,
                TemplateSaida = especial == "saida" && File.Exists(arqSaida) ? File.ReadAllText(arqSaida) : null,
                Nome = nome,
                Titulo = Regex.Match(md, @"^\s*#\s*(.+)$", RegexOptions.Multiline) is { Success: true } t ? t.Groups[1].Value.Trim() : nome,
                Descricao = y["descricao"] is JsonValue d && d.TryGetValue<string>(out var ds) ? ds : "",
                Papel = p.Papel,
                Cor = y["cor"] is not null ? p.Cor : Personas.CorPadrao(f),
                Esforco = p.Esforco,
                Modelos = Personas.Motores.ToDictionary(m => m, m => modelos[m]),
                Prompt = prompt,
            };
        }).ToList();
    }

    /// <summary>A persona que nasce quando o papel é arrastado para o canvas num motor.</summary>
    public static Persona PersonaDo(TemplatePapel t, string id, string motor, string? nome = null)
    {
        var baseP = new Persona
        {
            Id = id, Nome = nome ?? t.Titulo, Prompt = t.Prompt, Motor = motor, Modelo = t.Modelos[motor],
            Esforco = t.Esforco, Papel = t.Papel, Cor = t.Cor, Fallback = motor == "template" ? "api" : null,
        };
        if (t.Extras.Count == 0) return baseP;
        // Os campos especiais do template entram por cima, pelo mesmo JSON da persona.
        var j = System.Text.Json.JsonSerializer.SerializeToNode(baseP, Json.Opcoes)!.AsObject();
        foreach (var (k, v) in t.Extras) j[k] = v?.DeepClone();
        return System.Text.Json.JsonSerializer.Deserialize<Persona>(j, Json.Opcoes)!;
    }
}
