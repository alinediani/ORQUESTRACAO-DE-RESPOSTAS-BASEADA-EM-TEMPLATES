using System.Collections.Concurrent;
using Orquestracao.Core.Conversas;

namespace Orquestracao.Motores;

/// <summary>
/// Motor MCP: quem dá voz à persona é um cliente MCP, como o Claude Code, conectado ao /mcp. Cada vez
/// desta persona vira um turno pendente; o cliente busca os pendentes (<c>proxima_vez</c>), escreve como a
/// persona e devolve (<c>responder</c>). Turnos de vários diagramas e conversas convivem, e delegações em
/// paralelo aparecem juntas, para o cliente atender com subagentes.
/// </summary>
public sealed class MotorMcp : IMotor
{
    /// <summary>Sem contato do cliente MCP por este tempo, ele conta como desconectado.</summary>
    private static readonly TimeSpan Conectado = TimeSpan.FromMinutes(5);

    private sealed record Turno(Pedido Pedido, TaskCompletionSource<Resposta> Resultado);

    private readonly ConcurrentDictionary<string, Turno> _pendentes = new();
    private readonly Lock _trava = new();
    private TaskCompletionSource _novo = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DateTime _ultimoContato = DateTime.MinValue;

    public string Nome => "mcp";

    /// <summary>O cliente MCP fez contato (para o status "conectado").</summary>
    public void Tocar() => _ultimoContato = DateTime.UtcNow;

    public bool EstaConectado => DateTime.UtcNow - _ultimoContato < Conectado;

    public Task<Disponibilidade> Disponivel(CancellationToken ct = default) => Task.FromResult(EstaConectado
        ? new Disponibilidade(true, "Claude Code conectado ao /mcp")
        : new Disponibilidade(false, "nenhum Claude Code conectado ao /mcp: abra o Claude Code na pasta do projeto (o .mcp.json já aponta para cá) e peça para começar a conversa"));

    public Task<Resposta> Atender(Pedido pedido, Action<string> delta, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<Resposta>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (ct.IsCancellationRequested) return Task.FromException<Resposta>(new InvalidOperationException("conversa parada"));
        _pendentes[pedido.Id] = new Turno(pedido, tcs);
        ct.Register(() =>
        {
            if (_pendentes.TryRemove(pedido.Id, out _)) tcs.TrySetException(new InvalidOperationException("conversa parada"));
        });
        Acordar();
        return tcs.Task;
    }

    private void Acordar()
    {
        TaskCompletionSource velho;
        lock (_trava)
        {
            velho = _novo;
            _novo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        velho.TrySetResult();
    }

    /// <summary>Os turnos pendentes (de um diagrama, ou de todos), na ordem em que chegaram.</summary>
    public List<Pedido> Pendentes(string? diagrama = null) =>
        _pendentes.Values.Select(t => t.Pedido).Where(p => diagrama is null || p.Diagrama == diagrama).ToList();

    /// <summary>Os turnos pendentes; sem nenhum, espera até <paramref name="tempo"/> por um novo.</summary>
    public async Task<List<Pedido>> Aguardar(string? diagrama, TimeSpan tempo, CancellationToken ct = default)
    {
        var fim = DateTime.UtcNow + tempo;
        while (true)
        {
            Task sinal;
            lock (_trava) sinal = _novo.Task;
            var lista = Pendentes(diagrama);
            var resta = fim - DateTime.UtcNow;
            if (lista.Count > 0 || resta <= TimeSpan.Zero) return lista;
            await Task.WhenAny(sinal, Task.Delay(resta, ct));
            // Deixa a rodada terminar de criar os turnos (delegações paralelas chegam juntas).
            await Task.Delay(20, ct);
        }
    }

    public void Responder(string turno, string texto, List<Delegacao>? delegacoes = null)
    {
        if (!_pendentes.TryRemove(turno, out var t))
            throw new InvalidOperationException($"O turno \"{turno}\" não está pendente (já respondido, parado ou inexistente). Chame proxima_vez para ver os pendentes.");
        t.Resultado.TrySetResult(new Resposta { Texto = texto, Delegacoes = delegacoes });
    }
}
