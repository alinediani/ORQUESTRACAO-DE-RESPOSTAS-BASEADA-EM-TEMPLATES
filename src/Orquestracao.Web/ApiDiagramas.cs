using System.Text.Json;
using System.Text.Json.Nodes;
using Orquestracao.Core.Diagramas;
using static Orquestracao.Web.Http;

namespace Orquestracao.Web;

/// <summary>
/// A API dos diagramas, com as mesmas rotas do app original (legado/server/api.ts e diagrams.ts).
/// Cada retângulo e cada seta é um recurso próprio: um POST/PATCH/DELETE mexe só naquele elemento,
/// e os navegadores recebem só o evento daquela mudança.
/// <code>
/// /api/d/{slug}/nodes|edges|diagram|diagram.json|events   as rotas de um diagrama
/// /api/nodes|edges|diagram|diagram.json                   o primeiro diagrama (rotas antigas)
/// /api/diagrams[/{slug}[/duplicate|/export]]  /api/diagrams/import  /api/events (a lista)
/// </code>
/// </summary>
public static class ApiDiagramas
{
    /// <summary>O retângulo como a API devolve: active e owner sempre presentes, posição arredondada.</summary>
    public sealed record NoSaida(string Id, string Text, bool Active, string? Owner, long X, long Y)
    {
        public static NoSaida De(DiagramNode n) => new(n.Id, n.Text, n.Active, n.Owner, Arredonda(n.X), Arredonda(n.Y));

        private static long Arredonda(double v) => (long)Math.Floor(v + 0.5);
    }

    public static void MapApiDiagramas(this WebApplication app)
    {
        var ws = app.Services.GetRequiredService<Workspace>();

        // ---- a lista de diagramas ---------------------------------------------
        app.MapGet("/api/events", (HttpContext ctx) => Sse(ctx, ws.Assinar()));
        app.MapGet("/api/diagrams", (HttpContext ctx) => Enviar(ctx, 200, ws.Listar()));
        app.MapPost("/api/diagrams", async (HttpContext ctx) => await Enviar(ctx, 201, ws.Criar(await LerNome(ctx))));
        app.MapPost("/api/diagrams/import", async (HttpContext ctx) =>
            await Enviar(ctx, 201, ws.Importar(ctx.Request.Query["name"], await LerBytes(ctx, Workspace.MaxImport))));
        app.MapPatch("/api/diagrams/{slug}", async (HttpContext ctx, string slug) => await Enviar(ctx, 200, ws.Renomear(slug, await LerNome(ctx))));
        app.MapDelete("/api/diagrams/{slug}", (HttpContext ctx, string slug) =>
        {
            ws.Apagar(slug);
            return Enviar(ctx, 204);
        });
        app.MapPost("/api/diagrams/{slug}/duplicate", (HttpContext ctx, string slug) => Enviar(ctx, 201, ws.Duplicar(slug)));
        app.MapGet("/api/diagrams/{slug}/export", async (HttpContext ctx, string slug) =>
        {
            var arquivo = ws.NomeDoArquivo(slug);
            if (ctx.Request.Query["format"] == "mmd")
            {
                ctx.Response.ContentType = "text/plain; charset=utf-8";
                ctx.Response.Headers.ContentDisposition = $"attachment; filename=\"{arquivo}.mmd\"";
                await ctx.Response.WriteAsync(Mermaid.Gerar(ws.Atual(slug)) + "\n");
                return;
            }
            var zip = ws.ExportarZip(slug);
            ctx.Response.ContentType = "application/zip";
            ctx.Response.Headers.ContentDisposition = $"attachment; filename=\"{arquivo}.zip\"";
            await ctx.Response.Body.WriteAsync(zip);
        });

        // ---- código Mermaid, sem estado (o painel de código do front) ---------
        // O front não tem parser próprio: lê e gera o código aqui, e o resultado entra pelo fluxo normal.
        app.MapPost("/api/mermaid/ler", async (HttpContext ctx) =>
        {
            var corpo = await LerJson(ctx);
            if (corpo.ValueKind != JsonValueKind.Object || !corpo.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.String)
                throw new OpError(400, "Esperado { \"code\": \"…\", \"previous\"?: { nodes, edges } }.");
            Diagram? anterior = null;
            if (corpo.TryGetProperty("previous", out var prev) && prev.ValueKind == JsonValueKind.Object)
                anterior = prev.Deserialize<Diagram>(Json.Opcoes);
            var (diagram, errors) = Mermaid.Ler(code.GetString()!, anterior);
            await Enviar(ctx, 200, new { diagram, errors });
        });
        app.MapPost("/api/mermaid/gerar", async (HttpContext ctx) =>
        {
            var d = (await LerJson(ctx)).Deserialize<Diagram>(Json.Opcoes) ?? new Diagram();
            await Enviar(ctx, 200, Mermaid.Gerar(d));
        });

        // ---- cada diagrama ----------------------------------------------------
        MapDiagrama(app.MapGroup("/api/d/{slug}"), ctx => ws.Diagrama((string)ctx.Request.RouteValues["slug"]!), comEventos: true);
        // Rotas antigas, sem prefixo: o primeiro diagrama da lista.
        MapDiagrama(app.MapGroup("/api"), _ => ws.Primeiro(), comEventos: false);

        // Qualquer outra rota em /api: 404 em JSON (e não 405 quando só o método não existe).
        app.Map("/api/{**resto}", (HttpContext ctx) =>
            Enviar(ctx, 404, new { error = $"Rota não encontrada: {ctx.Request.Method} {ctx.Request.Path}" }));
    }

