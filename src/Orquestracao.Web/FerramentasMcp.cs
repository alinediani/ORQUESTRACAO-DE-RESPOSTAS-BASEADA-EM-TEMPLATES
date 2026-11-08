using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Orquestracao.Core.Conversas;
using Orquestracao.Core.Diagramas;
using Orquestracao.Motores;

namespace Orquestracao.Web;

/// <summary>
/// O servidor MCP em /mcp: é por ele que o Claude Code (ou outro cliente MCP) dá voz às personas de
/// motor "mcp". O cliente lista os diagramas, começa a conversa, pega cada vez (<c>proxima_vez</c>) e
/// responde como a persona (<c>responder</c>). Personas de outros motores falam sozinhas.
/// </summary>
[McpServerToolType]
public sealed class FerramentasMcp(EspacoIa ia, MotorMcp mcp, Orquestracao.Templates.Orquestrador orquestrador, RegistroDeMotores motores, OrquestradorAoVivo aoVivo)
{
    private static readonly JsonSerializerOptions Saida = new(Conversa.JsonOpcoes) { WriteIndented = true };

    private static string Json(object v) => JsonSerializer.Serialize(v, Saida);

    /// <summary>Subagente sugerido para o Claude Code, pelo modelo da persona.</summary>
    private static string Subagente(string modelo) =>
        modelo.Contains("haiku") ? "haiku" : modelo.Contains("sonnet") ? "sonnet" : modelo.Contains("fable") ? "fable" : "opus";

    private static readonly Dictionary<string, string> Como = new()
    {
        [TipoPedido.Fala] = "Escreva a fala desta persona (só ela, em primeira pessoa, sem o nome na frente) e chame responder(turno, texto).",
        [TipoPedido.Sintese] = "Escreva a síntese final pedida no contexto (a resposta polida) e chame responder(turno, texto).",
        [TipoPedido.Tarefa] = "Decida como fazer a tarefa. Para delegar, chame responder com `delegacoes: [{para, tarefa}]` (só para as delegáveis) e um texto curto com o plano; as delegadas viram novos turnos (em paralelo: dá para usar um subagente por turno) e depois vem um turno \"consolidar\". Se fizer sozinha, responda com o resultado direto, sem delegações.",
        [TipoPedido.Execucao] = "Faça a tarefa delegada e chame responder(turno, texto) com o resultado completo.",
        [TipoPedido.Consolidar] = "Junte os resultados do contexto numa entrega final única e chame responder(turno, texto).",
        [TipoPedido.Saida] = "Preencha o template de saída do contexto com o resultado da conversa (mantenha os títulos) e chame responder(turno, texto) só com o markdown preenchido.",
    };

    private static object ParaCliente(Pedido p) => new
    {
        turno = p.Id,
        diagrama = p.Diagrama,
        conversa = p.Conversa,
        tipo = p.Tipo,
        persona = new { id = p.Persona.Id, nome = p.Persona.Nome, papel = p.Persona.Papel, modelo = p.Persona.Modelo, subagente_sugerido = Subagente(p.Persona.Modelo) },
        system = p.System,
        contexto = p.Contexto,
        delegaveis = p.Delegaveis.Select(d => d.Nome),
        como_responder = Como.GetValueOrDefault(p.Tipo, "Responda com responder(turno, texto)."),
    };

    private void Abrir(string diagrama)
    {
        mcp.Tocar();
        if (!ia.Ws.Existe(diagrama)) throw new OpError(404, $"Diagrama \"{diagrama}\" não encontrado.");
    }

    [McpServerTool(Name = "listar_diagramas"), Description("Lista os diagramas (cada um é um grupo de personas de IA).")]
    public string ListarDiagramas()
    {
        mcp.Tocar();
        return Json(ia.Ws.Listar().Select(d => new { slug = d.Slug, nome = d.Name, personas = d.Nodes }));
    }

    [McpServerTool(Name = "ver_personas"), Description("As personas de um diagrama (nome, motor, modelo, papel) e as setas (quem responde a quem / quem delega para quem).")]
    public string VerPersonas([Description("slug do diagrama")] string diagrama)
    {
        Abrir(diagrama);
        var d = ia.Ws.Diagrama(diagrama).Diagrama;
        string Nome(string id) => d.Nodes.Find(n => n.Id == id)?.Text ?? id;
        return Json(new
        {
            personas = ia.PersonasDoDiagrama(diagrama).Select(p => new
            {
                id = p.Id, nome = p.Nome, motor = p.Motor, modelo = p.Modelo, papel = p.Papel, inicio = p.Inicio, saida = p.Saida,
                arquivo = p.Saida ? p.Arquivo : null, humana = p.Humana, fallback = p.Fallback,
            }),
            setas = d.Edges.Where(e => e.From != e.To).Select(e => $"{Nome(e.From)} → {Nome(e.To)}"),
        });
    }

