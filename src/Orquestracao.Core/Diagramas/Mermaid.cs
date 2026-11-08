using System.Globalization;
using System.Text.RegularExpressions;

namespace Orquestracao.Core.Diagramas;

public sealed record ResultadoLeitura(Diagram Diagram, List<string> Errors);

/// <summary>
/// Código Mermaid de ida e volta. As posições do canvas vão em comentários <c>%% @pos</c>, que o
/// Mermaid ignora, para que o código sozinho reconstrua o desenho. O responsável vai no texto do
/// retângulo, numa segunda linha, para aparecer também no desenho do Mermaid.
/// </summary>
public static class Mermaid
{
    // Ids só com ASCII, como no JavaScript (\w no .NET aceitaria letras acentuadas).
    private const string Id = "[A-Za-z_][A-Za-z0-9_-]*";
    private const string Num = "-?[0-9]+(?:\\.[0-9]+)?";

    public static readonly Regex IdRe = new($"^{Id}$", RegexOptions.Compiled);
    private static readonly Regex HeaderRe = new("^(flowchart|graph)(\\s+(TB|TD|BT|LR|RL))?$", RegexOptions.Compiled);
    private static readonly Regex NodeRe = new($"^({Id})\\((?:\"([^\"]*)\"|([^()\"]*))\\)$", RegexOptions.Compiled);
    // `n1 e1@--> n2`: o id antes de `@` é o id da seta (Mermaid 11+), usado para animá-la.
    private static readonly Regex EdgeRe = new($"^({Id})(?:\\s+({Id})@|\\s*)-->\\s*(?:\\|(?:\"([^\"]*)\"|([^|\"]*))\\|\\s*)?({Id})$", RegexOptions.Compiled);
    private static readonly Regex AnimateRe = new($"^({Id})@\\{{\\s*animate\\s*:\\s*true\\s*\\}}$", RegexOptions.Compiled);
    private static readonly Regex ClassRe = new($"^class\\s+({Id}(?:\\s*,\\s*{Id})*)\\s+({Id})$", RegexOptions.Compiled);
    private static readonly Regex OwnerRe = new($"^%%\\s*@owner\\s+({Id})\\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex PosRe = new($"^%%\\s*@pos\\s+({Id})\\s+({Num})\\s+({Num})$", RegexOptions.Compiled);

    public const string OwnerMark = "👤";
    private static readonly Regex LabelRe = new($"^(.*)<br/><small>{OwnerMark} (.+)</small>$", RegexOptions.Compiled);

    public const int OwnerMax = 60;
    public const string ActiveClass = "ativo";
    public const string ActiveColor = "#1f9d55";

    private static readonly Regex Espacos = new("\\s+", RegexOptions.Compiled);

    /// <summary>Responsável em uma linha só: espaços repetidos e quebras viram um espaço.</summary>
    public static string CleanOwner(string s) => Espacos.Replace(s.Trim(), " ");

    // `<` e `>` também são escapados: o texto nunca pode ser lido como o HTML que separa o responsável.
    private static string Escape(string s) => s.Replace("\"", "#quot;").Replace("<", "#lt;").Replace(">", "#gt;");
    private static string Unescape(string s) => s.Replace("#quot;", "\"").Replace("#lt;", "<").Replace("#gt;", ">");

    private static string LabelOf(DiagramNode n) =>
        n.Owner is { Length: > 0 } ? $"{Escape(n.Text)}<br/><small>{OwnerMark} {Escape(n.Owner)}</small>" : Escape(n.Text);

    /// <summary>Arredonda como o Math.round do JavaScript (meio para cima).</summary>
    private static long Round(double v) => (long)Math.Floor(v + 0.5);

    public static string Gerar(Diagram d)
    {
        var lines = new List<string> { "flowchart LR" };
        foreach (var n in d.Nodes) lines.Add($"    {n.Id}(\"{LabelOf(n)}\")");
        foreach (var e in d.Edges)
        {
            var arrow = e.Animated ? $"{e.Id}@-->" : "-->";
            lines.Add(e.Text.Length > 0 ? $"    {e.From} {arrow}|\"{Escape(e.Text)}\"| {e.To}" : $"    {e.From} {arrow} {e.To}");
        }
        foreach (var e in d.Edges.Where(e => e.Animated)) lines.Add($"    {e.Id}@{{ animate: true }}");
        var active = d.Nodes.Where(n => n.Active).Select(n => n.Id).ToList();
        if (active.Count > 0)
        {
            lines.Add($"    classDef {ActiveClass} stroke:{ActiveColor},stroke-width:3px");
            lines.Add($"    class {string.Join(',', active)} {ActiveClass}");
        }
        if (d.Nodes.Count > 0) lines.Add("");
        foreach (var n in d.Nodes) lines.Add($"%% @pos {n.Id} {Round(n.X)} {Round(n.Y)}");
        return string.Join('\n', lines);
    }

    /// <summary>
    /// Lê o subconjunto suportado: retângulos arredondados <c>A(texto)</c> e setas <c>A --&gt;|texto| B</c>.
    /// Nós sem <c>%% @pos</c> reaproveitam a posição de <paramref name="previous"/> ou recebem uma vaga em grade.
    /// </summary>
    public static ResultadoLeitura Ler(string code, Diagram? previous = null)
    {
        var errors = new List<string>();
        var nodes = new List<DiagramNode>();
        var porId = new Dictionary<string, DiagramNode>();
        var edges = new List<DiagramEdge>();
        var positions = new Dictionary<string, (double X, double Y)>();
        var activeIds = new List<string>();
        var animatedIds = new HashSet<string>();
        var owners = new Dictionary<string, string>();
        var ownerOrder = new List<string>();

        DiagramNode EnsureNode(string id)
        {
            if (!porId.TryGetValue(id, out var n))
            {
                n = new DiagramNode { Id = id, Text = id, X = double.NaN, Y = double.NaN };
                porId[id] = n;
                nodes.Add(n);
            }
            return n;
        }

        void SetOwner(string id, string owner)
        {
            if (!owners.ContainsKey(id)) ownerOrder.Add(id);
            owners[id] = owner;
        }

        var raw = Regex.Split(code, "\r?\n");
        for (var i = 0; i < raw.Length; i++)
        {
            var line = raw[i].Trim();
            if (line.EndsWith(';')) line = line[..^1];
            if (line.Length == 0) continue;

            if (PosRe.Match(line) is { Success: true } pos)
            {
                positions[pos.Groups[1].Value] = (Num_(pos.Groups[2].Value), Num_(pos.Groups[3].Value));
                continue;
            }
            if (OwnerRe.Match(line) is { Success: true } owner)
            {
                // Formato antigo (comentário): só vale se o texto do retângulo não trouxer o responsável.
                if (!owners.ContainsKey(owner.Groups[1].Value)) SetOwner(owner.Groups[1].Value, Corta(CleanOwner(owner.Groups[2].Value)));
                continue;
            }
            if (line.StartsWith("%%") || line.StartsWith("classDef ") || HeaderRe.IsMatch(line)) continue;

            if (AnimateRe.Match(line) is { Success: true } anim)
            {
                animatedIds.Add(anim.Groups[1].Value);
                continue;
            }
            if (ClassRe.Match(line) is { Success: true } cls)
            {
                if (cls.Groups[2].Value == ActiveClass)
                    foreach (var id in cls.Groups[1].Value.Split(',')) activeIds.Add(id.Trim());
                continue;
            }
            if (NodeRe.Match(line) is { Success: true } node)
            {
                var texto = node.Groups[2].Success ? node.Groups[2].Value : node.Groups[3].Value;
                var parts = LabelRe.Match(texto);
                EnsureNode(node.Groups[1].Value).Text = Unescape(parts.Success ? parts.Groups[1].Value : texto);
                if (parts.Success) SetOwner(node.Groups[1].Value, Corta(CleanOwner(Unescape(parts.Groups[2].Value))));
                continue;
            }
            if (EdgeRe.Match(line) is { Success: true } edge)
            {
                var from = edge.Groups[1].Value;
                var to = edge.Groups[5].Value;
                EnsureNode(from);
                EnsureNode(to);
                // O id escrito no código (`e1@-->`) vale, se não repetir o de outra seta.
                var edgeId = edge.Groups[2].Success ? edge.Groups[2].Value : "";
                var id = edgeId.Length > 0 && !edges.Exists(e => e.Id == edgeId) ? edgeId : "";
                var text = edge.Groups[3].Success ? edge.Groups[3].Value : edge.Groups[4].Success ? edge.Groups[4].Value : "";
                edges.Add(new DiagramEdge { Id = id, From = from, To = to, Text = Unescape(text).Trim() });
                continue;
            }
            errors.Add($"Linha {i + 1}: não reconhecida — \"{line}\"");
        }

        foreach (var id in activeIds)
            if (porId.TryGetValue(id, out var n)) n.Active = true;

        foreach (var id in ownerOrder)
            if (porId.TryGetValue(id, out var n) && owners[id].Length > 0) n.Owner = owners[id];

        var prev = previous?.Nodes.ToDictionary(n => n.Id) ?? [];
        var unplaced = new List<DiagramNode>();
        foreach (var n in nodes)
        {
            if (positions.TryGetValue(n.Id, out var p)) (n.X, n.Y) = p;
            else if (prev.TryGetValue(n.Id, out var pn)) (n.X, n.Y) = (pn.X, pn.Y);
            else unplaced.Add(n);
        }
        var taken = nodes.Where(n => !unplaced.Contains(n)).Select(n => (n.X, n.Y)).ToList();
        foreach (var n in unplaced)
        {
            (n.X, n.Y) = FreeSlot(taken);
            taken.Add((n.X, n.Y));
        }

        // O Mermaid não guarda id de seta: reaproveita o id da seta equivalente (mesma origem e destino)
        // do diagrama anterior, para que editar o código não troque o id das setas que já existiam.
        var unused = (previous?.Edges ?? []).Where(p => !edges.Exists(e => e.Id == p.Id)).ToList();
        foreach (var e in edges.Where(e => e.Id.Length == 0))
        {
            var i = unused.FindIndex(p => p.From == e.From && p.To == e.To);
            if (i >= 0)
            {
                e.Id = unused[i].Id;
                unused.RemoveAt(i);
            }
        }
        foreach (var e in edges.Where(e => e.Id.Length == 0)) e.Id = NextId("e", edges.Select(x => x.Id));
        foreach (var e in edges) if (animatedIds.Contains(e.Id)) e.Animated = true;

        return new ResultadoLeitura(new Diagram { Nodes = nodes, Edges = edges }, errors);
    }

    private static double Num_(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    private static string Corta(string s) => s.Length > OwnerMax ? s[..OwnerMax] : s;

    /// <summary>Primeira vaga livre de uma grade, longe dos retângulos em <paramref name="taken"/>.</summary>
    public static (double X, double Y) FreeSlot(IEnumerable<(double X, double Y)> taken)
    {
        var ocupados = taken.ToList();
        for (var slot = 0; ; slot++)
        {
            (double X, double Y) p = (140 + slot % 4 * 220, 80 + slot / 4 * 110);
            if (!ocupados.Exists(t => Math.Abs(t.X - p.X) < 180 && Math.Abs(t.Y - p.Y) < 70)) return p;
        }
    }

    public static string NextId(string prefix, IEnumerable<string> ids)
    {
        var re = new Regex($"^{Regex.Escape(prefix)}([0-9]+)$");
        long max = 0;
        foreach (var id in ids)
            if (re.Match(id) is { Success: true } m) max = Math.Max(max, long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
        return $"{prefix}{max + 1}";
    }
}
