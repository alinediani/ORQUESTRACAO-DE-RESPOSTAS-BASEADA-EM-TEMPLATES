namespace Orquestracao.Core.Diagramas;

/// <summary>Erro de uma operação, com o status HTTP que a API devolve.</summary>
public sealed class OpError(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

/// <summary>
/// Alteração de um retângulo: só os campos não nulos mudam. Em <see cref="Owner"/>, null deixa como
/// está e texto vazio (ou só espaços) remove o responsável.
/// </summary>
public record NodePatch
{
    public string? Text { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public bool? Active { get; init; }
    public string? Owner { get; init; }
}

public sealed record NewNode : NodePatch
{
    public string? Id { get; init; }
}

public sealed record EdgePatch
{
    public string? Text { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public bool? Animated { get; init; }
}

public sealed record NewEdge
{
    public string? Id { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public string? Text { get; init; }
    public bool? Animated { get; init; }
}

/// <summary>
/// Operações granulares sobre o diagrama. Cada uma altera o <see cref="Diagram"/> recebido e devolve o
/// evento que descreve a mudança. A validação dos tipos do JSON fica em <see cref="Entrada"/>.
/// </summary>
public static class Ops
{
    private static void CheckNodePatch(NodePatch p)
    {
        if (p.Text is not null && string.IsNullOrWhiteSpace(p.Text)) throw new OpError(400, "\"text\" deve ser um texto não vazio.");
        if (p.X is { } x && !double.IsFinite(x)) throw new OpError(400, "\"x\" deve ser um número.");
        if (p.Y is { } y && !double.IsFinite(y)) throw new OpError(400, "\"y\" deve ser um número.");
        if (p.Owner is not null && Mermaid.CleanOwner(p.Owner).Length > Mermaid.OwnerMax)
            throw new OpError(400, $"\"owner\" deve ter no máximo {Mermaid.OwnerMax} caracteres.");
    }

    private static void CheckNewId(string? id, IEnumerable<string> taken)
    {
        if (id is null) return;
        if (!Mermaid.IdRe.IsMatch(id)) throw new OpError(400, $"Id inválido: \"{id}\".");
        if (taken.Contains(id)) throw new OpError(409, $"Já existe um elemento com o id \"{id}\".");
    }

    public static DiagramNode GetNode(Diagram d, string id) =>
        d.Nodes.Find(n => n.Id == id) ?? throw new OpError(404, $"Retângulo \"{id}\" não encontrado.");

    public static DiagramEdge GetEdge(Diagram d, string id) =>
        d.Edges.Find(e => e.Id == id) ?? throw new OpError(404, $"Seta \"{id}\" não encontrada.");

    /// <summary>Encontra um retângulo pelo id ou, se não houver, pelo texto (sem diferenciar maiúsculas).</summary>
    public static DiagramNode ResolveNode(Diagram d, string reference)
    {
        if (d.Nodes.Find(n => n.Id == reference) is { } byId) return byId;
        var wanted = reference.Trim().ToLowerInvariant();
        var byText = d.Nodes.Where(n => n.Text.Trim().ToLowerInvariant() == wanted).ToList();
        if (byText.Count > 1) throw new OpError(409, $"Mais de um retângulo com o texto \"{reference}\"; use o id.");
        return byText.Count == 1 ? byText[0] : throw new OpError(404, $"Retângulo \"{reference}\" não encontrado.");
    }

    private static void CheckEdgeEnds(Diagram d, string? from, string? to, string? ignoreId = null)
    {
        foreach (var (campo, id) in new[] { ("from", from), ("to", to) })
            if (id is null || !d.Nodes.Exists(n => n.Id == id))
                throw new OpError(400, $"\"{campo}\": retângulo \"{id ?? "undefined"}\" não existe.");
        if (d.Edges.Exists(e => e.Id != ignoreId && e.From == from && e.To == to))
            throw new OpError(409, $"Já existe uma seta de \"{from}\" para \"{to}\".");
    }

    // ---- retângulos --------------------------------------------------------

    public static DiagramEvent AddNode(Diagram d, NewNode input)
    {
        CheckNodePatch(input);
        CheckNewId(input.Id, d.Nodes.Select(n => n.Id));
        var slot = input.X is null || input.Y is null ? Mermaid.FreeSlot(d.Nodes.Select(n => (n.X, n.Y))) : default;
        var owner = input.Owner is null ? "" : Mermaid.CleanOwner(input.Owner);
        var node = new DiagramNode
        {
            Id = input.Id ?? Mermaid.NextId("n", d.Nodes.Select(n => n.Id)),
            Text = input.Text?.Trim() ?? "Novo",
            X = input.X ?? slot.X,
            Y = input.Y ?? slot.Y,
            Active = input.Active == true,
            Owner = owner.Length > 0 ? owner : null,
        };
        d.Nodes.Add(node);
        return new NodeAdded(node);
    }

    public static DiagramEvent UpdateNode(Diagram d, string id, NodePatch patch)
    {
        CheckNodePatch(patch);
        var n = GetNode(d, id);
        if (patch.Text is not null) n.Text = patch.Text.Trim();
        if (patch.X is { } x) n.X = x;
        if (patch.Y is { } y) n.Y = y;
        if (patch.Active is { } active) n.Active = active;
        if (patch.Owner is not null)
        {
            var owner = Mermaid.CleanOwner(patch.Owner);
            n.Owner = owner.Length > 0 ? owner : null;
        }
        return new NodeUpdated(n);
    }

    public static DiagramEvent RemoveNode(Diagram d, string id)
    {
        GetNode(d, id);
        var edges = d.Edges.Where(e => e.From == id || e.To == id).Select(e => e.Id).ToList();
        d.Nodes.RemoveAll(n => n.Id == id);
        d.Edges.RemoveAll(e => edges.Contains(e.Id));
        return new NodeRemoved(id, edges);
    }

    // ---- setas -------------------------------------------------------------

    public static DiagramEvent AddEdge(Diagram d, NewEdge input)
    {
        CheckNewId(input.Id, d.Edges.Select(e => e.Id));
        CheckEdgeEnds(d, input.From, input.To);
        var edge = new DiagramEdge
        {
            Id = input.Id ?? Mermaid.NextId("e", d.Edges.Select(e => e.Id)),
            From = input.From!,
            To = input.To!,
            Text = input.Text?.Trim() ?? "",
            Animated = input.Animated == true,
        };
        d.Edges.Add(edge);
        return new EdgeAdded(edge);
    }

    public static DiagramEvent UpdateEdge(Diagram d, string id, EdgePatch patch)
    {
        var e = GetEdge(d, id);
        var from = patch.From ?? e.From;
        var to = patch.To ?? e.To;
        if (from != e.From || to != e.To) CheckEdgeEnds(d, from, to, e.Id);
        e.From = from;
        e.To = to;
        if (patch.Text is not null) e.Text = patch.Text.Trim();
        if (patch.Animated is { } animated) e.Animated = animated;
        return new EdgeUpdated(e);
    }

    public static DiagramEvent RemoveEdge(Diagram d, string id)
    {
        GetEdge(d, id);
        d.Edges.RemoveAll(e => e.Id == id);
        return new EdgeRemoved(id);
    }

    // ---- diagrama inteiro --------------------------------------------------

    /// <summary>Aplica um evento (vindo de outra cópia do diagrama) a <paramref name="d"/>.</summary>
    public static Diagram ApplyEvent(Diagram d, DiagramEvent ev)
    {
        switch (ev)
        {
            case NodeAdded(var node):
                Substituir(d.Nodes, node.Clone(), n => n.Id == node.Id);
                return d;
            case NodeUpdated(var node):
                Substituir(d.Nodes, node.Clone(), n => n.Id == node.Id);
                return d;
            case NodeRemoved(var id, var edges):
                d.Nodes.RemoveAll(n => n.Id == id);
                d.Edges.RemoveAll(e => edges.Contains(e.Id) || e.From == id || e.To == id);
                return d;
            case EdgeAdded(var edge):
                Substituir(d.Edges, edge.Clone(), e => e.Id == edge.Id);
                return d;
            case EdgeUpdated(var edge):
                Substituir(d.Edges, edge.Clone(), e => e.Id == edge.Id);
                return d;
            case EdgeRemoved(var id):
                d.Edges.RemoveAll(e => e.Id == id);
                return d;
            case DiagramReplaced(var diagram):
                return diagram.Clone();
            default:
                throw new ArgumentException($"Evento desconhecido: {ev.Type}");
        }
    }

    private static void Substituir<T>(List<T> lista, T item, Predicate<T> igual)
    {
        var i = lista.FindIndex(igual);
        if (i >= 0) lista[i] = item;
        else lista.Add(item);
    }
}
