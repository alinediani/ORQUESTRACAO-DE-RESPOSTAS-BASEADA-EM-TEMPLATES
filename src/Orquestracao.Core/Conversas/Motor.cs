using System.Text.Json.Serialization;

namespace Orquestracao.Core.Conversas;

/// <summary>O que pede o turno: uma fala, a síntese, uma tarefa, a execução de uma delegação…</summary>
public static class TipoPedido
{
    public const string Fala = "fala";
    public const string Sintese = "sintese";
    public const string Tarefa = "tarefa";
    public const string Execucao = "execucao";
    public const string Consolidar = "consolidar";
    public const string Saida = "saida";
}

/// <summary>Um turno entregue a um motor: quem fala, o system prompt e o contexto da conversa.</summary>
public sealed record Pedido
{
    /// <summary>Id do turno: é por ele que o MCP responde.</summary>
    public required string Id { get; init; }
    public required string Conversa { get; init; }
    public required string Diagrama { get; init; }
    public required string Tipo { get; init; }
    public required Persona Persona { get; init; }
    public required string System { get; init; }
    public required string Contexto { get; init; }
    /// <summary>A pergunta original da conversa (o orquestrador por template extrai a estrutura dela).</summary>
    public string Assunto { get; init; } = "";
    /// <summary>Ids de quem esta fala responde ("usuario" quando foi chamada no chat).</summary>
    public List<string> RespondendoA { get; init; } = [];
    /// <summary>Para quem pode delegar (só nas tarefas).</summary>
    public List<Pessoa> Delegaveis { get; init; } = [];
}

public sealed record Uso
{
    public long Entrada { get; init; }
    public long Saida { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Cache { get; init; }
    /// <summary>Em dólares (zero no Ollama, no MCP e quando o template respondeu sem LLM).</summary>
    public double Custo { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TokensPorSegundo { get; init; }
}

public sealed record Resposta
{
    public required string Texto { get; init; }
    /// <summary>Delegações estruturadas (o MCP manda assim); as linhas DELEGAR do texto também valem.</summary>
    public List<Delegacao>? Delegacoes { get; init; }
    public Uso? Uso { get; init; }
    /// <summary>Informação extra do motor, gravada na mensagem (o orquestrador por template conta aqui de onde veio a resposta).</summary>
    public System.Text.Json.Nodes.JsonObject? Detalhe { get; init; }
}

public sealed record Disponibilidade(bool Ok, string Detalhe);

/// <summary>Quem dá voz a uma persona: MCP (Claude Code), Ollama, a API da Anthropic ou o orquestrador por template.</summary>
public interface IMotor
{
    string Nome { get; }

    Task<Disponibilidade> Disponivel(CancellationToken ct = default);

    /// <summary>Atende o turno; <paramref name="delta"/> recebe o texto aos poucos (streaming), quando houver.</summary>
    Task<Resposta> Atender(Pedido pedido, Action<string> delta, CancellationToken ct);
}

/// <summary>Os motores disponíveis, pelo nome (o yaml da persona escolhe um).</summary>
public sealed class RegistroDeMotores
{
    private readonly Dictionary<string, IMotor> _porNome = [];

    /// <summary>Com dois motores de mesmo nome, vale o último (os testes trocam um motor real por um falso).</summary>
    public RegistroDeMotores(IEnumerable<IMotor> motores)
    {
        foreach (var m in motores) _porNome[m.Nome] = m;
    }

    public IMotor this[string nome] =>
        _porNome.TryGetValue(nome, out var m) ? m : throw new InvalidOperationException($"motor \"{nome}\" não está configurado");

    public bool Tem(string nome) => _porNome.ContainsKey(nome);

    public IEnumerable<IMotor> Todos => _porNome.Values;
}
