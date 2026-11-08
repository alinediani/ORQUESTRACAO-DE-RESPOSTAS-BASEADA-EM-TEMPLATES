using Orquestracao.Core.Conversas;

namespace Orquestracao.Templates;

public sealed record RespostaLlm(string Texto, long Entrada = 0, long Saida = 0, double Custo = 0);

/// <summary>O LLM atrás de uma interface (como pede o TCC): nos testes, um simulado que conta as chamadas.</summary>
public interface ILlm
{
    Task<RespostaLlm> Completar(string system, string usuario, CancellationToken ct = default);
}

/// <summary>Qualquer motor de persona (API da Anthropic, Ollama, MCP) usado como LLM do orquestrador.</summary>
public sealed class LlmPorMotor(IMotor motor, Persona persona, Pedido? origem = null) : ILlm
{
    private int _seq;

    public async Task<RespostaLlm> Completar(string system, string usuario, CancellationToken ct = default)
    {
        var pedido = new Pedido
        {
            Id = $"{origem?.Id ?? "orq"}-llm{Interlocked.Increment(ref _seq)}",
            Conversa = origem?.Conversa ?? "orquestrador",
            Diagrama = origem?.Diagrama ?? "orquestrador",
            Tipo = origem?.Tipo ?? TipoPedido.Fala,
            Persona = persona,
            System = system,
            Contexto = usuario,
            Assunto = origem?.Assunto ?? usuario,
            RespondendoA = origem?.RespondendoA ?? [],
        };
        var r = await motor.Atender(pedido, _ => { }, ct);
        return new RespostaLlm(r.Texto, r.Uso?.Entrada ?? 0, r.Uso?.Saida ?? 0, r.Uso?.Custo ?? 0);
    }
}

/// <summary>Um LLM simulado: responde por uma função e conta as chamadas (R07, testes e experimento sem custo).</summary>
public sealed class LlmSimulado(Func<string, string, string> responder, long tokensEntrada = 50, long tokensSaida = 20, double custo = 0) : ILlm
{
    private int _chamadas;
    public int Chamadas => _chamadas;

    public Task<RespostaLlm> Completar(string system, string usuario, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _chamadas);
        return Task.FromResult(new RespostaLlm(responder(system, usuario), tokensEntrada, tokensSaida, custo));
    }
}

/// <summary>Cria o LLM só na primeira chamada (o motor pode depender de serviços que ainda não existem).</summary>
public sealed class LlmPreguicoso(Func<ILlm> criar) : ILlm
{
    private readonly Lazy<ILlm> _llm = new(criar);

    public Task<RespostaLlm> Completar(string system, string usuario, CancellationToken ct = default) => _llm.Value.Completar(system, usuario, ct);
}
