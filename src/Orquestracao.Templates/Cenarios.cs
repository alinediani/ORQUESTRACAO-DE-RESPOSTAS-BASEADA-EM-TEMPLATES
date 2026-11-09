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
