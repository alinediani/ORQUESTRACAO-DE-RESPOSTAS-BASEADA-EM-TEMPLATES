using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Orquestracao.Core.Diagramas;
using YamlDotNet.Core;

namespace Orquestracao.Core.Conversas;

/// <summary>Para onde vai o pós-fluxo de uma persona de saída (lido e guardado; não executado nesta versão).</summary>
public sealed record PosFluxo(string Diagrama, string? Modo = null, int? Rodadas = null, string? Prefixo = null);

/// <summary>
/// Cada retângulo é uma persona, descrita em <c>diagrams/&lt;slug&gt;/personas/&lt;id&gt;.md</c>: o texto é o
/// system prompt e um bloco ```yaml traz as opções. O nome é o texto do retângulo. O JSON é o mesmo do
/// app original (o painel da persona no front depende dele).
/// </summary>
public sealed record Persona
{
    public required string Id { get; init; }
    public required string Nome { get; init; }
    public string Prompt { get; init; } = "";
    /// <summary>mcp · ollama · api · template (o orquestrador do TCC).</summary>
    public string Motor { get; init; } = "mcp";
    public string Modelo { get; init; } = "";
    public string Esforco { get; init; } = "medium";
    /// <summary>debatedora · moderadora (sintetiza) · orquestradora (delega) · executora.</summary>
    public string Papel { get; init; } = "debatedora";
    public string Cor { get; init; } = "#2563eb";
    public List<string> Ferramentas { get; init; } = [];
    /// <summary>Bloco de início: a rodada começa por ela.</summary>
    public bool Inicio { get; init; }
    /// <summary>Persona de saída: não fala nas rodadas; roda uma vez no fim e preenche o template em <see cref="Arquivo"/>.</summary>
    public bool Saida { get; init; }
    public string Arquivo { get; init; } = "saida.md";
    /// <summary>Persona humana: é você; na vez dela, a conversa espera a sua resposta (sem motor).</summary>
    public bool Humana { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Prazo { get; init; }
    public string AoVencer { get; init; } = "seguir";
    /// <summary>Motor de fallback do orquestrador por template (motor: template): quem responde às chaves novas.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Fallback { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PosFluxo? Depois { get; init; }
    public bool Memoria { get; init; }
    [JsonConverter(typeof(TodasOuLista))]
    public List<string>? Escuta { get; init; }
    public string Quando { get; init; } = "rodada";
    public int Limite { get; init; } = 4000;
    [JsonConverter(typeof(TodasOuLista))]
    public List<string>? ResumoPara { get; init; }
    public bool Curadoria { get; init; }
}

/// <summary>"todas" (null) ou uma lista de ids, como no yaml.</summary>
public sealed class TodasOuLista : JsonConverter<List<string>?>
{
    public override bool HandleNull => true;

    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.StartArray ? JsonSerializer.Deserialize<List<string>>(ref reader, options) : SkipAndNull(ref reader);

    private static List<string>? SkipAndNull(ref Utf8JsonReader reader)
    {
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteStringValue("todas");
        else JsonSerializer.Serialize(writer, value, options);
    }
}

/// <summary>Leitura, validação e escrita do .md das personas (equivale a legado/server/personas.ts).</summary>
public static partial class Personas
{
    public static readonly string[] Motores = ["mcp", "ollama", "api", "template"];
    public static readonly string[] Papeis = ["debatedora", "moderadora", "orquestradora", "executora"];
    public static readonly string[] Esforcos = ["low", "medium", "high", "xhigh", "max"];
    public static readonly string[] FerramentasAceitas = ["web_search"];

    /// <summary>Modelo de cada motor quando a persona não diz qual.</summary>
    public static readonly IReadOnlyDictionary<string, string> ModeloPadrao = new Dictionary<string, string>
    {
        ["mcp"] = "claude-opus-5-5",
        ["ollama"] = "qwen2.5:3b",
        ["api"] = "claude-opus-5-5",
        ["template"] = "claude-haiku-5-5",
    };

    public const string MemoriaPadrao = "# Memória\n\n## Decisões\n\n## Fatos\n\n## Hipóteses\n\n## Pendências\n\n## Preferências do usuário\n";

    public const string TemplateSaidaPadrao =
        "# {{título}}\n\n## Resumo\n{{o resultado da conversa em poucas linhas}}\n\n## Detalhes\n{{os pontos principais, em tópicos}}\n\n## Próximos passos\n{{o que fazer a seguir}}\n";

    [GeneratedRegex(@"^```ya?ml[ \t]*\r?\n([\s\S]*?)^```[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex FenceRe();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex CorRe();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]*$")]
    public static partial Regex IdRe();

    /// <summary>Só um nome de arquivo .md, sem pasta.</summary>
    [GeneratedRegex(@"^[\p{L}\p{N}_][\p{L}\p{N} _.()-]*\.md$")]
    public static partial Regex ArquivoRe();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]*$")]
    private static partial Regex SlugRe();

