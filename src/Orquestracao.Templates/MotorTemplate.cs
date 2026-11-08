using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Orquestracao.Core.Conversas;

namespace Orquestracao.Templates;

/// <summary>
/// O orquestrador como motor de persona (<c>motor: template</c>): ele responde às perguntas da conversa pelo
/// template quando a estrutura já é conhecida e, quando não, pelo motor de fallback da persona
/// (<c>fallback: api | ollama | mcp</c>), de onde extrai o template. O feedback vem da persona humana:
/// Aprovar é bom, Pedir mudança é ruim (e a resposta seguinte volta ao LLM, até o limite de tentativas).
/// </summary>
public sealed class MotorTemplate(Orquestrador orquestrador, Func<string, IMotor> motorPorNome) : IMotor
{
    public string Nome => "template";
    public Orquestrador Orquestrador => orquestrador;

    /// <summary>A última resposta de cada persona em cada conversa (para o feedback e as tentativas).</summary>
    private sealed record Ultima(string Chave, int Tentativas, bool Ruim);

    private readonly ConcurrentDictionary<(string Conversa, string Persona), Ultima> _ultimas = new();

    public Task<Disponibilidade> Disponivel(CancellationToken ct = default) =>
        Task.FromResult(new Disponibilidade(true, $"{orquestrador.Store.Listar().Count} templates · {orquestrador.Fonte.Entidades.Count} entidades na fonte"));

    /// <summary>A pergunta do turno: a do usuário no chat (quando ela foi chamada com @) ou o assunto da conversa.</summary>
    public static string PerguntaDe(Pedido p)
    {
        if (!p.RespondendoA.Contains("usuario")) return p.Assunto;
        var i = p.Contexto.LastIndexOf("Usuário: ", StringComparison.Ordinal);
        if (i < 0) return p.Assunto;
        var resto = p.Contexto[(i + "Usuário: ".Length)..];
        var fim = resto.IndexOf("\n\n", StringComparison.Ordinal);
        return (fim < 0 ? resto : resto[..fim]).Trim();
    }

    /// <summary>A persona como o motor de fallback a vê: o mesmo prompt, o motor e um modelo que ele aceite.</summary>
    public static Persona PersonaDeFallback(Persona p)
    {
        var motor = p.Fallback ?? "api";
        var modelo = motor == "ollama" && p.Modelo.StartsWith("claude", StringComparison.Ordinal) ? Personas.ModeloPadrao["ollama"] : p.Modelo;
        return p with { Motor = motor, Modelo = modelo };
    }

    public async Task<Resposta> Atender(Pedido pedido, Action<string> delta, CancellationToken ct)
    {
        var fallback = PersonaDeFallback(pedido.Persona);
        var motor = motorPorNome(fallback.Motor);
        // Síntese, tarefa, consolidação…: não são perguntas factuais; o fallback responde direto.
        if (pedido.Tipo != TipoPedido.Fala)
            return await motor.Atender(pedido with { Persona = fallback }, delta, ct);

        var chave = (pedido.Conversa, pedido.Persona.Id);
        var ultima = _ultimas.GetValueOrDefault(chave);
        var forcar = ultima?.Ruim == true;
        var tentativas = forcar ? ultima!.Tentativas + 1 : 0;
        if (forcar && tentativas > orquestrador.Configuracao.LimiteTentativas)
        {
            _ultimas[chave] = ultima! with { Ruim = false };
            return new Resposta
            {
                Texto = $"Não consegui uma resposta melhor depois de {orquestrador.Configuracao.LimiteTentativas} tentativas.",
                Detalhe = new JsonObject { ["caminho"] = "limite", ["chave"] = ultima.Chave, ["tentativas"] = ultima.Tentativas },
            };
        }

        var llm = new LlmPorMotor(motor, fallback, pedido);
        var r = await orquestrador.Responder(PerguntaDe(pedido), llm, ct, forcar, pedido.Persona.Prompt);
        _ultimas[chave] = new Ultima(r.Chave, tentativas, false);
        delta(r.Texto);
        return new Resposta
        {
            Texto = r.Texto,
            Uso = new Uso { Entrada = r.TokensEntrada, Saida = r.TokensSaida, Custo = r.Custo },
            Detalhe = Detalhe(r, tentativas),
        };
    }

    public static JsonObject Detalhe(ResultadoOrquestrador r, int tentativas = 0) => new()
    {
        ["caminho"] = r.Caminho,
        ["chave"] = r.Chave,
        ["entidade"] = r.Entidade,
        ["template"] = r.Template,
        ["peso"] = r.Peso,
        ["templateSalvo"] = r.TemplateSalvo,
        ["motivo"] = r.Motivo,
        ["chamadasGeracao"] = r.ChamadasGeracao,
        ["chamadasParser"] = r.ChamadasParser,
        ["latenciaMs"] = Math.Round(r.LatenciaMs, 1),
        ["tentativas"] = tentativas,
    };

    /// <summary>O feedback da persona humana sobre a última resposta desta persona na conversa (R05).</summary>
    public void RegistrarFeedback(string conversa, string persona, bool bom)
    {
        if (!_ultimas.TryGetValue((conversa, persona), out var u)) return;
        orquestrador.Feedback(u.Chave, bom);
        _ultimas[(conversa, persona)] = u with { Ruim = !bom };
    }
}
