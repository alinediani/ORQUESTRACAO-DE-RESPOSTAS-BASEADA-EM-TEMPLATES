using System.Threading.Channels;

namespace Orquestracao.Core.Diagramas;

/// <summary>
/// Quem está ouvindo os eventos (as abas abertas, pelo SSE). Cada assinatura é um canal de mensagens
/// já serializadas em JSON; quem lê decide como entregá-las (no Web, como <c>data: …</c>).
/// </summary>
public sealed class Assinantes
{
    private readonly Lock _trava = new();
    private readonly List<Channel<string>> _canais = [];

    /// <summary>Uma assinatura nova; <paramref name="primeira"/> (o snapshot) é a primeira mensagem.</summary>
    public Assinatura Assinar(string? primeira = null)
    {
        var canal = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        lock (_trava)
        {
            // O snapshot entra antes de qualquer evento novo: dentro da trava, nada passa na frente.
            if (primeira is not null) canal.Writer.TryWrite(primeira);
            _canais.Add(canal);
        }
        return new Assinatura(canal.Reader, () =>
        {
            lock (_trava) _canais.Remove(canal);
            canal.Writer.TryComplete();
        });
    }

    public void Publicar(string json)
    {
        lock (_trava)
            foreach (var c in _canais) c.Writer.TryWrite(json);
    }

    /// <summary>Encerra todas as assinaturas (o diagrama foi renomeado, apagado ou o servidor parou).</summary>
    public void Fechar()
    {
        lock (_trava)
        {
            foreach (var c in _canais) c.Writer.TryComplete();
            _canais.Clear();
        }
    }

    public int Quantos
    {
        get
        {
            lock (_trava) return _canais.Count;
        }
    }
}

public sealed class Assinatura(ChannelReader<string> mensagens, Action cancelar) : IDisposable
{
    public ChannelReader<string> Mensagens { get; } = mensagens;

    public void Dispose() => cancelar();
}
