using System.Text.RegularExpressions;
using static Microsoft.Playwright.Assertions;

namespace Orquestracao.E2E.Tests;

/// <summary>O editor no navegador falando com o backend em C#: canvas, SSE, painel de código, preview e menu.</summary>
[Collection("app")]
public class EditorTests(App app)
{
    private async Task<string> DiagramaAB()
    {
        var slug = await app.NovoDiagrama("ab");
        await app.Enviar("POST", $"/api/d/{slug}/nodes", new { text = "Início", x = 200, y = 150 });
        await app.Enviar("POST", $"/api/d/{slug}/nodes", new { text = "Fim", x = 500, y = 150 });
        await app.Enviar("POST", $"/api/d/{slug}/edges", new { from = "n1", to = "n2", text = "processa" });
        return slug;
    }

    [Fact]
    public async Task AbreODiagramaDoServidor_ComRetangulosESetas()
    {
        var slug = await DiagramaAB();
        var page = await app.Abrir(slug, retangulos: 2);
        await Expect(page.Locator("[data-node=n1] .node-label")).ToHaveTextAsync("Início");
        await Expect(page.Locator("[data-edge=e1] .edge-label")).ToHaveTextAsync("processa");
        await Expect(page.Locator("#diagram-name")).ToContainTextAsync("ab");
    }

    [Fact]
    public async Task RetanguloCriadoNoCanvas_ChegaAoServidor()
    {
        var slug = await app.NovoDiagrama("criar");
        var page = await app.Abrir(slug);
        await page.Locator("[data-left-tab=componentes]").ClickAsync(); // a paleta fica na aba Componentes
        await page.Locator("[data-tool=rect]").ClickAsync();
        var editor = page.Locator(".inline-editor");
        await Expect(editor).ToBeVisibleAsync();
        await editor.FillAsync("Parser");
        await editor.PressAsync("Enter");

        var nodes = await App.Esperar(() => app.Get($"/api/d/{slug}/nodes"), n => n.AsArray().Any(x => (string?)x!["text"] == "Parser"));
        Assert.Contains("Parser", nodes.AsArray().Select(x => (string?)x!["text"]));
    }

    [Fact]
    public async Task MudancaFeitaPelaApi_ApareceAoVivoNoCanvas()
    {
        var slug = await DiagramaAB();
        var page = await app.Abrir(slug, retangulos: 2);
        await app.Enviar("PATCH", $"/api/d/{slug}/nodes/n2", new { text = "Entrega", active = true });
        await Expect(page.Locator("[data-node=n2] .node-label")).ToHaveTextAsync("Entrega");
        await Expect(page.Locator("[data-node=n2]")).ToHaveClassAsync(new Regex(@"\bactive\b"));
        await app.Enviar("POST", $"/api/d/{slug}/nodes", new { text = "Feedback" });
        await Expect(page.Locator("[data-node]")).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task PainelDeCodigo_MostraOMermaidGeradoNoServidor_EAEdicaoVoltaParaOCanvas()
    {
        var slug = await DiagramaAB();
        var page = await app.Abrir(slug, retangulos: 2);
        var code = page.Locator("#code");
        await Expect(code).ToHaveValueAsync(new Regex("n1 -->\\|\"processa\"\\| n2"));

        var novo = (await code.InputValueAsync()).Replace("    n1 -->|\"processa\"| n2", "    n3(\"Fonte externa\")\n    n1 -->|\"processa\"| n2\n    n2 --> n3");
        await code.FillAsync(novo);
        await Expect(page.Locator("[data-node]")).ToHaveCountAsync(3);
        var nodes = await App.Esperar(() => app.Get($"/api/d/{slug}/nodes"), n => n.AsArray().Count == 3);
        Assert.Equal("Fonte externa", (string?)nodes[2]!["text"]);
        await Expect(page.Locator("#errors")).ToBeEmptyAsync();
    }

    [Fact]
    public async Task CodigoInvalido_MostraOErro_ENaoMudaOCanvas()
    {
        var slug = await DiagramaAB();
        var page = await app.Abrir(slug, retangulos: 2);
        await page.Locator("#code").FillAsync("flowchart LR\n    isto não é mermaid");
        await Expect(page.Locator("#errors")).ToContainTextAsync("não reconhecida");
        await Expect(page.Locator("[data-node]")).ToHaveCountAsync(2);
    }

    [Fact]
    public async Task Preview_DesenhaOMermaidLocal()
    {
        var slug = await DiagramaAB();
        var page = await app.Abrir(slug, retangulos: 2);
        await Expect(page.Locator("#preview svg")).ToBeVisibleAsync();
        await Expect(page.Locator("#preview")).ToContainTextAsync("Início");
    }

    [Fact]
    public async Task Menu_ListaOsDiagramas_EAtualizaAoVivo()
    {
        var slug = await app.NovoDiagrama("menu");
        var page = await app.Abrir(slug);
        await page.Locator("#btn-menu").ClickAsync();
        await Expect(page.Locator("#menu")).ToBeVisibleAsync();
        await Expect(page.Locator(".menu-item.current")).ToHaveCountAsync(1);
        var outro = await app.NovoDiagrama("chegou-agora");
        Assert.NotEqual(slug, outro);
        await Expect(page.Locator(".menu-item", new() { HasText = "chegou-agora" })).ToHaveCountAsync(1);
    }
}
