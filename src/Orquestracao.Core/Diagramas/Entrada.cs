using System.Text.Json;

namespace Orquestracao.Core.Diagramas;

/// <summary>
/// Lê o JSON que chega pela API e o transforma nos tipos das <see cref="Ops"/>, com as mesmas mensagens
/// de erro (400) do app original: corpo que não é objeto, campo desconhecido, tipo errado.
/// </summary>
public static class Entrada
{
    private static readonly string[] CamposNo = ["text", "x", "y", "active", "owner"];
    private static readonly string[] CamposSeta = ["text", "from", "to", "animated"];

    public static NewNode NovoNo(JsonElement json)
    {
        var props = Campos(json, CamposNo, "id");
        return new NewNode
        {
            Id = props.TryGetValue("id", out var id) ? Id(id) : null,
            Text = Texto(props, "text", "\"text\" deve ser um texto não vazio."),
            X = Numero(props, "x"),
            Y = Numero(props, "y"),
            Active = Booleano(props, "active", "\"active\" deve ser true ou false."),
            Owner = Owner(props),
        };
    }

    public static NodePatch PatchNo(JsonElement json)
    {
        var props = Campos(json, CamposNo);
        return new NodePatch
        {
            Text = Texto(props, "text", "\"text\" deve ser um texto não vazio."),
            X = Numero(props, "x"),
            Y = Numero(props, "y"),
            Active = Booleano(props, "active", "\"active\" deve ser true ou false."),
            Owner = Owner(props),
        };
    }

    public static NewEdge NovaSeta(JsonElement json)
    {
        var props = Campos(json, CamposSeta, "id");
        return new NewEdge
        {
            Id = props.TryGetValue("id", out var id) ? Id(id) : null,
            From = Ponta(props, "from"),
            To = Ponta(props, "to"),
            Text = Texto(props, "text", "\"text\" deve ser um texto."),
            Animated = Booleano(props, "animated", "\"animated\" deve ser true ou false."),
        };
    }

    public static EdgePatch PatchSeta(JsonElement json)
    {
        var props = Campos(json, CamposSeta);
        return new EdgePatch
        {
            Text = Texto(props, "text", "\"text\" deve ser um texto."),
            From = Ponta(props, "from"),
            To = Ponta(props, "to"),
            Animated = Booleano(props, "animated", "\"animated\" deve ser true ou false."),
        };
    }

    /// <summary>Monta um diagrama validando cada retângulo e seta com as mesmas regras das operações.</summary>
    public static Diagram Diagrama(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object
            || !json.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array
            || !json.TryGetProperty("edges", out var edges) || edges.ValueKind != JsonValueKind.Array)
            throw new OpError(400, "Esperado um objeto { \"nodes\": [...], \"edges\": [...] }.");
        var d = new Diagram();
        foreach (var n in nodes.EnumerateArray()) Ops.AddNode(d, NovoNo(n));
        foreach (var e in edges.EnumerateArray()) Ops.AddEdge(d, NovaSeta(e));
        return d;
    }

    /// <summary>Lê um texto JSON; JSON inválido vira 400.</summary>
    public static JsonElement Ler(string texto)
    {
        try
        {
            using var doc = JsonDocument.Parse(texto);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new OpError(400, "JSON inválido.");
        }
    }

    private static Dictionary<string, JsonElement> Campos(JsonElement json, string[] aceitos, string? extra = null)
    {
        if (json.ValueKind != JsonValueKind.Object) throw new OpError(400, "O corpo deve ser um objeto JSON.");
        var props = new Dictionary<string, JsonElement>();
        foreach (var p in json.EnumerateObject()) props[p.Name] = p.Value;
        var desconhecidos = props.Keys.Where(k => !aceitos.Contains(k) && k != extra).ToList();
        if (desconhecidos.Count > 0)
            throw new OpError(400, $"Campo(s) desconhecido(s): {string.Join(", ", desconhecidos)}. Aceitos: {string.Join(", ", aceitos)}.");
        return props;
    }

    private static string Id(JsonElement v) =>
        v.ValueKind == JsonValueKind.String ? v.GetString()! : throw new OpError(400, $"Id inválido: \"{v.GetRawText()}\".");

    private static string? Texto(Dictionary<string, JsonElement> p, string campo, string erro)
    {
        if (!p.TryGetValue(campo, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : throw new OpError(400, erro);
    }

    private static double? Numero(Dictionary<string, JsonElement> p, string campo)
    {
        if (!p.TryGetValue(campo, out var v)) return null;
        return v.ValueKind == JsonValueKind.Number ? v.GetDouble() : throw new OpError(400, $"\"{campo}\" deve ser um número.");
    }

    private static bool? Booleano(Dictionary<string, JsonElement> p, string campo, string erro)
    {
        if (!p.TryGetValue(campo, out var v)) return null;
        return v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : throw new OpError(400, erro);
    }

    /// <summary><c>owner</c>: ausente deixa como está; null ou vazio remove (vira "").</summary>
    private static string? Owner(Dictionary<string, JsonElement> p)
    {
        if (!p.TryGetValue("owner", out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Null => "",
            JsonValueKind.String => v.GetString(),
            _ => throw new OpError(400, "\"owner\" deve ser um texto (ou null para remover)."),
        };
    }

    /// <summary>Ponta da seta: ausente fica null (na criação, CheckEdgeEnds recusa; na alteração, mantém).</summary>
    private static string? Ponta(Dictionary<string, JsonElement> p, string campo)
    {
        if (!p.TryGetValue(campo, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : throw new OpError(400, $"\"{campo}\": retângulo \"{v.GetRawText()}\" não existe.");
    }
}
