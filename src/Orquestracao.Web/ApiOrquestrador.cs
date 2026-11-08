using System.Text.Json;
using Orquestracao.Core.Conversas;
using Orquestracao.Core.Diagramas;
using Orquestracao.Templates;
using static Orquestracao.Web.Http;

namespace Orquestracao.Web;

/// <summary>
/// As rotas do orquestrador por template (o TCC):
/// <code>
/// GET    /api/orquestrador                   configuração, estatísticas (R07), templates e a fonte
/// POST   /api/orquestrador/perguntar         {pergunta, motor?} → o resultado, com o caminho seguido
/// POST   /api/orquestrador/feedback          {chave, bom} → o template com o peso novo (ou descartado)
/// DELETE /api/orquestrador/templates[/{chave}]  apaga um template, ou todos
/// GET    /api/orquestrador/events            SSE: cada etapa do fluxo, cada resultado e cada feedback
/// </code>
/// </summary>
public static class ApiOrquestrador
{
    /// <summary>A "persona" do orquestrador quando ele é usado direto (fora de uma conversa).</summary>
    public static Persona PersonaDireta(string motor) => new()
    {
        // No Ollama, o modelo local; nos outros, o do LLM de geração do TCC (Claude Sonnet).
        Id = "orquestrador", Nome = "Orquestrador", Motor = motor, Modelo = motor == "ollama" ? Personas.ModeloPadrao["ollama"] : Personas.ModeloPadrao["template"],
        Prompt = "Você responde perguntas factuais de forma curta e correta.",
    };

    /// <summary>Pergunta direto ao orquestrador, com o LLM de geração no motor pedido (api, ollama ou mcp).</summary>
    public static async Task<ResultadoOrquestrador> Perguntar(Orquestrador orq, RegistroDeMotores motores, OrquestradorAoVivo aoVivo, string? pergunta, string? motor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pergunta)) throw new OpError(400, "Escreva a pergunta.");
        motor ??= "api";
        if (motor is not ("api" or "ollama" or "mcp") || !motores.Tem(motor)) throw new OpError(400, "\"motor\" deve ser api, ollama ou mcp.");
        // O resultado chega ao painel pelo evento Respondido do orquestrador (OrquestradorAoVivo).
        return await orq.Responder(pergunta.Trim(), new LlmPorMotor(motores[motor], PersonaDireta(motor)), ct);
    }

    public static TemplateResposta? Feedback(Orquestrador orq, OrquestradorAoVivo aoVivo, string? chave, bool bom)
    {
        if (string.IsNullOrWhiteSpace(chave)) throw new OpError(400, "Diga a chave do template.");
        var t = orq.Feedback(chave, bom);
        aoVivo.Publicar(new { type = "feedback", chave, bom, template = t });
        return t;
    }

    public static object Estado(Orquestrador orq) => new
    {
        configuracao = orq.Configuracao,
        estatisticas = orq.Estatisticas.Resumo(),
        templates = orq.Store.Listar(),
        fonte = new { entidades = orq.Fonte.Entidades.Count, tipos = orq.Fonte.Entidades.GroupBy(e => e.Tipo).ToDictionary(g => g.Key, g => g.Count()) },
    };

    public static void MapApiOrquestrador(this WebApplication app)
    {
        var orq = app.Services.GetRequiredService<Orquestrador>();
        var motores = app.Services.GetRequiredService<RegistroDeMotores>();
        var aoVivo = app.Services.GetRequiredService<OrquestradorAoVivo>();

        app.MapGet("/api/orquestrador", (HttpContext ctx) => Enviar(ctx, 200, Estado(orq)));
        app.MapGet("/api/orquestrador/events", (HttpContext ctx) => Sse(ctx, aoVivo.Ouvintes.Assinar()));
        app.MapPost("/api/orquestrador/perguntar", async (HttpContext ctx) =>
        {
            var corpo = await LerJson(ctx);
            string? Campo(string k) => corpo.ValueKind == JsonValueKind.Object && corpo.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            await Enviar(ctx, 200, await Perguntar(orq, motores, aoVivo, Campo("pergunta"), Campo("motor"), ctx.RequestAborted));
        });
        app.MapPost("/api/orquestrador/feedback", async (HttpContext ctx) =>
        {
            var corpo = await LerJson(ctx);
            if (corpo.ValueKind != JsonValueKind.Object || !corpo.TryGetProperty("bom", out var b) || b.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new OpError(400, "Esperado { \"chave\": \"…\", \"bom\": true | false }.");
            var chave = corpo.TryGetProperty("chave", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            var t = Feedback(orq, aoVivo, chave, b.GetBoolean());
            await Enviar(ctx, 200, new { template = t, descartado = t is null });
        });
        app.MapDelete("/api/orquestrador/templates", (HttpContext ctx) =>
        {
            foreach (var t in orq.Store.Listar()) orq.Store.Remover(t.Chave);
            return Enviar(ctx, 204);
        });
        app.MapDelete("/api/orquestrador/templates/{chave}", (HttpContext ctx, string chave) =>
        {
            if (orq.Store.Obter(chave) is null) throw new OpError(404, $"Template \"{chave}\" não encontrado.");
            orq.Store.Remover(chave);
            return Enviar(ctx, 204);
        });
    }
}
