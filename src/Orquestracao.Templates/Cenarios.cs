using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Orquestracao.Templates;

/// <summary>
/// O que a análise estrutural mede numa base de perguntas, sem chamar LLM: com o parser do protótipo, quantas
/// perguntas têm uma entidade da fonte (sem ela, a pergunta sempre vai ao LLM), quantas chaves distintas aparecem
/// e, daí, o teto de reaproveitamento (cada chave paga uma chamada ao LLM; as repetições poderiam sair do template).
/// A "chave abstrata" troca também os números por {n}: mede se a estrutura se repete mesmo ignorando os valores.
/// </summary>
public sealed record AnaliseEstrutural
{
    public required string Cenario { get; init; }
    public int Perguntas { get; init; }
    public int ComEntidade { get; init; }
    public int ChavesDistintas { get; init; }
    public int ChavesComEntidade { get; init; }
    /// <summary>Perguntas com entidade cuja chave já tinha aparecido: o máximo que o template poderia responder.</summary>
    public int TetoReaproveitamento => ComEntidade - ChavesComEntidade;
    public double TetoReaproveitamentoPct => Perguntas == 0 ? 0 : (double)TetoReaproveitamento / Perguntas;
    public int ChavesAbstratasDistintas { get; init; }
    /// <summary>Perguntas cuja chave abstrata (números → {n}) já tinha aparecido.</summary>
    public int RepeticoesAbstratas => Perguntas - ChavesAbstratasDistintas;
    public List<(string Chave, int Vezes)> MaisFrequentes { get; init; } = [];
    public List<(string Chave, int Vezes)> AbstratasMaisFrequentes { get; init; } = [];
    /// <summary>As intenções (as três primeiras palavras da pergunta normalizada) que mais se repetem.</summary>
    public List<(string Chave, int Vezes)> IntencoesMaisFrequentes { get; init; } = [];
    /// <summary>Perguntas cuja intenção aparece ao menos 10 vezes na base.</summary>
    public int PerguntasEmIntencoesFrequentes { get; init; }
    public int IntencoesDistintas { get; init; }
}

/// <summary>Os cenários do experimento (uma base pública por cenário, em docs/experimento/cenarios/{nome}/).</summary>
public static partial class Cenarios
{
    [GeneratedRegex(@"\d+([.,]\d+)*")]
    private static partial Regex Numero();

    [GeneratedRegex(@"####\s*(.+)$", RegexOptions.Multiline)]
    private static partial Regex RespostaFinal();

    /// <summary>A intenção da pergunta: as três primeiras palavras normalizadas ("quem canta", "quantas temporadas de").</summary>
    public static string Intencao(string pergunta) => string.Join(' ', Texto.Normalizar(pergunta).Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(3));

    public static string ChaveAbstrata(string chave) => Numero().Replace(chave, "{n}");

