using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Orquestracao.E2E.Tests;

/// <summary>O chat no navegador conversando com as personas pelo backend em C# (motor "api" falso).</summary>
[Collection("app")]
public class ChatTests(App app)
{
    private async Task<string> Persona(string slug, string papel, double x, string motor = "api")
    {
        await app.Enviar("POST", $"/api/d/{slug}/templates/{papel}", new { motor, x, y = 150 });
        return (string)(await app.Get($"/api/d/{slug}/nodes")).AsArray().Last()!["id"]!;
    }

    private static async Task Comecar(IPage page, string assunto, string modo, int rodadas)
    {
        await page.Locator("#chat-assunto").FillAsync(assunto);
        await page.Locator("#chat-modo").SelectOptionAsync(modo);
        await page.Locator("#chat-rodadas").FillAsync(rodadas.ToString());
        await page.Locator("#chat-novo button[type=submit]").ClickAsync();
    }

    [Fact]
    public async Task DebaterEResponder_AsFalasChegamAoVivo_EASinteseFechaAConversa()
    {
        var slug = await app.NovoDiagrama("chat");
        var a = await Persona(slug, "otimista", 200);
        var m = await Persona(slug, "moderadora", 500);
        await app.Enviar("POST", $"/api/d/{slug}/edges", new { from = a, to = m });
        var page = await app.Abrir(slug, retangulos: 2);
        await Expect(page.Locator($"[data-node={a}]")).ToHaveClassAsync(new Regex(@"\bpersona\b"));

        await Comecar(page, "Abrir uma cafeteria no bairro?", "resposta", 1);
        var mensagens = page.Locator("#chat-mensagens");
        await Expect(mensagens.Locator(".msg.tipo-usuario")).ToContainTextAsync("Abrir uma cafeteria no bairro?");
        await Expect(mensagens.Locator(".msg.tipo-fala")).ToHaveCountAsync(2);
        await Expect(mensagens.Locator(".msg.tipo-sintese")).ToContainTextAsync("Moderadora fala (sintese).");
        await Expect(page.Locator("#chat-custo")).ToContainTextAsync("US$");
        await Expect(page.Locator("#chat-intervir")).ToBeVisibleAsync(); // depois de começar, dá para falar com o grupo
    }

    [Fact]
    public async Task PersonaHumana_AConversaEsperaVoce_EAprovarPeloChatSegue()
    {
        var slug = await app.NovoDiagrama("humana");
        var a = await Persona(slug, "otimista", 200);
        var voce = await Persona(slug, "voce", 500);
        var b = await Persona(slug, "cetica", 800);
        await app.Enviar("POST", $"/api/d/{slug}/edges", new { from = a, to = voce });
        await app.Enviar("POST", $"/api/d/{slug}/edges", new { from = voce, to = b });
        var page = await app.Abrir(slug, retangulos: 3);

        await Comecar(page, "Lançar na sexta?", "debate", 1);
        var espera = page.Locator("#chat-espera");
        await Expect(espera).ToBeVisibleAsync();
        await page.Locator("#ce-texto").FillAsync("pode seguir");
        await espera.Locator("[data-acao=aprovar]").ClickAsync();

        await Expect(espera).ToBeHiddenAsync();
        await Expect(page.Locator("#chat-mensagens .msg.tipo-fala")).ToHaveCountAsync(3);
        await Expect(page.Locator("#chat-mensagens")).ToContainTextAsync("Aprovado. pode seguir");
        await Expect(page.Locator("#chat-mensagens")).ToContainTextAsync("Cética fala (fala).");
    }
}
