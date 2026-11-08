using System.Text.Json.Serialization;

namespace Orquestracao.Core.Diagramas;

/// <summary>
/// O evento que descreve uma mudança no diagrama. Serializado como no app original:
/// <c>{"type":"node.updated","node":{…}}</c>. Aplicado com <see cref="Ops.ApplyEvent"/>, reproduz a
/// mudança em outra cópia do diagrama.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(NodeAdded), "node.added")]
[JsonDerivedType(typeof(NodeUpdated), "node.updated")]
[JsonDerivedType(typeof(NodeRemoved), "node.removed")]
[JsonDerivedType(typeof(EdgeAdded), "edge.added")]
[JsonDerivedType(typeof(EdgeUpdated), "edge.updated")]
[JsonDerivedType(typeof(EdgeRemoved), "edge.removed")]
[JsonDerivedType(typeof(DiagramReplaced), "diagram.replaced")]
public abstract record DiagramEvent
{
    /// <summary>O nome do tipo, igual ao do JSON (<c>node.added</c>…).</summary>
    [JsonIgnore]
    public string Type => this switch
    {
        NodeAdded => "node.added",
        NodeUpdated => "node.updated",
        NodeRemoved => "node.removed",
        EdgeAdded => "edge.added",
        EdgeUpdated => "edge.updated",
        EdgeRemoved => "edge.removed",
        DiagramReplaced => "diagram.replaced",
        _ => throw new InvalidOperationException(GetType().Name),
    };
}

public sealed record NodeAdded(DiagramNode Node) : DiagramEvent;

public sealed record NodeUpdated(DiagramNode Node) : DiagramEvent;

/// <summary>O retângulo removido e os ids das setas que saíram junto.</summary>
public sealed record NodeRemoved(string Id, List<string> Edges) : DiagramEvent;

public sealed record EdgeAdded(DiagramEdge Edge) : DiagramEvent;

public sealed record EdgeUpdated(DiagramEdge Edge) : DiagramEvent;

public sealed record EdgeRemoved(string Id) : DiagramEvent;

public sealed record DiagramReplaced(Diagram Diagram) : DiagramEvent;