    public static AnaliseEstrutural Analisar(string cenario, IReadOnlyList<string> perguntas, ParserHeuristico parser, int top = 10)
    {
        var estruturas = perguntas.Select(parser.ExtrairSemLlm).ToList();
        var porChave = estruturas.GroupBy(e => e.Chave).ToList();
        var abstratas = estruturas.GroupBy(e => ChaveAbstrata(e.Chave)).ToList();
        static List<(string, int)> Top(IEnumerable<IGrouping<string, Estrutura>> g, int n) =>
            g.Where(x => x.Count() > 1).OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal).Take(n).Select(x => (x.Key, x.Count())).ToList();
        var intencoes = perguntas.GroupBy(Intencao).ToList();
        return new AnaliseEstrutural
        {
            IntencoesMaisFrequentes = intencoes.OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal).Take(top).Select(x => (x.Key, x.Count())).ToList(),
            PerguntasEmIntencoesFrequentes = intencoes.Where(x => x.Count() >= 10).Sum(x => x.Count()),
            IntencoesDistintas = intencoes.Count,
            Cenario = cenario, Perguntas = perguntas.Count, ComEntidade = estruturas.Count(e => e.TemSlot),
            ChavesDistintas = porChave.Count, ChavesComEntidade = porChave.Count(g => g.First().TemSlot),
            ChavesAbstratasDistintas = abstratas.Count, MaisFrequentes = Top(porChave, top), AbstratasMaisFrequentes = Top(abstratas, top),
        };
    }

    // ---- GSM8K -------------------------------------------------------------------------------------------

    /// <summary>
    /// Lê o GSM8K (JSONL com question/answer; a resposta final vem depois de "####"). A família é "gsm8k" e o
    /// gabarito é o número final, sem separador de milhar.
    /// </summary>
    public static List<PerguntaTeste> LerGsm8k(string caminho)
    {
        var lista = new List<PerguntaTeste>();
        foreach (var linha in File.ReadLines(caminho).Where(l => l.Trim().Length > 0))
        {
            using var doc = JsonDocument.Parse(linha);
            var q = doc.RootElement.GetProperty("question").GetString()!.Trim();
            var a = doc.RootElement.GetProperty("answer").GetString()!;
            var m = RespostaFinal().Match(a);
            var gabarito = m.Success ? m.Groups[1].Value.Trim().Replace(",", "").Replace(" ", "") : "";
            lista.Add(new PerguntaTeste(lista.Count + 1, q, "gsm8k", "", "resposta", gabarito));
        }
        return lista;
    }

    // ---- MKQA --------------------------------------------------------------------------------------------

    /// <summary>
    /// Lê o recorte em português do MKQA (query_pt e answers_pt). Só entram as perguntas com resposta curta conferível:
    /// entidade, número, número com unidade (vale o número) e data que é só o ano. A família é o tipo da resposta, e o
    /// gabarito junta o texto e os apelidos com " || " (qualquer um vale).
    /// </summary>
    public static List<PerguntaTeste> LerMkqa(string caminho, bool soConferiveis = true)
    {
        var lista = new List<PerguntaTeste>();
        foreach (var linha in File.ReadLines(caminho).Where(l => l.Trim().Length > 0))
        {
            using var doc = JsonDocument.Parse(linha);
            var q = doc.RootElement.GetProperty("query_pt").GetString()!.Trim();
            var respostas = doc.RootElement.GetProperty("answers_pt");
            var primeira = respostas.GetArrayLength() > 0 ? respostas[0] : default;
            var tipo = primeira.ValueKind == JsonValueKind.Object ? primeira.GetProperty("type").GetString()! : "unanswerable";
            var textos = new List<string>();
            foreach (var r in respostas.EnumerateArray())
            {
                if (r.TryGetProperty("text", out var t) && t.GetString() is { Length: > 0 } x) textos.Add(x);
                if (r.TryGetProperty("aliases", out var al)) textos.AddRange(al.EnumerateArray().Select(a => a.GetString()!).Where(a => a.Length > 0));
            }
            string? gabarito = tipo switch
            {
                "entity" => string.Join(" || ", textos.Distinct()),
                "number" or "number_with_unit" => Numero().Match(textos.FirstOrDefault() ?? "") is { Success: true } m ? SemZeroDecimal(m.Value) : null,
                "date" => textos.FirstOrDefault() is { Length: 4 } ano ? ano : null,
                _ => null,
            };
            if (soConferiveis && string.IsNullOrEmpty(gabarito)) continue;
            lista.Add(new PerguntaTeste(lista.Count + 1, q, tipo, "", "resposta", gabarito ?? ""));
        }
        return lista;
    }

    private static string SemZeroDecimal(string n) => n.EndsWith(".0", StringComparison.Ordinal) ? n[..^2] : n;

    // ---- Quiz (SimpleQuestions no Wikidata) --------------------------------------------------------------

    /// <summary>O prompt do normalizador (cenário 6b): reescreve a pergunta na forma canônica da relação.</summary>
    public const string SystemNormalizacao =
        "Traduza a pergunta para o português do Brasil e reescreva-a na forma mais simples e padronizada possível para o que ela pede " +
        "(por exemplo: \"Onde X nasceu?\", \"Qual é a nacionalidade de X?\", \"Quem dirigiu X?\"), mantendo o nome da entidade. Devolva só a pergunta.";

    public sealed record Quiz(List<PerguntaTeste> Perguntas, List<Entidade> Entidades, List<string> Originais);

    /// <summary>
    /// O quiz: perguntas de uma relação só (sujeito, relação, objeto) do SimpleQuestions mapeado para o Wikidata. A fonte sai do
    /// Wikidata: cada sujeito vira uma entidade (tipo "item") e cada relação um atributo, com o rótulo do objeto em português (ou
    /// em inglês). <paramref name="variante"/>: "fiel" (traducao-fiel.tsv), "simplificada" (a forma canônica de simplificacao.tsv)
    /// ou "original" (o inglês). A família é a relação; o gabarito, o rótulo do objeto e os apelidos.
    /// </summary>
    public static Quiz LerQuiz(string pasta, string variante)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(pasta, "amostra.json")));
        var labels = doc.RootElement.GetProperty("labels");
        string Nome(string id) => labels.TryGetProperty(id, out var l)
            ? (l.GetProperty("pt").GetString() ?? l.GetProperty("en").GetString() ?? id) : id;
        IEnumerable<string> Apelidos(string id) => labels.TryGetProperty(id, out var l)
            ? l.GetProperty("apelidos_pt").EnumerateArray().Select(a => a.GetString()!).Append(l.GetProperty("en").GetString() ?? "").Where(a => a.Length > 0)
            : [];
        var fiel = File.ReadLines(Path.Combine(pasta, "traducao-fiel.tsv")).Where(l => l.Contains('\t')).Select(l => l.Split('\t', 2)).ToDictionary(c => int.Parse(c[0], CultureInfo.InvariantCulture), c => c[1].Trim());
        var forma = File.ReadLines(Path.Combine(pasta, "simplificacao.tsv")).Where(l => !l.StartsWith('#') && l.Contains('\t')).Select(l => l.Split('\t', 2)).ToDictionary(c => c[0], c => c[1].Trim());
        var entidades = new Dictionary<string, Entidade>();
        var perguntas = new List<PerguntaTeste>();
        var originais = new List<string>();
        foreach (var r in doc.RootElement.GetProperty("amostra").EnumerateArray())
        {
            var (suj, rel, obj, en) = (r[0].GetString()!, r[1].GetString()!, r[2].GetString()!, r[3].GetString()!.Trim());
            var n = perguntas.Count + 1;
            var nome = Nome(suj);
            var resposta = Nome(obj);
            var e = entidades.GetValueOrDefault(suj) ?? new Entidade
            {
                Nome = nome, Tipo = "item", Apelidos = Apelidos(suj).Where(a => !string.Equals(a, nome, StringComparison.OrdinalIgnoreCase)).Distinct().ToList(),
            };
            // Um atributo por relação (a primeira resposta vale, como numa fonte de um valor só).
            e.Atributos.TryAdd(rel.ToLowerInvariant(), resposta);
            entidades[suj] = e;
            var pergunta = variante switch
            {
                "fiel" => fiel[n],
                // Sem rótulo no Wikidata, o normalizador manteria o nome escrito na pergunta: fica a tradução fiel.
                "simplificada" => forma.TryGetValue(rel, out var f) && !SemRotulo(nome) ? f.Replace("{X}", nome) : fiel[n],
                _ => en,
            };
            var gabarito = string.Join(" || ", Apelidos(obj).Prepend(resposta).Distinct());
            perguntas.Add(new PerguntaTeste(n, pergunta, rel, nome, rel.ToLowerInvariant(), gabarito));
            originais.Add(en);
        }
        return new Quiz(perguntas, entidades.Values.ToList(), originais);
    }

    private static bool SemRotulo(string nome) => nome.Length > 1 && nome[0] == 'Q' && nome[1..].All(char.IsDigit);

    /// <summary>
    /// O custo de simplificar as perguntas com um LLM antes do parser: uma chamada por pergunta, com o system do normalizador e a
    /// pergunta original na entrada e a pergunta simplificada na saída (tokens estimados por ≈ 4 caracteres).
    /// </summary>
    public static (long Entrada, long Saida) TokensSimplificacao(IEnumerable<(string Original, string Simplificada)> pares)
    {
        long e = 0, s = 0;
        foreach (var (o, x) in pares)
        {
            e += LlmDeFatos.Tokens(SystemNormalizacao) + LlmDeFatos.Tokens(o);
            s += LlmDeFatos.Tokens(x);
        }
        return (e, s);
    }

    /// <summary>A amostra do experimento do quiz: as <paramref name="porRelacao"/> primeiras perguntas de cada uma das <paramref name="relacoes"/> relações mais frequentes.</summary>
    public static List<PerguntaTeste> AmostraPorRelacao(IReadOnlyList<PerguntaTeste> todas, int relacoes, int porRelacao) =>
        todas.GroupBy(p => p.Familia).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(relacoes)
            .SelectMany(g => g.Take(porRelacao)).OrderBy(p => p.N).Select((p, i) => p with { N = i + 1 }).ToList();

    // ---- Estilos (cenário 7b) --------------------------------------------------------------------------

    /// <summary>
    /// O cenário 7b: 10 fatos da fonte padrão (5 capitais e 5 símbolos químicos) pedidos em cada um dos 5 estilos de
    /// <see cref="Estilos.DoCenario"/>, 50 perguntas embaralhadas pela semente. A família é "estilo/atributo"; o gabarito, o dado.
    /// </summary>
    public static List<PerguntaTeste> GerarEstilos(IFonteDeFatos fonte, int semente)
    {
        (string Entidade, string Atributo, string Frase)[] fatos =
        [
            ("França", "capital", "Qual é a capital {de}?"), ("Japão", "capital", "Qual é a capital {de}?"), ("Itália", "capital", "Qual é a capital {de}?"),
            ("Peru", "capital", "Qual é a capital {de}?"), ("Canadá", "capital", "Qual é a capital {de}?"),
            ("ouro", "simbolo", "Qual é o símbolo químico {de}?"), ("prata", "simbolo", "Qual é o símbolo químico {de}?"), ("ferro", "simbolo", "Qual é o símbolo químico {de}?"),
            ("sódio", "simbolo", "Qual é o símbolo químico {de}?"), ("cobre", "simbolo", "Qual é o símbolo químico {de}?"),
        ];
        var rnd = new Random(semente);
        var lista = from est in Estilos.DoCenario
                    from f in fatos
                    let e = fonte.PorNome(f.Entidade) ?? throw new InvalidOperationException($"sem {f.Entidade} na fonte")
                    select (Estilo: est, Fato: f, E: e);
        return lista.OrderBy(_ => rnd.Next()).Select((x, i) => new PerguntaTeste(i + 1,
            $"{x.Estilo.Instrucao}: {x.Fato.Frase.Replace("{de}", x.E.Forma("de_entidade"))}", $"{x.Estilo.Nome}/{x.Fato.Atributo}", x.E.Nome, x.Fato.Atributo,
            x.E.Atributos[x.Fato.Atributo])).ToList();
    }

    // ---- SQuAD-pt ----------------------------------------------------------------------------------------

    /// <summary>
    /// Lê o SQuAD-pt agrupado por parágrafo (title, context, qas[{id, question, answer}]). Com <paramref name="comTexto"/>,
    /// a pergunta leva o parágrafo ("Texto: …\n\nPergunta: …"), porque sem ele não dá para responder; sem, fica só a
    /// pergunta (para a análise estrutural). A família é o artigo da Wikipédia; o gabarito, o trecho do parágrafo.
    /// </summary>
    public static List<PerguntaTeste> LerSquad(string caminho, bool comTexto = true)
    {
        var lista = new List<PerguntaTeste>();
        foreach (var linha in File.ReadLines(caminho).Where(l => l.Trim().Length > 0))
        {
            using var doc = JsonDocument.Parse(linha);
            var titulo = doc.RootElement.GetProperty("title").GetString()!;
            var texto = doc.RootElement.GetProperty("context").GetString()!.Trim();
            foreach (var qa in doc.RootElement.GetProperty("qas").EnumerateArray())
            {
                var q = qa.GetProperty("question").GetString()!.Trim();
                var a = qa.GetProperty("answer").GetString()!.Trim();
                lista.Add(new PerguntaTeste(lista.Count + 1, comTexto ? $"Texto: {texto}\n\nPergunta: {q}" : q, titulo, "", "trecho", a));
            }
        }
        return lista;
    }

    // ---- Saúde (SaudeBR-QA) -----------------------------------------------------------------------------

    /// <summary>Lê um CSV (vírgula, aspas duplas com "" escapadas e quebras de linha dentro das aspas).</summary>
    public static IEnumerable<List<string>> LerCsv(string caminho)
    {
        var texto = File.ReadAllText(caminho);
        var campo = new StringBuilder();
        var linha = new List<string>();
        var aspas = false;
        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];
            if (aspas)
            {
                if (c == '"' && i + 1 < texto.Length && texto[i + 1] == '"') { campo.Append('"'); i++; }
                else if (c == '"') aspas = false;
                else campo.Append(c);
            }
            else if (c == '"') aspas = true;
            else if (c == ',') { linha.Add(campo.ToString()); campo.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n') i++;
                linha.Add(campo.ToString());
                campo.Clear();
                if (linha.Count > 1 || linha[0].Length > 0) yield return linha;
                linha = [];
            }
            else campo.Append(c);
        }
        if (campo.Length > 0 || linha.Count > 0)
        {
            linha.Add(campo.ToString());
            yield return linha;
        }
    }

    /// <summary>
    /// As perguntas distintas do SaudeBR-QA (o CSV tem uma linha por resposta de profissional). A família é a
    /// especialidade (sem o sufixo numérico) e o gabarito fica vazio: as respostas são orientações livres, sem
    /// resposta curta conferível.
    /// </summary>
    public static List<PerguntaTeste> LerSaude(string caminho)
    {
        var vistas = new HashSet<string>();
        var lista = new List<PerguntaTeste>();
        foreach (var l in LerCsv(caminho).Skip(1))
        {
            if (l.Count < 3) continue;
            var q = l[2].Trim();
            if (q.Length == 0 || !vistas.Add(q)) continue;
            var esp = l[1].Contains('-') ? l[1][..l[1].LastIndexOf('-')] : l[1];
            lista.Add(new PerguntaTeste(lista.Count + 1, q, esp.Replace('+', ' '), "", "especialidade", ""));
        }
        return lista;
    }

    /// <summary>O profissional de cada especialidade (o atributo que o template procura na resposta do LLM).</summary>
    public static readonly Dictionary<string, string> Profissional = new()
    {
        ["alergia e imunologia"] = "alergista", ["angiologia"] = "angiologista", ["cardiologia"] = "cardiologista",
        ["cirurgia plastica"] = "cirurgião plástico", ["cirurgia vascular"] = "cirurgião vascular", ["clinica medica"] = "clínico geral",
        ["coloproctologia"] = "proctologista", ["dermatologia"] = "dermatologista", ["endocrinologia e metabologia"] = "endocrinologista",
        ["gastroenterologia"] = "gastroenterologista", ["ginecologia e obstetricia"] = "ginecologista", ["infectologia"] = "infectologista",
        ["nefrologia"] = "nefrologista", ["neurologia"] = "neurologista", ["neurologia pediatrica"] = "neuropediatra", ["oftalmologia"] = "oftalmologista",
        ["ortopedia e traumatologia"] = "ortopedista", ["otorrinolaringologia"] = "otorrinolaringologista", ["pneumologia"] = "pneumologista",
        ["psiquiatria"] = "psiquiatra", ["psiquiatria da infancia e adolescencia"] = "psiquiatra infantil", ["reumatologia"] = "reumatologista",
        ["sexologia"] = "sexólogo", ["urologia"] = "urologista", ["andrologia"] = "andrologista", ["medicina do sono"] = "médico do sono",
        ["pediatria"] = "pediatra", ["geriatria"] = "geriatra", ["mastologia"] = "mastologista", ["hepatologia"] = "hepatologista",
    };

    /// <summary>
    /// A fonte do cenário de saúde, tirada do próprio corpus: cada condição da lista (nome;artigo;apelidos) citada em
    /// <paramref name="minimo"/>+ perguntas vira uma entidade do tipo "condicao", com o atributo "especialista" (o
    /// profissional da especialidade mais frequente entre essas perguntas).
    /// </summary>
    public static List<Entidade> FonteSaude(IReadOnlyList<PerguntaTeste> perguntas, string condicoes, int minimo = 20)
    {
        var normalizadas = perguntas.Select(p => (Texto: " " + Texto.Normalizar(p.Pergunta) + " ", p.Familia)).ToList();
        var lista = new List<Entidade>();
        foreach (var linha in File.ReadLines(condicoes).Where(l => l.Trim().Length > 0 && !l.StartsWith('#')))
        {
            var c = linha.Split(';');
            var apelidos = c.Length > 2 ? c[2].Split('|', StringSplitOptions.RemoveEmptyEntries).ToList() : [];
            var nomes = apelidos.Prepend(c[0]).Select(n => " " + Texto.Normalizar(n) + " ").ToList();
            var citam = normalizadas.Where(q => nomes.Any(q.Texto.Contains)).ToList();
            if (citam.Count < minimo) continue;
            var esp = citam.GroupBy(q => q.Familia).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).First().Key;
            lista.Add(new Entidade
            {
                Nome = c[0], Tipo = "condicao", Artigo = c.Length > 1 ? c[1] : "", Apelidos = apelidos,
                Atributos = new() { ["especialista"] = Profissional.GetValueOrDefault(esp, "clínico geral"), ["citacoes"] = citam.Count.ToString(CultureInfo.InvariantCulture) },
            });
        }
        return lista;
    }

    /// <summary>
    /// O melhor caso para o protótipo: todas as perguntas com entidade cuja chave se repete na base (até <paramref name="maximo"/>),
    /// embaralhadas pela semente. São as únicas que podem passar pelo caminho do template.
    /// </summary>
    public static List<PerguntaTeste> AmostraMelhorCaso(IReadOnlyList<PerguntaTeste> todas, ParserHeuristico parser, int maximo, int semente)
    {
        var rnd = new Random(semente);
        var escolhidas = todas.Select(p => (P: p, E: parser.ExtrairSemLlm(p.Pergunta))).Where(x => x.E.TemSlot)
            .GroupBy(x => x.E.Chave).Where(g => g.Count() >= 2)
            .SelectMany(g => g.Select(x => x.P with { Entidade = x.E.Entidade!.Nome, Atributo = x.E.Chave })).ToList();
        return escolhidas.OrderBy(_ => rnd.Next()).Take(maximo).Select((p, i) => p with { N = i + 1 }).ToList();
    }

    /// <summary>Uma amostra reprodutível de <paramref name="n"/> perguntas (renumeradas de 1 a n, na ordem sorteada).</summary>
    public static List<PerguntaTeste> Amostra(IReadOnlyList<PerguntaTeste> todas, int n, int semente)
    {
        var rnd = new Random(semente);
        return todas.OrderBy(_ => rnd.Next()).Take(n).Select((p, i) => p with { N = i + 1 }).ToList();
    }

    // ---- relatório ---------------------------------------------------------------------------------------

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static string Pct(double x) => (x * 100).ToString("0.0", PtBr) + "%";

    public static string RelatorioAnalise(AnaliseEstrutural a, string fonteDescricao)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Análise estrutural — {a.Cenario}");
        sb.AppendLine();
        sb.AppendLine($"Gerado por `dotnet run --project src/Orquestracao.Experimento -- --cenario {a.Cenario} --analisar`, sem LLM: o parser heurístico do protótipo " +
                      $"extrai a chave de cada pergunta com a fonte de fatos {fonteDescricao}.");
        sb.AppendLine();
        sb.AppendLine("| Medida | Valor |");
        sb.AppendLine("|---|---:|");
        sb.AppendLine($"| Perguntas | {a.Perguntas} |");
        sb.AppendLine($"| Com entidade da fonte (podem virar template) | {a.ComEntidade} ({Pct(a.Perguntas == 0 ? 0 : (double)a.ComEntidade / a.Perguntas)}) |");
        sb.AppendLine($"| Chaves distintas | {a.ChavesDistintas} |");
        sb.AppendLine($"| Chaves distintas com entidade | {a.ChavesComEntidade} |");
        sb.AppendLine($"| **Teto de reaproveitamento** (repetições de chave com entidade) | **{a.TetoReaproveitamento} ({Pct(a.TetoReaproveitamentoPct)})** |");
        sb.AppendLine($"| Chaves abstratas distintas (números → {{n}}) | {a.ChavesAbstratasDistintas} |");
        sb.AppendLine($"| Perguntas com a estrutura abstrata repetida | {a.RepeticoesAbstratas} ({Pct(a.Perguntas == 0 ? 0 : (double)a.RepeticoesAbstratas / a.Perguntas)}) |");
        sb.AppendLine($"| Intenções distintas (3 primeiras palavras) | {a.IntencoesDistintas} |");
        sb.AppendLine($"| Perguntas em intenções que aparecem 10+ vezes | {a.PerguntasEmIntencoesFrequentes} ({Pct(a.Perguntas == 0 ? 0 : (double)a.PerguntasEmIntencoesFrequentes / a.Perguntas)}) |");
        sb.AppendLine();
        void Lista(string titulo, List<(string Chave, int Vezes)> l)
        {
            sb.AppendLine($"## {titulo}");
            sb.AppendLine();
            if (l.Count == 0)
            {
                sb.AppendLine("Nenhuma chave se repete.");
                sb.AppendLine();
                return;
            }
            sb.AppendLine("| Chave | Perguntas |");
            sb.AppendLine("|---|---:|");
            foreach (var (c, v) in l) sb.AppendLine($"| `{(c.Length > 120 ? c[..119] + "…" : c)}` | {v} |");
            sb.AppendLine();
        }
        Lista("Chaves que mais se repetem", a.MaisFrequentes);
        Lista("Estruturas abstratas que mais se repetem", a.AbstratasMaisFrequentes);
        Lista("Intenções que mais se repetem (3 primeiras palavras)", a.IntencoesMaisFrequentes);
        return sb.ToString();
    }
}
