using Orquestracao.Core.Conversas;
using Orquestracao.Core.Diagramas;
using Orquestracao.Motores;
using Orquestracao.Web;

var builder = WebApplication.CreateBuilder(args);

// Pasta dos diagramas: Diagramas:Pasta (ou MERMAID_DRAW_DIR, como no app original); padrão: diagrams/.
var pasta = builder.Configuration["Diagramas:Pasta"]
    ?? Environment.GetEnvironmentVariable("MERMAID_DRAW_DIR")
    ?? Path.Combine(builder.Environment.ContentRootPath, "diagrams");
var antigo = builder.Configuration["Diagramas:ArquivoAntigo"] ?? Environment.GetEnvironmentVariable("MERMAID_DRAW_FILE");
builder.Services.AddSingleton(_ => new Workspace(pasta, antigo));

// Os motores que dão voz às personas. Com dois de mesmo nome, vale o último registrado (os testes trocam por falsos).
builder.Services.AddSingleton<MotorMcp>();
builder.Services.AddSingleton<IMotor>(sp => sp.GetRequiredService<MotorMcp>());
builder.Services.AddSingleton<IMotor>(_ => new MotorOllama());
builder.Services.AddSingleton<IMotor>(_ => new MotorApi());
builder.Services.AddSingleton<RegistroDeMotores>();
var templates = EspacoIa.AcharTemplates(builder.Configuration["Templates:Pasta"], builder.Environment.ContentRootPath);
builder.Services.AddSingleton(sp => new EspacoIa(sp.GetRequiredService<Workspace>(), sp.GetRequiredService<RegistroDeMotores>(), templates));

// O servidor MCP (Streamable HTTP, sem sessão): o Claude Code dá voz às personas de motor "mcp".
builder.Services.AddMcpServer().WithHttpTransport(o => o.Stateless = true).WithTools<FerramentasMcp>();

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
app.MapApiPersonas();
app.MapApiDiagramas();
app.MapMcp("/mcp");

app.Run();

/// <summary>Exposto para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