    private static readonly string[] Cores = ["#2563eb", "#e11d48", "#059669", "#7c3aed", "#d97706", "#0891b2", "#db2777", "#4f46e5"];

    /// <summary>Cor estável para quem não escolheu uma (pelo id).</summary>
    public static string CorPadrao(string id)
    {
        uint h = 0;
        foreach (var c in id) h = unchecked(h * 31 + c);
        return Cores[h % (uint)Cores.Length];
    }

    private static OpError Invalido(string msg) => new(400, msg);

    /// <summary>Separa o prompt (o .md sem o título e sem o bloco yaml) e o yaml.</summary>
    public static (string Yaml, string Prompt) Partes(string md)
    {
        var m = FenceRe().Match(md);
        var semYaml = m.Success ? md[..m.Index] + md[(m.Index + m.Length)..] : md;
        var prompt = Regex.Replace(semYaml, @"^\s*#[^\n]*\n", "").Trim();
        return (m.Success ? m.Groups[1].Value : "", prompt);
    }

    public static JsonObject LerYaml(string yaml)
    {
        JsonNode? v;
        try
        {
            v = Yaml.Ler(yaml);
        }
        catch (YamlException e)
        {
            throw Invalido($"yaml inválido: {e.Message.Split('\n')[0]}");
        }
        return v switch
        {
            null => [],
            JsonObject o => o,
            _ => throw Invalido("yaml inválido: esperava campos (motor, modelo…)."),
        };
    }

    private static string? Texto(JsonNode? v) => v is JsonValue j && j.TryGetValue<string>(out var s) ? s : null;

    private static bool? Bool(JsonNode? v) => v is JsonValue j && j.TryGetValue<bool>(out var b) ? b : null;

    private static double? Numero(JsonNode? v) =>
        v is JsonValue j && j.GetValueKind() == JsonValueKind.Number
            ? j.TryGetValue<long>(out var l) ? l : j.TryGetValue<double>(out var d) ? d : double.Parse(j.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture)
            : null;

    private static string UmDe(string campo, JsonNode? valor, string[] opcoes, string padrao)
    {
        if (valor is null) return padrao;
        var s = Texto(valor);
        if (s == "") return padrao;
        if (s is null || !opcoes.Contains(s)) throw Invalido($"\"{campo}\" deve ser {string.Join(", ", opcoes)}; veio {valor.ToJsonString()}.");
        return s;
    }

    private static void BoolOuNada(JsonObject y, string campo)
    {
        if (y[campo] is not null && Bool(y[campo]) is null) throw Invalido($"\"{campo}\" deve ser true ou false.");
    }

    private static List<string>? LerEscuta(string campo, JsonNode? v)
    {
        if (v is null || Texto(v) == "todas") return null;
        if (v is not JsonArray a || a.Any(x => Texto(x) is not { } s || !IdRe().IsMatch(s)))
            throw Invalido($"\"{campo}\" deve ser \"todas\" ou uma lista de ids (ex.: [n1, n3]).");
        return a.Select(x => Texto(x)!).ToList();
    }

    private static PosFluxo? LerPosFluxo(JsonNode? v)
    {
        if (v is null) return null;
        const string msg = "\"depois\" deve ter o diagrama a iniciar (ex.: depois: {diagrama: plano, modo: tarefa, rodadas: 1}).";
        if (v is not JsonObject o || Texto(o["diagrama"]) is not { } d || !SlugRe().IsMatch(d)) throw Invalido(msg);
        string? modo = o["modo"] is null ? null : UmDe("depois.modo", o["modo"], ["debate", "resposta", "tarefa"], "debate");
        int? rodadas = null;
        if (o["rodadas"] is not null)
        {
            var r = Numero(o["rodadas"]);
            if (r is not { } n || n != Math.Floor(n) || n < 1 || n > 10) throw Invalido("\"depois.rodadas\" deve ser de 1 a 10.");
            rodadas = (int)n;
        }
        var prefixo = Texto(o["prefixo"])?.Trim();
        return new PosFluxo(d, modo, rodadas, string.IsNullOrEmpty(prefixo) ? null : prefixo);
    }