    [McpServerTool(Name = "iniciar_conversa"), Description("Começa uma conversa entre as personas do diagrama. modo: \"debate\" (rodadas pelas setas), \"resposta\" (debate + síntese polida no fim) ou \"tarefa\" (a orquestradora delega pelas setas e consolida). Depois, chame proxima_vez para dar voz às personas de motor mcp.")]
    public string IniciarConversa(string diagrama, [Description("o assunto, a pergunta ou a tarefa")] string assunto, string? modo = null, int? rodadas = null)
    {
        Abrir(diagrama);
        var c = ia.ConversasDe(diagrama).Iniciar(assunto, modo, rodadas);
        var personas = ia.PersonasDoDiagrama(diagrama);
        var d = ia.Ws.Diagrama(diagrama).Diagrama;
        var rodada = Turnos.SemNos(d, personas.Where(p => p.Saida).Select(p => p.Id).ToList());
        return Json(new
        {
            conversa = c.Id, modo = c.Modo, rodadas = c.Rodadas,
            ordem = Turnos.OrdemDaRodada(rodada, personas.Where(p => p.Inicio).Select(p => p.Id)).Select(id => d.Nodes.Find(n => n.Id == id)!.Text),
            saidas = personas.Where(p => p.Saida).Select(p => $"{p.Nome} → {p.Arquivo}"),
        });
    }

    [McpServerTool(Name = "proxima_vez"), Description("Os turnos pendentes das personas de motor mcp (de um diagrama, ou de todos). Sem nenhum, espera até `aguardar_s` segundos por um novo (por exemplo, quando o usuário escreve no chat do navegador). Responda cada turno com responder.")]
    public async Task<string> ProximaVez(string? diagrama = null, double? aguardar_s = null, CancellationToken ct = default)
    {
        mcp.Tocar();
        var turnos = await mcp.Aguardar(diagrama, TimeSpan.FromSeconds(Math.Clamp(aguardar_s ?? 25, 0, 120)), ct);
        return Json(new { turnos = turnos.Select(ParaCliente) });
    }

    public sealed record DelegacaoMcp([property: Description("nome da persona delegável")] string Para, string Tarefa);

    [McpServerTool(Name = "responder"), Description("Entrega a fala (ou o resultado) de um turno pendente. Em turnos de tarefa, `delegacoes` divide o trabalho entre as personas delegáveis.")]
    public string Responder(string turno, string texto, List<DelegacaoMcp>? delegacoes = null)
    {
        mcp.Tocar();
        mcp.Responder(turno, texto, delegacoes?.Select(d => new Delegacao(d.Para, d.Tarefa)).ToList());
        return Json(new { ok = true });
    }

    [McpServerTool(Name = "ver_conversa"), Description("A conversa (a última do diagrama, se não disser qual): status, mensagens e a resposta final.")]
    public string VerConversa(string diagrama, string? conversa = null)
    {
        Abrir(diagrama);
        var conversas = ia.ConversasDe(diagrama);
        var id = conversa ?? conversas.Listar().FirstOrDefault()?.Id ?? throw new OpError(404, "Este diagrama ainda não tem conversas.");
        var c = conversas.Obter(id);
        return Json(new
        {
            id = c.Id, assunto = c.Assunto, modo = c.Modo, status = c.Status, resposta = c.Resposta,
            mensagens = c.Mensagens.Select(m => new { nome = m.Nome, tipo = m.Tipo, persona = m.Persona, texto = m.Texto, detalhe = m.Detalhe }),
        });
    }

    // ---- o orquestrador por template (R06) ----------------------------------

    [McpServerTool(Name = "perguntar_orquestrador"), Description("Faz uma pergunta factual ao orquestrador por template: se a estrutura já é conhecida, a resposta sai do template + base de fatos, sem chamar o LLM; senão, o LLM (motor: api, ollama) responde e o template é extraído. Devolve o texto, o caminho (template ou llm), a chave, o template e o peso.")]
    public async Task<string> PerguntarOrquestrador(string pergunta, [Description("api (padrão) ou ollama: quem responde às chaves novas")] string? motor = null, CancellationToken ct = default)
    {
        mcp.Tocar();
        if (motor == "mcp") throw new OpError(400, "Pelo MCP, use motor api ou ollama (o mcp seria você mesmo respondendo).");
        var r = await ApiOrquestrador.Perguntar(orquestrador, motores, aoVivo, pergunta, motor, ct);
        return Json(new { r.Texto, r.Caminho, r.Chave, r.Entidade, r.Template, r.Peso, r.TemplateSalvo, r.Motivo, r.ChamadasGeracao, r.ChamadasParser });
    }

    [McpServerTool(Name = "avaliar_resposta"), Description("O feedback sobre uma resposta do orquestrador (pela chave): bom sobe o peso do template, ruim desce; abaixo do limiar, o template é descartado.")]
    public string AvaliarResposta(string chave, bool bom)
    {
        mcp.Tocar();
        var t = ApiOrquestrador.Feedback(orquestrador, aoVivo, chave, bom);
        return Json(new { template = t, descartado = t is null });
    }

    [McpServerTool(Name = "ver_orquestrador"), Description("As estatísticas do orquestrador (perguntas, acertos de template, chamadas ao LLM, tokens, custo, latência) e os templates guardados com o peso.")]
    public string VerOrquestrador()
    {
        mcp.Tocar();
        return Json(ApiOrquestrador.Estado(orquestrador));
    }

    [McpServerTool(Name = "sintetizar"), Description("Pede a resposta final polida agora (depois da vez em andamento).")]
    public string Sintetizar(string diagrama, string conversa)
    {
        Abrir(diagrama);
        ia.ConversasDe(diagrama).Sintetizar(conversa);
        return Json(new { ok = true });
    }

    [McpServerTool(Name = "parar"), Description("Para a conversa (os turnos pendentes são cancelados).")]
    public string Parar(string diagrama, string conversa)
    {
        Abrir(diagrama);
        ia.ConversasDe(diagrama).Parar(conversa);
        return Json(new { ok = true });
    }
}
