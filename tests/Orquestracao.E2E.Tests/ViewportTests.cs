using Microsoft.Playwright;

namespace Orquestracao.E2E.Tests;

/// <summary>
/// Portado de legado/src/viewport.test.ts. As funções da câmera do canvas (js/viewport.js) rodam no
/// próprio Chromium: o teste importa o módulo na página e confere o resultado em C#.
/// </summary>
[Collection("app")]
public class ViewportTests(App app) : IAsyncLifetime
{
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _page = await app.Browser.NewPageAsync();
        await _page.GotoAsync($"{app.Url}/css/style.css"); // qualquer página da mesma origem serve para importar o módulo
    }

    public Task DisposeAsync() => _page.CloseAsync();

    /// <summary>Roda <paramref name="corpo"/> com o módulo em <c>v</c> e devolve o resultado como JSON.</summary>
    private async Task<string> Js(string corpo)
    {
        // O JSON é feito na própria página (o Playwright acrescentaria metadados como "$id").
        return await _page.EvaluateAsync<string>($"async () => {{ const v = await import('/js/viewport.js'); return JSON.stringify({corpo}); }}");
    }

    [Fact]
    public async Task ConverteTelaEMundo()
    {
        Assert.Equal("""{"x":100,"y":100}""", await Js("v.toWorld({ x: 100, y: -50, k: 2 }, 300, 150)"));
        Assert.Equal("""{"x":300,"y":150}""", await Js("v.toScreen({ x: 100, y: -50, k: 2 }, 100, 100)"));
    }

    [Fact]
    public async Task Zoom_MantemOPontoDoCursorParado_ERespeitaOsLimites()
    {
        Assert.Equal("true", await Js("(() => { const a = { x: 0, y: 0, k: 1 }; const z = v.zoomAt(a, 2, 200, 100); const p = v.toWorld(z, 200, 100), q = v.toWorld(a, 200, 100); return p.x === q.x && p.y === q.y && z.k === 2; })()"));
        Assert.Equal("true", await Js("v.zoomAt({ x: 0, y: 0, k: 1 }, 100, 0, 0).k === v.MAX_ZOOM"));
        Assert.Equal("true", await Js("v.zoomAt({ x: 0, y: 0, k: 1 }, 0.001, 0, 0).k === v.MIN_ZOOM"));
    }

    [Fact]
    public async Task VerTudo_DesenhoGrandeDiminuiEFicaCentralizado()
    {
        Assert.Equal("""[0.5,{"x":500,"y":300},true]""", await Js(
            "(() => { const box = { x: 0, y: 0, w: 2000, h: 500 }; const f = v.fitBox(box, 1000, 600, 0); return [f.k, v.toScreen(f, 1000, 250), v.contains(f, box, 1000, 600)]; })()"));
    }

    [Fact]
    public async Task VerTudo_NaoAmpliaUmDesenhoPequenoAlemDeMaxZoom()
    {
        Assert.Equal("1", await Js("v.fitBox({ x: 100, y: 100, w: 100, h: 40 }, 1000, 600).k"));
        Assert.Equal("1.8", await Js("v.fitBox({ x: 100, y: 100, w: 100, h: 40 }, 1000, 600, 40, 1.8).k"));
    }

    [Fact]
    public async Task Contains_PercebeOQueFicouForaDaTela()
    {
        Assert.Equal("[true,false,false]", await Js(
            "[{ x: 10, y: 10, w: 100, h: 100 }, { x: 450, y: 10, w: 100, h: 100 }, { x: -5, y: 10, w: 10, h: 10 }].map((b) => v.contains({ x: 0, y: 0, k: 1 }, b, 500, 500))"));
    }

    [Fact]
    public async Task UnionELerp()
    {
        Assert.Equal("null", await Js("v.union([])"));
        Assert.Equal("""{"x":-5,"y":0,"w":15,"h":20}""", await Js("v.union([{ x: 0, y: 10, w: 10, h: 10 }, { x: -5, y: 0, w: 5, h: 5 }])"));
        Assert.Equal("""[{"x":0,"y":0,"k":1},{"x":100,"y":50,"k":2}]""", await Js(
            "[v.lerpView({ x: 0, y: 0, k: 1 }, { x: 100, y: 50, k: 2 }, 0), v.lerpView({ x: 0, y: 0, k: 1 }, { x: 100, y: 50, k: 2 }, 1)]"));
    }
}
