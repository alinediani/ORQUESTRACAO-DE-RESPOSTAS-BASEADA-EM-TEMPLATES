using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Orquestracao.Templates;

/// <summary>
/// O cenário 7a: quanto da demanda real (o primeiro pedido de cada conversa do WildChat) pede um estilo ("no estilo de",
/// "como um pirata", "in the style of"), quão concentrados são esses estilos, se há ondas no tempo, e quantos pedidos se
/// repetem idênticos (o caso do cache puro).
/// </summary>
public static partial class AnaliseEstilos
{
    public sealed record Pedido(string Texto, DateTime Quando, string Idioma);

    public sealed record Resultado
    {
        public required string Base { get; init; }
        public int Pedidos { get; init; }
        public int PedidosDeEstilo { get; init; }
        public List<(string Estilo, int Vezes)> Estilos { get; init; } = [];
        /// <summary>Fração dos pedidos de estilo que caem nos 10 estilos mais pedidos.</summary>
        public double ConcentracaoTop10 { get; init; }
        public int EstilosDistintos { get; init; }
        /// <summary>Pedidos cujo início (os primeiros 200 caracteres normalizados) repete o de outro pedido.</summary>
        public int RepetidosIdenticos { get; init; }
        public List<(string Inicio, int Vezes)> MaisRepetidos { get; init; } = [];
        /// <summary>Por mês: pedidos, pedidos de estilo e o estilo mais pedido no mês (com quantas vezes).</summary>
        public List<(string Mes, int Pedidos, int DeEstilo, string Top, int TopVezes)> PorMes { get; init; } = [];
    }

    // O alvo do estilo: o que vem depois do marcador, até a pontuação (no máximo 4 palavras).
    [GeneratedRegex(@"\b(?:no estilo d[eoa]s?|ao estilo d[eoa]s?|estilo d[eoa]s?|à maneira d[eoa]s?|como se fosse|como um|como uma|in the style of|in the voice of|as if you were|like a|pretend (?:to be|you are)|act as an?|act as)\s+([^\n.,;:!?""()]{2,60})", RegexOptions.IgnoreCase)]
    private static partial Regex Marcador();

    public static List<Pedido> Ler(string jsonl)
    {
        var lista = new List<Pedido>();
        foreach (var l in File.ReadLines(jsonl).Where(x => x.Trim().Length > 0))
        {
            using var d = JsonDocument.Parse(l);
            var t = d.RootElement.GetProperty("pedido").GetString() ?? "";
            var q = DateTime.Parse(d.RootElement.GetProperty("timestamp").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            lista.Add(new Pedido(t, q, d.RootElement.GetProperty("language").GetString() ?? ""));
        }
        return lista;
    }

    /// <summary>O estilo pedido (as até 4 primeiras palavras normalizadas do alvo), ou null.</summary>
    public static string? EstiloDe(string pedido)
    {
        var m = Marcador().Match(pedido);
        if (!m.Success) return null;
        var alvo = string.Join(' ', Texto.Normalizar(m.Groups[1].Value).Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(4));
        return alvo.Length < 2 ? null : alvo;
    }

    public static Resultado Analisar(string nome, IReadOnlyList<Pedido> pedidos)
    {
        var estilos = pedidos.Select(p => (p, e: EstiloDe(p.Texto))).Where(x => x.e is not null).ToList();
        var porEstilo = estilos.GroupBy(x => x.e!).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
        var inicios = pedidos.GroupBy(p => Texto.Normalizar(p.Texto.Length > 200 ? p.Texto[..200] : p.Texto)).Where(g => g.Key.Length > 0).ToList();
        var porMes = pedidos.GroupBy(p => p.Quando.ToString("yyyy-MM", CultureInfo.InvariantCulture)).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g =>
        {
            var doMes = g.Select(p => EstiloDe(p.Texto)).Where(e => e is not null).GroupBy(e => e!).OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal).FirstOrDefault();
            return (g.Key, g.Count(), g.Count(p => EstiloDe(p.Texto) is not null), doMes?.Key ?? "—", doMes?.Count() ?? 0);
        }).ToList();
        return new Resultado
        {
            Base = nome, Pedidos = pedidos.Count, PedidosDeEstilo = estilos.Count, EstilosDistintos = porEstilo.Count,
            Estilos = porEstilo.Take(15).Select(g => (g.Key, g.Count())).ToList(),
            ConcentracaoTop10 = estilos.Count == 0 ? 0 : (double)porEstilo.Take(10).Sum(g => g.Count()) / estilos.Count,
            RepetidosIdenticos = inicios.Where(g => g.Count() > 1).Sum(g => g.Count() - 1),
            MaisRepetidos = inicios.Where(g => g.Count() > 1).OrderByDescending(g => g.Count()).Take(8).Select(g => (g.Key.Length > 90 ? g.Key[..90] + "…" : g.Key, g.Count())).ToList(),
            PorMes = porMes,
        };
    }

    private static string Pct(double x) => (x * 100).ToString("0.0", CultureInfo.GetCultureInfo("pt-BR")) + "%";

    public static string Relatorio(IEnumerable<Resultado> resultados)
    {
        var sb = new StringBuilder("# Cenário 7a — Demanda real por estilos (WildChat)\n\n");
        sb.AppendLine("Gerado por `dotnet run --project src/Orquestracao.Experimento -- --cenario estilos --analisar`, sem LLM. Um pedido é \"de estilo\" quando traz um marcador como \"no estilo de\", \"como um/uma\", \"in the style of\", \"as if you were\", \"like a\" ou \"act as\"; o estilo é o que vem depois do marcador (até 4 palavras). Os padrões são simples e contam também usos que não são pedidos de estilo (\"como um todo\"); os números são uma aproximação.");
        sb.AppendLine();
        foreach (var r in resultados)
        {
            sb.AppendLine($"## {r.Base}");
            sb.AppendLine();
            sb.AppendLine("| Medida | Valor |");
            sb.AppendLine("|---|---:|");
            sb.AppendLine($"| Pedidos (primeiro turno de cada conversa) | {r.Pedidos} |");
            sb.AppendLine($"| Pedidos de estilo | {r.PedidosDeEstilo} ({Pct(r.Pedidos == 0 ? 0 : (double)r.PedidosDeEstilo / r.Pedidos)}) |");
            sb.AppendLine($"| Estilos distintos | {r.EstilosDistintos} |");
            sb.AppendLine($"| Fração dos pedidos de estilo nos 10 estilos mais pedidos | {Pct(r.ConcentracaoTop10)} |");
            sb.AppendLine($"| Pedidos idênticos a outro (cache puro, pelos primeiros 200 caracteres) | {r.RepetidosIdenticos} ({Pct(r.Pedidos == 0 ? 0 : (double)r.RepetidosIdenticos / r.Pedidos)}) |");
            sb.AppendLine();
            sb.AppendLine("**Estilos mais pedidos:** " + string.Join("; ", r.Estilos.Select(e => $"\"{e.Estilo}\" ({e.Vezes})")) + ".");
            sb.AppendLine();
            sb.AppendLine("**Pedidos mais repetidos (início):**");
            sb.AppendLine();
            foreach (var (i, v) in r.MaisRepetidos) sb.AppendLine($"- {v}× `{i.Replace("`", "'")}`");
            sb.AppendLine();
            sb.AppendLine("| Mês | Pedidos | De estilo | Estilo mais pedido no mês |");
            sb.AppendLine("|---|---:|---:|---|");
            foreach (var m in r.PorMes) sb.AppendLine($"| {m.Mes} | {m.Pedidos} | {m.DeEstilo} | {m.Top} ({m.TopVezes}) |");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
