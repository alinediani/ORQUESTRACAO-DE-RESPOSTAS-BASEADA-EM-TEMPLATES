using Orquestracao.Core.Diagramas;
using Orquestracao.Web;

var builder = WebApplication.CreateBuilder(args);

// Pasta dos diagramas: Diagramas:Pasta (ou MERMAID_DRAW_DIR, como no app original); padrão: diagrams/.
var pasta = builder.Configuration["Diagramas:Pasta"]
    ?? Environment.GetEnvironmentVariable("MERMAID_DRAW_DIR")
    ?? Path.Combine(builder.Environment.ContentRootPath, "diagrams");
var antigo = builder.Configuration["Diagramas:ArquivoAntigo"] ?? Environment.GetEnvironmentVariable("MERMAID_DRAW_FILE");
builder.Services.AddSingleton(_ => new Workspace(pasta, antigo));

var app = builder.Build();

// Erros das operações viram {error} com o status certo (400, 404, 409); o resto, 500.
app.Use(async (ctx, next) =>
{
    try
    {
        await next(ctx);
    }
    catch (OpError e) when (!ctx.Response.HasStarted)
    {
        await Http.Enviar(ctx, e.Status, new { error = e.Message });
    }
    catch (Exception e) when (!ctx.Response.HasStarted && ctx.Request.Path.StartsWithSegments("/api"))
    {
        await Http.Enviar(ctx, 500, new { error = e.Message });
    }
});

// O front (JavaScript puro, sem build) é servido de wwwroot/.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/saude", () => Results.Ok(new { ok = true }));
app.MapApiDiagramas();

app.Run();

/// <summary>Exposto para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
