using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Orquestracao.Core.Diagramas;

namespace Orquestracao.Core.Conversas;

public sealed record Mensagem
{
    public required string Id { get; init; }
    /// <summary>usuario · fala · sintese · tarefa · resultado · saida · erro · sistema</summary>
    public required string Tipo { get; init; }
    public required string Nome { get; init; }
    public required string Texto { get; init; }
    public required string Em { get; init; }
    public string? Persona { get; init; }
    public string? Turno { get; init; }
    public string? Motor { get; init; }
    public string? Modelo { get; init; }
    public List<string>? RespondendoA { get; init; }
    /// <summary>Na tarefa: a mensagem que originou esta delegação.</summary>
    public string? Pai { get; init; }
    /// <summary>Na saída: o nome do .md gerado.</summary>
    public string? Arquivo { get; init; }
    public Uso? Uso { get; init; }
    /// <summary>O que o motor contou sobre a resposta (ex.: o orquestrador por template: chave, template, peso).</summary>
    public JsonObject? Detalhe { get; init; }
}

/// <summary>A vez de uma persona humana: a conversa espera a resposta do usuário.</summary>
public sealed record Espera
{
    public required string Turno { get; init; }
    public required string Persona { get; init; }
    public required string Nome { get; init; }
    public required string Tipo { get; init; }
    public required string Contexto { get; init; }
    /// <summary>No debate: onde a rodada parou (para retomar depois de reiniciar o servidor).</summary>
    public Posicao? Posicao { get; init; }
    public required string Em { get; init; }
}

public sealed record Posicao(int Rodada, int Indice);

public sealed record RespostaHumana(string Acao, string? Texto = null, bool Venceu = false);

public sealed record Pendente(string Turno, string Persona, string Nome, string Tipo, string Motor);

public sealed class Conversa
{
    public required string Id { get; init; }
    public required string Diagrama { get; init; }
    public required string Assunto { get; init; }
    /// <summary>debate · resposta · tarefa</summary>
    public required string Modo { get; init; }
    public int Rodadas { get; init; }
    /// <summary>rodando · aguardando · terminada · parada</summary>
    public string Status { get; set; } = "rodando";
    public List<Mensagem> Mensagens { get; init; } = [];
    /// <summary>A síntese ou a entrega final, quando houver.</summary>
    public string? Resposta { get; set; }
    public Espera? Espera { get; set; }
    /// <summary>Turnos em andamento (só na conversa viva; não é salvo).</summary>
    public List<Pendente>? Pendentes { get; set; }
    public required string CriadaEm { get; init; }
    public string AtualizadaEm { get; set; } = "";

    /// <summary>JSON da conversa: sem campos nulos, como o app original.</summary>
    public static readonly JsonSerializerOptions JsonOpcoes = new(Json.Opcoes) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public Conversa Copia() => JsonSerializer.Deserialize<Conversa>(JsonSerializer.Serialize(this, JsonOpcoes), JsonOpcoes)!;
}

public sealed record ResumoConversa(string Id, string Assunto, string Modo, string Status, int Mensagens, string CriadaEm, string AtualizadaEm);
