using System.Text.Json;
using System.Text.Json.Nodes;
using Orquestracao.Core.Diagramas;
using Orquestracao.Templates;

namespace Orquestracao.Web;

/// <summary>
/// A visualização do fluxo (F6): cada etapa do orquestrador vira um pulso na seta correspondente do diagrama
/// "Orquestrador por template" (a Figura 1 do TCC, criada sozinha na primeira vez) e um evento no SSE do painel
/// (<c>/api/orquestrador/events</c>), junto com o resultado de cada pergunta e cada feedback.
/// </summary>
public sealed class OrquestradorAoVivo
{
    public const string Slug = "orquestrador-por-template";
    public const string Nome = "Orquestrador por template";

    public Assinantes Ouvintes { get; } = new();
    private readonly Workspace _ws;

    /// <summary>Os blocos da Figura 1: id, texto e posição no canvas.</summary>
    public static readonly (string Id, string Texto, double X, double Y)[] Blocos =
    [
        ("ui", "UI · pergunta", 420, 60),
        ("parser", "Parser · estrutura e slots", 420, 170),
        ("motor", "Motor · consulta a chave", 420, 280),
        ("busca", "Busca o template", 640, 390),
        ("fonte", "Fonte externa · base de fatos", 640, 500),
        ("adapta", "Adapta · preenche os slots", 640, 610),
        ("entrega", "Entrega · resposta ao usuário", 640, 720),
        ("feedback", "Feedback · ajusta o peso", 640, 830),
        ("fim", "Fim", 880, 940),
        ("llm", "LLM padrão", 200, 830),
        ("extrai", "Extrai o template", 200, 940),
        ("salva", "Salva no chave-valor · peso 0,5", 420, 1050),
    ];

    /// <summary>As setas: de, para e o texto (as mesmas etapas que o orquestrador emite).</summary>
    public static readonly (string De, string Para, string Texto)[] Setas =
    [
        ("ui", "parser", ""), ("parser", "motor", ""), ("motor", "busca", "chave existe"), ("busca", "fonte", ""),
        ("fonte", "adapta", ""), ("adapta", "entrega", ""), ("entrega", "feedback", ""), ("feedback", "fim", "bom"),
        ("motor", "llm", "chave nova"), ("feedback", "llm", "ruim: até o limite"), ("llm", "extrai", ""),
        ("extrai", "salva", ""), ("salva", "entrega", "entrega"), ("extrai", "entrega", "não reutilizável"),
    ];

    public static string IdDaSeta(string de, string para) => $"{de}-{para}";

    /// <param name="criarDiagrama">Cria o diagrama da Figura 1 na partida (config Orquestrador:DiagramaDaFigura).</param>
    public OrquestradorAoVivo(Workspace ws, Orquestrador orquestrador, bool criarDiagrama = true)
    {
        _ws = ws;
        if (criarDiagrama) GarantirDiagrama();
        orquestrador.Etapa += Etapa;
    }

    /// <summary>Cria o diagrama da Figura 1, se ainda não existe.</summary>
    public void GarantirDiagrama()
    {
        if (_ws.Existe(Slug)) return;
        var d = new Diagram
        {
            Nodes = Blocos.Select(b => new DiagramNode { Id = b.Id, Text = b.Texto, X = b.X, Y = b.Y }).ToList(),
            Edges = Setas.Select(s => new DiagramEdge { Id = IdDaSeta(s.De, s.Para), From = s.De, To = s.Para, Text = s.Texto }).ToList(),
        };
        _ws.Criar(Nome, d);
    }

    private void Etapa(EtapaFluxo e)
    {
        Publicar(new { type = "etapa", de = e.De, para = e.Para, rotulo = e.Rotulo });
        if (!_ws.Existe(Slug)) return;
        var id = IdDaSeta(e.De, e.Para);
        var diagrama = _ws.Diagrama(Slug);
        if (!diagrama.Diagrama.Edges.Exists(x => x.Id == id)) return;
        var pulso = new JsonObject { ["type"] = "edge.pulse", ["id"] = id, ["source"] = "orquestrador" };
        if (!string.IsNullOrEmpty(e.Rotulo)) pulso["label"] = e.Rotulo;
        diagrama.Transmitir(pulso);
    }

    public void Publicar(object evento) => Ouvintes.Publicar(JsonSerializer.Serialize(evento, Json.Opcoes));
}
