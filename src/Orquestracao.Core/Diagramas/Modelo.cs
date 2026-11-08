using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orquestracao.Core.Diagramas;

/// <summary>Um retângulo do diagrama. O JSON é o mesmo do app original (o front depende dele).</summary>
public sealed class DiagramNode
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";

    /// <summary>Centro do retângulo, em pixels do canvas.</summary>
    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>Retângulos ativos ganham a borda verde (classe Mermaid <c>ativo</c>). Só vai no JSON quando é true.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Active { get; set; }

    /// <summary>Quem cuida desta etapa (texto livre, em uma linha).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Owner { get; set; }

    public DiagramNode Clone() => (DiagramNode)MemberwiseClone();
}

/// <summary>Uma seta do diagrama.</summary>
public sealed class DiagramEdge
{
    public string Id { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Text { get; set; } = "";

    /// <summary>Seta animada: o tracejado corre de origem para destino (sintaxe <c>e1@{ animate: true }</c>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Animated { get; set; }

    public DiagramEdge Clone() => (DiagramEdge)MemberwiseClone();
}

public sealed class Diagram
{
    public List<DiagramNode> Nodes { get; set; } = [];
    public List<DiagramEdge> Edges { get; set; } = [];

    public Diagram Clone() => new() { Nodes = Nodes.ConvertAll(n => n.Clone()), Edges = Edges.ConvertAll(e => e.Clone()) };
}

/// <summary>As opções de JSON usadas em toda a solução: camelCase e acentos sem escape.</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // O "type" dos eventos pode vir em qualquer posição do objeto (o front não garante a ordem).
        AllowOutOfOrderMetadataProperties = true,
    };

    public static string Serializar<T>(T valor) => JsonSerializer.Serialize(valor, Opcoes);

    public static T? Ler<T>(string json) => JsonSerializer.Deserialize<T>(json, Opcoes);
}
