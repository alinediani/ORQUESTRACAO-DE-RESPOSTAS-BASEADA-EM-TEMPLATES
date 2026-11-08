using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Orquestracao.Core.Diagramas;

namespace Orquestracao.Core.Conversas;

public sealed record Pessoa(string Id, string Nome);

public sealed record Delegacao(string Para, string Tarefa);

/// <summary>
/// As setas dizem quem responde a quem: A → B faz B falar depois de A, respondendo a ele. No modo
/// tarefa, a mesma seta quer dizer "A pode delegar para B". Tudo aqui é função pura do diagrama.
/// </summary>
public static partial class Turnos
{
    private static IEnumerable<DiagramEdge> Outras(Diagram d) => d.Edges.Where(e => e.From != e.To);

    /// <summary>Quem abre a rodada: os retângulos sem seta chegando; num ciclo puro, o primeiro.</summary>
    public static List<string> Raizes(Diagram d)
    {
        var comEntrada = Outras(d).Select(e => e.To).ToHashSet();
        var r = d.Nodes.Where(n => !comEntrada.Contains(n.Id)).Select(n => n.Id).ToList();
        return r.Count > 0 || d.Nodes.Count == 0 ? r : [d.Nodes[0].Id];
    }

    /// <summary>
    /// A ordem de uma rodada: a partir dos blocos de início (sem nenhum, das raízes), em largura e na
    /// ordem das setas, cada retângulo uma vez. Quem não foi alcançado entra depois.
    /// </summary>
    public static List<string> OrdemDaRodada(Diagram d, IEnumerable<string>? inicios = null)
    {
        var vistos = new HashSet<string>();
        var ordem = new List<string>();
        void Percorrer(IEnumerable<string> inicio)
        {
            var fila = new Queue<string>();
            foreach (var id in inicio.Where(vistos.Add)) fila.Enqueue(id);
            while (fila.Count > 0)
            {
                var id = fila.Dequeue();
                ordem.Add(id);
                foreach (var e in Outras(d))
                    if (e.From == id && vistos.Add(e.To)) fila.Enqueue(e.To);
            }
        }
        var lista = inicios?.ToHashSet() ?? [];
        var marcados = d.Nodes.Where(n => lista.Contains(n.Id)).Select(n => n.Id).ToList();
        Percorrer(marcados.Count > 0 ? marcados : Raizes(d));
        foreach (var n in d.Nodes)
            if (!vistos.Contains(n.Id)) Percorrer([n.Id]);
        return ordem;
    }

    /// <summary>A quem <paramref name="id"/> responde: as origens das setas que chegam nele.</summary>
    public static List<string> Predecessores(Diagram d, string id) => Outras(d).Where(e => e.To == id).Select(e => e.From).Distinct().ToList();

    /// <summary>Para quem <paramref name="id"/> pode delegar: os destinos das setas que saem dele.</summary>
    public static List<string> Delegaveis(Diagram d, string id) => Outras(d).Where(e => e.From == id).Select(e => e.To).Distinct().ToList();

    /// <summary>O desenho sem os retângulos <paramref name="ids"/> e sem as setas deles.</summary>
    public static Diagram SemNos(Diagram d, ICollection<string> ids) =>
        ids.Count == 0
            ? d
            : new Diagram { Nodes = d.Nodes.Where(n => !ids.Contains(n.Id)).ToList(), Edges = d.Edges.Where(e => !ids.Contains(e.From) && !ids.Contains(e.To)).ToList() };

    /// <summary>Ids das setas de <paramref name="de"/> para <paramref name="para"/> (para a mensagem percorrer no canvas).</summary>
    public static List<string> Arestas(Diagram d, ICollection<string> de, string para) =>
        d.Edges.Where(e => e.To == para && de.Contains(e.From)).Select(e => e.Id).ToList();

    /// <summary>Minúsculas e sem acento, para comparar nomes.</summary>
    public static string Normalizar(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().ToLowerInvariant().Trim();
    }

    /// <summary>Acha a pessoa pelo nome (ou id), sem diferenciar acento e maiúsculas, aceitando um @ na frente.</summary>
    public static Pessoa? AcharPessoa(string nome, IEnumerable<Pessoa> pessoas)
    {
        var n = Normalizar(nome.TrimStart('@'));
        return pessoas.FirstOrDefault(p => Normalizar(p.Nome) == n || p.Id.ToLowerInvariant() == n);
    }

    /// <summary>A persona mencionada com @ no texto (o nome mais longo que casar).</summary>
    public static string? Mencionada(string texto, IEnumerable<Pessoa> pessoas)
    {
        var t = Normalizar(texto);
        foreach (var p in pessoas.OrderByDescending(p => p.Nome.Length))
            foreach (var alvo in new[] { Normalizar(p.Nome), p.Id.ToLowerInvariant() })
            {
                var i = t.IndexOf("@" + alvo, StringComparison.Ordinal);
                if (i < 0) continue;
                var depois = i + alvo.Length + 1;
                if (depois >= t.Length || !char.IsLetterOrDigit(t[depois])) return p.Id;
            }
        return null;
    }

    [GeneratedRegex(@"^\s*delegar\s+@?([^:]+?)\s*:\s*(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex DelegarRe();

    /// <summary>
    /// Lê as linhas <c>DELEGAR &lt;persona&gt;: &lt;tarefa&gt;</c>. Só valem as permitidas (alcançáveis pelas
    /// setas); as outras voltam em Recusadas. Resto é o texto sem essas linhas.
    /// </summary>
    public static (List<Delegacao> Delegacoes, List<string> Recusadas, string Resto) LerDelegacoes(string texto, IReadOnlyList<Pessoa> permitidas)
    {
        var delegacoes = new List<Delegacao>();
        var recusadas = new List<string>();
        var resto = new List<string>();
        foreach (var linha in texto.Split('\n'))
        {
            var m = DelegarRe().Match(linha.TrimEnd('\r'));
            if (!m.Success)
            {
                resto.Add(linha);
                continue;
            }
            if (AcharPessoa(m.Groups[1].Value, permitidas) is { } p) delegacoes.Add(new Delegacao(p.Id, m.Groups[2].Value.Trim()));
            else recusadas.Add(m.Groups[1].Value.Trim());
        }
        return (delegacoes, recusadas, string.Join('\n', resto).Trim());
    }
}