    /// <summary>Lê e valida o .md de uma persona (erros viram 400, como no app original).</summary>
    public static Persona Ler(string id, string md, string nome)
    {
        var (yaml, prompt) = Partes(md);
        var y = LerYaml(yaml);
        var motor = UmDe("motor", y["motor"], Motores, "mcp");
        if (y["modelo"] is not null && string.IsNullOrWhiteSpace(Texto(y["modelo"]))) throw Invalido("\"modelo\" não pode ser vazio.");
        if (y["cor"] is not null && (Texto(y["cor"]) is not { } cor || !CorRe().IsMatch(cor))) throw Invalido("\"cor\" deve ser como \"#e11d48\".");
        var ferramentas = new List<string>();
        if (y["ferramentas"] is not null)
        {
            if (y["ferramentas"] is not JsonArray fa || fa.Any(f => Texto(f) is not { } s || !FerramentasAceitas.Contains(s)))
                throw Invalido($"\"ferramentas\" aceita só: {string.Join(", ", FerramentasAceitas)}.");
            ferramentas = fa.Select(f => Texto(f)!).ToList();
        }
        foreach (var campo in new[] { "inicio", "saida", "humana", "memoria", "curadoria" }) BoolOuNada(y, campo);
        double? prazo = null;
        if (y["prazo"] is not null)
        {
            prazo = Numero(y["prazo"]);
            if (prazo is not > 0) throw Invalido("\"prazo\" deve ser um número de minutos maior que zero.");
        }
        if (y["arquivo"] is not null && (Texto(y["arquivo"]) is not { } arq || !ArquivoRe().IsMatch(arq) || arq.Contains("..")))
            throw Invalido("\"arquivo\" deve ser só o nome de um .md, como \"README.md\".");
        var limite = 4000;
        if (y["limite"] is not null)
        {
            var l = Numero(y["limite"]);
            if (l is not { } n || n != Math.Floor(n) || n < 200) throw Invalido("\"limite\" deve ser um número inteiro de caracteres, a partir de 200.");
            limite = (int)n;
        }
        string? fallback = null;
        if (y["fallback"] is not null)
        {
            fallback = UmDe("fallback", y["fallback"], ["mcp", "ollama", "api"], "api");
        }
        return new Persona
        {
            Id = id,
            Nome = nome,
            Prompt = prompt,
            Motor = motor,
            Modelo = Texto(y["modelo"])?.Trim() is { Length: > 0 } modelo ? modelo : ModeloPadrao[motor],
            Esforco = UmDe("esforco", y["esforco"], Esforcos, "medium"),
            Papel = UmDe("papel", y["papel"], Papeis, "debatedora"),
            Cor = Texto(y["cor"]) ?? CorPadrao(id),
            Ferramentas = ferramentas,
            Inicio = Bool(y["inicio"]) == true,
            Saida = Bool(y["saida"]) == true,
            Arquivo = Texto(y["arquivo"]) ?? "saida.md",
            Humana = Bool(y["humana"]) == true,
            Prazo = prazo,
            AoVencer = UmDe("aoVencer", y["aoVencer"], ["seguir", "encerrar"], "seguir"),
            Fallback = motor == "template" ? fallback ?? "api" : fallback,
            Depois = LerPosFluxo(y["depois"]),
            Memoria = Bool(y["memoria"]) == true,
            Escuta = LerEscuta("escuta", y["escuta"]),
            ResumoPara = LerEscuta("resumoPara", y["resumoPara"]),
            Curadoria = Bool(y["curadoria"]) == true,
            Quando = UmDe("quando", y["quando"], ["rodada", "fala"], "rodada"),
            Limite = limite,
        };
    }

    /// <summary>O .md de uma persona: título, prompt e o yaml só com o que difere do padrão.</summary>
    public static string Escrever(Persona p)
    {
        var o = new List<KeyValuePair<string, JsonNode?>>
        {
            new("motor", p.Motor), new("modelo", p.Modelo), new("esforco", p.Esforco), new("papel", p.Papel), new("cor", p.Cor),
        };
        void Se(bool cond, string k, Func<JsonNode?> v)
        {
            if (cond) o.Add(new(k, v()));
        }
        Se(p.Ferramentas.Count > 0, "ferramentas", () => new JsonArray(p.Ferramentas.Select(f => (JsonNode?)f).ToArray()));
        Se(p.Fallback is not null, "fallback", () => p.Fallback);
        Se(p.Inicio, "inicio", () => true);
        Se(p.Saida, "saida", () => true);
        Se(p.Arquivo != "saida.md", "arquivo", () => p.Arquivo);
        Se(p.Humana, "humana", () => true);
        Se(p.Prazo is not null, "prazo", () => p.Prazo);
        Se(p.AoVencer != "seguir", "aoVencer", () => p.AoVencer);
        Se(p.Depois is not null, "depois", () => JsonSerializer.SerializeToNode(p.Depois, new JsonSerializerOptions(Json.Opcoes) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }));
        Se(p.Memoria, "memoria", () => true);
        Se(p.Escuta is not null, "escuta", () => new JsonArray(p.Escuta!.Select(x => (JsonNode?)x).ToArray()));
        Se(p.Quando != "rodada", "quando", () => p.Quando);
        Se(p.Limite != 4000, "limite", () => p.Limite);
        Se(p.ResumoPara is not null, "resumoPara", () => new JsonArray(p.ResumoPara!.Select(x => (JsonNode?)x).ToArray()));
        Se(p.Curadoria, "curadoria", () => true);
        return $"# {p.Nome}\n\n{p.Prompt.Trim()}\n\n```yaml\n{Yaml.Escrever(o)}```\n";
    }

    public static Persona Padrao(string id, string nome) =>
        Ler(id, $"Você é {nome}. Participe da conversa com a sua visão, de forma direta.", nome);
}
