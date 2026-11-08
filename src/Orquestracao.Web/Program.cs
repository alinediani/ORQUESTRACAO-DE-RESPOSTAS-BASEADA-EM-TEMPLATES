var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// O front (JavaScript puro, sem build) é servido de wwwroot/.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/saude", () => Results.Ok(new { ok = true }));

app.Run();

/// <summary>Exposto para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
