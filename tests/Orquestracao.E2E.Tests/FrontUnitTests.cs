using System.Text.Json;
using Microsoft.Playwright;

namespace Orquestracao.E2E.Tests;

/// <summary>
/// Os testes de unidade do front (legado/src/chatstate.test.ts e palacio.test.ts) rodando no próprio
/// Chromium: js/mini-teste.js (describe/it/expect) + o *.spec.js viram um módulo, que importa o código
/// de wwwroot/js e devolve o resultado de cada teste.
/// </summary>
[Collection("app")]
public class FrontUnitTests(App app)
{
    private sealed record Resultado(string Nome, bool Ok, string? Erro);

    [Theory]
    [InlineData("chatstate.spec.js", 9)]
    [InlineData("palacio.spec.js", 3)]
    public async Task SpecDoFront_PassaNoNavegador(string spec, int quantos)
    {
        var pasta = Path.Combine(AppContext.BaseDirectory, "js");
        var fonte = (await File.ReadAllTextAsync(Path.Combine(pasta, "mini-teste.js"))) + "\n" + await File.ReadAllTextAsync(Path.Combine(pasta, spec));
        // O módulo roda a partir de um blob: os imports de /js/ precisam do endereço completo do app.
        fonte = fonte.Replace("from '/js/", $"from '{app.Url}/js/");

        var page = await app.Browser.NewPageAsync();
        await page.GotoAsync($"{app.Url}/css/style.css");
        var json = await page.EvaluateAsync<string>(
            "async (fonte) => { const url = URL.createObjectURL(new Blob([fonte], { type: 'text/javascript' })); const m = await import(url); return JSON.stringify(await m.rodar()); }",
            fonte);
        await page.CloseAsync();

        var resultados = JsonSerializer.Deserialize<List<Resultado>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(quantos, resultados.Count);
        var falhas = resultados.Where(r => !r.Ok).Select(r => $"{r.Nome}: {r.Erro}").ToList();
        Assert.True(falhas.Count == 0, string.Join("\n", falhas));
    }
}