    private static async Task<string?> LerNome(HttpContext ctx)
    {
        var texto = await LerTexto(ctx);
        if (string.IsNullOrWhiteSpace(texto)) return "";
        try
        {
            using var doc = JsonDocument.Parse(texto);
            return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("name", out var n)
                ? n.ValueKind == JsonValueKind.String ? n.GetString() : n.GetRawText()
                : "";
        }
        catch (JsonException)
        {
            throw new OpError(400, "Corpo não é um JSON válido.");
        }
    }

    private static void MapDiagrama(RouteGroupBuilder g, Func<HttpContext, DiagramaArmazenado> abrir, bool comEventos)
    {
        if (comEventos) g.MapGet("/events", (HttpContext ctx) => Sse(ctx, abrir(ctx).Assinar()));

        // ---- código e estado inteiro ----------------------------------------
        g.MapGet("/diagram", (HttpContext ctx) =>
        {
            var a = abrir(ctx);
            return a.Vazio ? Enviar(ctx, 204, rev: a.Rev) : Enviar(ctx, 200, Mermaid.Gerar(a.Diagrama), a.Rev);
        });
        g.MapPut("/diagram", async (HttpContext ctx) =>
        {
            var a = abrir(ctx);
            var (diagram, errors) = Mermaid.Ler(await LerTexto(ctx), a.Vazio ? null : a.Diagrama);
            if (errors.Count > 0)
            {
                await Enviar(ctx, 400, new { errors }, a.Rev);
                return;
            }
            var (_, rev) = a.Commit(Source(ctx), _ => new DiagramReplaced(diagram));
            await Enviar(ctx, 204, rev: rev);
        });
        g.MapGet("/diagram.json", (HttpContext ctx) =>
        {
            var a = abrir(ctx);
            return Enviar(ctx, 200, a.Estado(), a.Rev);
        });
        g.MapPut("/diagram.json", async (HttpContext ctx) =>
        {
            var a = abrir(ctx);
            var novo = Entrada.Diagrama(await LerJson(ctx));
            var (_, rev) = a.Commit(Source(ctx), _ => new DiagramReplaced(novo));
            await Enviar(ctx, 204, rev: rev);
        });

        // ---- retângulos -----------------------------------------------------
        g.MapGet("/nodes", (HttpContext ctx) =>
        {
            var a = abrir(ctx);
            return Enviar(ctx, 200, a.Diagrama.Nodes.Select(NoSaida.De).ToList(), a.Rev);
        });
        g.MapPost("/nodes", async (HttpContext ctx) =>
        {
            var a = abrir(ctx);
            var novo = Entrada.NovoNo(await LerJson(ctx));
            var (ev, rev) = a.Commit(Source(ctx), d => Ops.AddNode(d, novo));
            await Enviar(ctx, 201, NoSaida.De(((NodeAdded)ev).Node), rev);
        });
        g.MapGet("/nodes/{ref}", (HttpContext ctx, string @ref) =>
        {
            var a = abrir(ctx);
            return Enviar(ctx, 200, NoSaida.De(Ops.ResolveNode(a.Diagrama, @ref)), a.Rev);
        });
        g.MapPatch("/nodes/{ref}", async (HttpContext ctx, string @ref) =>
        {
            var a = abrir(ctx);
            var id = Ops.ResolveNode(a.Diagrama, @ref).Id;
            var patch = Entrada.PatchNo(await LerJson(ctx));
            var (ev, rev) = a.Commit(Source(ctx), d => Ops.UpdateNode(d, id, patch));
            await Enviar(ctx, 200, NoSaida.De(((NodeUpdated)ev).Node), rev);
        });
        g.MapDelete("/nodes/{ref}", (HttpContext ctx, string @ref) =>
        {
            var a = abrir(ctx);
            var id = Ops.ResolveNode(a.Diagrama, @ref).Id;
            var (ev, rev) = a.Commit(Source(ctx), d => Ops.RemoveNode(d, id));
            return Enviar(ctx, 200, ev, rev);
        });
        g.MapPost("/nodes/{ref}/{acao:regex(^(ativar|desativar|alternar)$)}", (HttpContext ctx, string @ref, string acao) =>
        {
            var a = abrir(ctx);
            var no = Ops.ResolveNode(a.Diagrama, @ref);
            var active = acao switch { "ativar" => true, "desativar" => false, _ => !no.Active };
            var (ev, rev) = a.Commit(Source(ctx), d => Ops.UpdateNode(d, no.Id, new NodePatch { Active = active }));
            return Enviar(ctx, 200, NoSaida.De(((NodeUpdated)ev).Node), rev);
        });

        // ---- setas ----------------------------------------------------------
        g.MapGet("/edges", (HttpContext ctx) =>
        {
            var a = abrir(ctx);
            return Enviar(ctx, 200, a.Diagrama.Edges, a.Rev);
        });
        g.MapPost("/edges", async (HttpContext ctx) =>
        {
            var a = abrir(ctx);
            var nova = Entrada.NovaSeta(ResolverPontas(a, await LerJson(ctx)));
            var (ev, rev) = a.Commit(Source(ctx), d => Ops.AddEdge(d, nova));
            await Enviar(ctx, 201, ((EdgeAdded)ev).Edge, rev);
        });
        g.MapGet("/edges/{id}", (HttpContext ctx, string id) =>
        {
            var a = abrir(ctx);
            return Enviar(ctx, 200, Ops.GetEdge(a.Diagrama, id), a.Rev);
        });
        g.MapPatch("/edges/{id}", async (HttpContext ctx, string id) =>
        {
            var a = abrir(ctx);
            var patch = Entrada.PatchSeta(ResolverPontas(a, await LerJson(ctx)));
            var (ev, rev) = a.Commit(Source(ctx), d => Ops.UpdateEdge(d, id, patch));
            await Enviar(ctx, 200, ((EdgeUpdated)ev).Edge, rev);
        });
        g.MapDelete("/edges/{id}", (HttpContext ctx, string id) =>
        {
            var (_, rev) = abrir(ctx).Commit(Source(ctx), d => Ops.RemoveEdge(d, id));
            return Enviar(ctx, 204, rev: rev);
        });
        g.MapPost("/edges/{id}/pulse", async (HttpContext ctx, string id) =>
        {
            var a = abrir(ctx);
            Ops.GetEdge(a.Diagrama, id);
            var corpo = await LerJson(ctx);
            string? label = null;
            if (corpo.ValueKind == JsonValueKind.Object && corpo.TryGetProperty("label", out var l))
                label = l.ValueKind == JsonValueKind.String ? l.GetString()!.Trim() : throw new OpError(400, "\"label\" deve ser um texto.");
            // Efêmero: não entra no estado, não sobe o rev. Quem estiver olhando vê a mensagem passar.
            var ev = new JsonObject { ["type"] = "edge.pulse", ["id"] = id };
            if (!string.IsNullOrEmpty(label)) ev["label"] = label;
            ev["source"] = Source(ctx);
            a.Transmitir(ev);
            await Enviar(ctx, 204, rev: a.Rev);
        });
    }

    /// <summary>Nas setas, from/to aceitam id ou texto do retângulo, como :ref em /nodes.</summary>
    private static JsonElement ResolverPontas(DiagramaArmazenado a, JsonElement corpo)
    {
        if (corpo.ValueKind != JsonValueKind.Object) return corpo;
        var obj = JsonNode.Parse(corpo.GetRawText())!.AsObject();
        var d = a.Diagrama;
        foreach (var campo in new[] { "from", "to" })
            if (obj[campo] is JsonValue v && v.TryGetValue<string>(out var texto))
                obj[campo] = Ops.ResolveNode(d, texto).Id;
        return JsonSerializer.SerializeToElement(obj);
    }
}
