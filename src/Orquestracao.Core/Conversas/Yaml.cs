using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Orquestracao.Core.Conversas;

/// <summary>
/// O bloco ```yaml das personas, lido como uma árvore JSON (para validar os tipos como no app
/// original: <c>true</c> sem aspas é booleano, <c>"true"</c> é texto) e escrito de volta.
/// </summary>
public static class Yaml
{
    /// <summary>Lê o yaml; vazio vira um objeto vazio. Erros de sintaxe viram <see cref="YamlException"/>.</summary>
    public static JsonNode? Ler(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return new JsonObject();
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return stream.Documents.Count == 0 ? null : Converter(stream.Documents[0].RootNode);
    }

    private static JsonNode? Converter(YamlNode no) => no switch
    {
        YamlMappingNode m => new JsonObject(m.Children.Select(kv =>
            KeyValuePair.Create(((YamlScalarNode)kv.Key).Value ?? "", Converter(kv.Value)))),
        YamlSequenceNode s => new JsonArray(s.Children.Select(Converter).ToArray()),
        YamlScalarNode e => Escalar(e),
        _ => null,
    };

    private static JsonNode? Escalar(YamlScalarNode e)
    {
        var v = e.Value ?? "";
        if (e.Style is ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted or ScalarStyle.Literal or ScalarStyle.Folded)
            return JsonValue.Create(v);
        switch (v)
        {
            case "" or "~" or "null" or "Null" or "NULL":
                return null;
            case "true" or "True" or "TRUE":
                return JsonValue.Create(true);
            case "false" or "False" or "FALSE":
                return JsonValue.Create(false);
        }
        if (long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return JsonValue.Create(i);
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return JsonValue.Create(d);
        return JsonValue.Create(v);
    }

    /// <summary>Escreve campos simples (texto, número, booleano, lista de textos, objeto de um nível).</summary>
    public static string Escrever(IEnumerable<KeyValuePair<string, JsonNode?>> campos)
    {
        var sb = new StringBuilder();
        foreach (var (k, v) in campos)
        {
            switch (v)
            {
                case JsonArray a:
                    sb.Append(k).Append(":\n");
                    foreach (var item in a) sb.Append("  - ").Append(Valor(item)).Append('\n');
                    break;
                case JsonObject o:
                    sb.Append(k).Append(":\n");
                    foreach (var (k2, v2) in o) sb.Append("  ").Append(k2).Append(": ").Append(Valor(v2)).Append('\n');
                    break;
                default:
                    sb.Append(k).Append(": ").Append(Valor(v)).Append('\n');
                    break;
            }
        }
        return sb.ToString();
    }

    private static string Valor(JsonNode? v)
    {
        if (v is null) return "null";
        var j = v.AsValue();
        if (j.TryGetValue<bool>(out var b)) return b ? "true" : "false";
        if (j.TryGetValue<string>(out var s)) return Texto(s);
        return j.ToJsonString();
    }

    /// <summary>Texto sem aspas quando é seguro; com aspas quando seria lido como outra coisa.</summary>
    private static string Texto(string s)
    {
        var precisa = s.Length == 0
            || s != s.Trim()
            || "#&*!|>'\"%@`-?:,[]{}".Contains(s[0])
            || s.Contains(": ") || s.Contains(" #") || s.Contains('\n')
            || s is "true" or "false" or "null" or "~" or "yes" or "no"
            || double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        return precisa ? "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"" : s;
    }
}
