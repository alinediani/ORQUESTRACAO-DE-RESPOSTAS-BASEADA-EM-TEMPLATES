using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;
using Orquestracao.Core.Conversas;

namespace Orquestracao.Motores;

/// <summary>
/// Motor API: a persona fala pelo Claude, com a credencial da Anthropic (ANTHROPIC_API_KEY ou um perfil
/// do <c>ant auth login</c>). O texto chega aos poucos (streaming), o prompt da persona fica em cache e
/// o custo de cada vez é calculado pelo uso de tokens.
/// </summary>
public sealed class MotorApi(Func<bool>? credencial = null, Func<AnthropicClient>? cliente = null) : IMotor
{
    /// <summary>Dólares por milhão de tokens: entrada, saída e leitura de cache.</summary>
    private static readonly Dictionary<string, (double Entrada, double Saida, double Cache)> Precos = new()
    {
        ["claude-fable-5-1"] = (10, 50, 1),
        ["claude-opus-5-5"] = (4, 20, 0.2),
        ["claude-opus-5"] = (5, 25, 0.5),
        ["claude-sonnet-5-5"] = (2, 10, 0.2),
        ["claude-haiku-5-5"] = (0.1, 0.5, 0.01),
    };

    /// <summary>Modelos oferecidos no painel da persona.</summary>
    public static readonly string[] Modelos = ["claude-opus-5-5", "claude-sonnet-5-5", "claude-haiku-5-5", "claude-fable-5-1"];

    private const string SemChave = "sem credencial da Anthropic: defina ANTHROPIC_API_KEY (ou rode `ant auth login`) e reinicie o servidor";

    private readonly Func<bool> _credencial = credencial ?? TemCredencial;
    private AnthropicClient? _cliente;

    public string Nome => "api";

    /// <summary>Custo em dólares; escrita de cache custa 1,25× a entrada.</summary>
    public static double CustoDe(string modelo, long entrada, long saida, long cacheLido = 0, long cacheEscrito = 0)
    {
        if (!Precos.TryGetValue(modelo, out var p)) return 0;
        var e = entrada + cacheEscrito * 1.25;
        return (e * p.Entrada + saida * p.Saida + cacheLido * p.Cache) / 1_000_000;
    }

    /// <summary>Há credencial? A chave, o token, um perfil escolhido ou um perfil do <c>ant auth login</c> no disco.</summary>
    public static bool TemCredencial()
    {
        foreach (var v in new[] { "ANTHROPIC_API_KEY", "ANTHROPIC_AUTH_TOKEN", "ANTHROPIC_PROFILE" })
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(v))) return true;
        var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "anthropic");
        return Directory.Exists(pasta) && Directory.EnumerateFileSystemEntries(pasta).Any();
    }

    public Task<Disponibilidade> Disponivel(CancellationToken ct = default) =>
        Task.FromResult(_credencial() ? new Disponibilidade(true, "credencial da Anthropic encontrada") : new Disponibilidade(false, SemChave));

    private static ApiEnum<string, Effort> EsforcoDe(string esforco) => esforco switch
    {
        "low" => Effort.Low,
        "high" => Effort.High,
        "xhigh" => Effort.Xhigh,
        "max" => Effort.Max,
        _ => Effort.Medium,
    };

    /// <summary>Os parâmetros da chamada (separado para os testes conferirem sem rede).</summary>
    public static MessageCreateParams Parametros(Pedido p)
    {
        var web = p.Persona.Ferramentas.Contains("web_search");
        return new MessageCreateParams
        {
            Model = p.Persona.Modelo,
            MaxTokens = 64000,
            // O prompt da persona é estável entre as vezes: fica em cache.
            System = new List<TextBlockParam> { new() { Text = p.System, CacheControl = new CacheControlEphemeral() } },
            Messages = [new UserMessageParam { Content = p.Contexto }],
            OutputConfig = new OutputConfig { Effort = EsforcoDe(p.Persona.Esforco) },
            Tools = web ? [new ToolUnion(new WebSearchTool20260209 { MaxUses = 5 })] : null,
        };
    }

    public async Task<Resposta> Atender(Pedido pedido, Action<string> delta, CancellationToken ct)
    {
        if (!_credencial()) throw new InvalidOperationException(SemChave);
        _cliente ??= cliente?.Invoke() ?? new AnthropicClient();
        var texto = new System.Text.StringBuilder();
        long entrada = 0, saida = 0, cacheLido = 0, cacheEscrito = 0;
        string? parada = null;
        await foreach (var ev in _cliente.Messages.CreateStreaming(Parametros(pedido), ct))
        {
            if (ev.TryPickStart(out var inicio))
            {
                var u = inicio.Message.Usage;
                entrada = u.InputTokens;
                cacheLido = u.CacheReadInputTokens ?? 0;
                cacheEscrito = u.CacheCreationInputTokens ?? 0;
            }
            else if (ev.TryPickContentBlockDelta(out var bloco) && bloco.Delta.TryPickText(out var t))
            {
                texto.Append(t.Text);
                delta(t.Text);
            }
            else if (ev.TryPickDelta(out var fim))
            {
                parada = fim.Delta.StopReason?.ToString();
                saida = fim.Usage.OutputTokens;
            }
        }
        if (parada == "refusal") throw new InvalidOperationException("o modelo recusou responder");
        if (parada == "max_tokens") texto.Append("\n\n(resposta cortada: atingiu o limite de tamanho)");
        return new Resposta
        {
            Texto = texto.ToString(),
            Uso = new Uso { Entrada = entrada, Saida = saida, Cache = cacheLido, Custo = CustoDe(pedido.Persona.Modelo, entrada, saida, cacheLido, cacheEscrito) },
        };
    }
}
