using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Orquestracao.E2E.Tests;

/// <summary>F6: o fluxo do orquestrador visível — o painel, os pulsos no canvas da Figura 1 e o selo no chat.</summary>
[Collection("app")]
public class OrquestradorPainelTests(App app)
{
    /// <summary>Começa sem templates (o app é o mesmo para a coleção inteira).</summary>
    private async Task Limpar() => (await app.Api.DeleteAsync("/api/orquestrador/templates")).EnsureSuccessStatusCode();

    private async Task<IPage> AbrirPainel()
    {
        var page = await app.Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = 1400, Height = 1000 } });
        var erros = new List<string>();
        page.PageError += (_, e) => erros.Add(e);
        await page.GotoAsync($"{app.Url}/orquestrador.html");
        await Expect(page.Locator("body[data-pronto=true]")).ToHaveCountAsync(1);
        Assert.Empty(erros);
        return page;
    }

    private static async Task Perguntar(IPage page, string pergunta)
    {
        await page.Locator("#pergunta").FillAsync(pergunta);
        await page.Locator("#form-pergunta button[type=submit]").ClickAsync();
    }

    [Fact]
    public async Task Painel_ChaveNovaVaiAoLlm_EAMesmaFamiliaSaiDoTemplate_ComOCaminhoDestacado()
    {
        await Limpar();
        var page = await AbrirPainel();
        await Expect(page.Locator("#fluxo svg")).ToBeVisibleAsync(); // a Figura 1, ainda sem caminho
        await Expect(page.Locator("#sem-templates")).ToBeVisibleAsync();

        await Perguntar(page, "Qual é a capital da França?");
        await Expect(page.Locator("#r-caminho")).ToHaveTextAsync("LLM padrão");
        await Expect(page.Locator("#r-texto")).ToHaveTextAsync("A capital da França é Paris.");
        await Expect(page.Locator("#r-template")).ToHaveTextAsync("A capital {de_entidade} é {capital}.");
        await Expect(page.Locator("#r-chave")).ToHaveTextAsync("capital {pais}");
        await Expect(page.Locator("#fluxo")).ToHaveAttributeAsync("data-caminho", "llm");
        await Expect(page.Locator("#templates tr")).ToHaveCountAsync(1);

        await Perguntar(page, "Me diga a capital do Japão");
        await Expect(page.Locator("#r-caminho")).ToHaveTextAsync("Template · sem LLM");
        await Expect(page.Locator("#r-texto")).ToHaveTextAsync("A capital do Japão é Tóquio.");
        await Expect(page.Locator("#r-chamadas")).ToContainTextAsync("0 chamadas de geração");
        await Expect(page.Locator("#fluxo")).ToHaveAttributeAsync("data-caminho", "template");
        await Expect(page.Locator("#templates tr[data-chave='capital {pais}'] td").Nth(3)).ToHaveTextAsync("1"); // usos
        await Expect(page.Locator(".numero[data-id=geracao] b")).Not.ToHaveTextAsync("0");
    }

    [Fact]
    public async Task Feedback_PeloPainel_BaixaOPeso_EDescartaAbaixoDoLimiar()
    {
        await Limpar();
        var page = await AbrirPainel();
        await Perguntar(page, "Qual o símbolo químico do ferro?");
        await Expect(page.Locator("#r-peso")).ToHaveTextAsync("0,50");

        await page.Locator("#ruim").ClickAsync();
        await Expect(page.Locator("#r-feedback")).ToHaveTextAsync("Peso agora: 0,35");
        await Expect(page.Locator("#templates tr td").Nth(2)).ToContainTextAsync("0,35");
        await page.Locator("#ruim").ClickAsync();
        await Expect(page.Locator("#r-feedback")).ToHaveTextAsync("Peso agora: 0,25");
        await page.Locator("#ruim").ClickAsync();
        await Expect(page.Locator("#r-feedback")).ToContainTextAsync("Template descartado");
        await Expect(page.Locator("#templates tr")).ToHaveCountAsync(0);
        await Expect(page.Locator("#sem-templates")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PerguntaDeOutraOrigem_ApareceAoVivoNoPainel()
    {
        await Limpar();
        var page = await AbrirPainel();
        await app.Enviar("POST", "/api/orquestrador/perguntar", new { pergunta = "Qual a moeda da Argentina?" });
        await Expect(page.Locator("#r-texto")).ToHaveTextAsync("A moeda da Argentina é o peso argentino.");
        await Expect(page.Locator("#log li").First).ToContainTextAsync("resposta pelo LLM");
        await Expect(page.Locator("#log")).ToContainTextAsync("motor → llm (chave nova)");
    }

    [Fact]
    public async Task Canvas_DaFigura1_CadaEtapaPercorreASeta()
    {
        var page = await app.Abrir("orquestrador-por-template", retangulos: 12);
        await Expect(page.Locator("[data-node=motor] .node-label")).ToHaveTextAsync("Motor · consulta a chave");
        // O pulso acende a seta por 1,4 s: um observador anota cada seta que acendeu, para o teste não depender do tempo.
        await page.EvaluateAsync(@"() => {
            window.__acesas = [];
            new MutationObserver((ms) => {
                for (const m of ms) {
                    const g = m.target;
                    if (g.classList?.contains('flash') && !window.__acesas.includes(g.dataset.edge)) window.__acesas.push(g.dataset.edge);
                }
            }).observe(document.querySelector('#canvas'), { subtree: true, attributes: true, attributeFilter: ['class'] });
        }");
        await app.Enviar("POST", "/api/orquestrador/perguntar", new { pergunta = "Qual é a capital da França?" });
        await page.WaitForFunctionAsync("() => window.__acesas.length >= 6");
        var acesas = await page.EvaluateAsync<string[]>("() => window.__acesas");
        Assert.Equal(["ui-parser", "parser-motor"], acesas[..2]);
        // A resposta chega à entrega pelo template (adapta → entrega) ou pelo LLM (salva → entrega).
        Assert.True(acesas.Contains("adapta-entrega") || acesas.Contains("salva-entrega"), string.Join(", ", acesas));

    }

    [Fact]
    public async Task Chat_ComAPersonaDoOrquestrador_MostraDeOndeVeioAResposta()
    {
        await Limpar();
        var slug = await app.NovoDiagrama("respondedor");
        await app.Enviar("POST", $"/api/d/{slug}/templates/respondedor", new { motor = "template", x = 300, y = 150 });
        var page = await app.Abrir(slug, retangulos: 1);
        await page.Locator("#chat-assunto").FillAsync("Qual é a capital da França?");
        await page.Locator("#chat-rodadas").FillAsync("1");
        await page.Locator("#chat-novo button[type=submit]").ClickAsync();
        var fala = page.Locator("#chat-mensagens .msg.tipo-fala");
        await Expect(fala).ToContainTextAsync("A capital da França é Paris.");
        await Expect(fala.Locator(".msg-caminho")).ToHaveTextAsync("LLM");
        await Expect(fala.Locator(".msg-caminho")).ToHaveAttributeAsync("title", new Regex("chave: capital \\{pais\\}"));

        await page.Locator("#chat-nova").ClickAsync();
        await page.Locator("#chat-assunto").FillAsync("Qual é a capital do Japão?");
        await page.Locator("#chat-novo button[type=submit]").ClickAsync();
        await Expect(page.Locator("#chat-mensagens .msg.tipo-fala .msg-caminho")).ToHaveTextAsync("template · sem LLM");
        await Expect(page.Locator("#chat-mensagens .msg.tipo-fala")).ToContainTextAsync("A capital do Japão é Tóquio.");
    }

    [Fact]
    public async Task Editor_TemOLinkParaOPainel()
    {
        var slug = await app.NovoDiagrama("link");
        var page = await app.Abrir(slug);
        await page.Locator("#link-orquestrador").ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("orquestrador\\.html$"));
        await Expect(page.Locator("body[data-pronto=true]")).ToHaveCountAsync(1);
    }
}
