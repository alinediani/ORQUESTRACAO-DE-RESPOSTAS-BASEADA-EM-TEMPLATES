using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Orquestracao.Core.Conversas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Orquestracao.E2E.Tests;

/// <summary>
/// O app de verdade (Kestrel numa porta livre, pasta de diagramas temporária) e um Chromium headless.
/// Um por coleção de testes: cada teste cria o próprio diagrama e abre uma página nova.
/// </summary>
public sealed class App : IAsyncLifetime
{
    public string Dir { get; } = Directory.CreateTempSubdirectory("orquestracao-e2e-").FullName;
    public string Url { get; private set; } = "";
    public HttpClient Api { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    private WebApplicationFactory<Program> _fabrica = null!;
    private IPlaywright _playwright = null!;

    public async Task InitializeAsync()
    {
        _fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Diagramas:Pasta", Path.Combine(Dir, "diagrams"));
            // A API da Anthropic é trocada por um motor falso: os testes não usam rede nem chave.
            b.ConfigureTestServices(s => s.AddSingleton<IMotor>(new ApiFalsa()));
        });
        _fabrica.UseKestrel(0); // porta livre, escolhida pelo sistema
        _fabrica.StartServer();
        // O endereço real (com a porta) que o Kestrel publicou.
        var enderecos = _fabrica.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;
        Url = enderecos.First().Replace("[::]", "localhost").Replace("0.0.0.0", "localhost").Replace("127.0.0.1", "localhost").TrimEnd('/');
        Api = new HttpClient { BaseAddress = new Uri(Url) };

        // Baixa o Chromium na primeira vez (no CI ou numa máquina nova); depois é instantâneo.
        var codigo = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (codigo != 0) throw new InvalidOperationException($"playwright install chromium saiu com {codigo}");
        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        _playwright.Dispose();
        Api.Dispose();
        await _fabrica.DisposeAsync();
        try
        {
            Directory.Delete(Dir, recursive: true);
        }
        catch (IOException) { }
    }

    // ---- ajudantes ---------------------------------------------------------

    /// <summary>Cria um diagrama novo (nome único por teste) e devolve o slug.</summary>
    public async Task<string> NovoDiagrama(string nome)
    {
        var res = await Api.PostAsJsonAsync("/api/diagrams", new { name = $"{nome} {Guid.NewGuid().ToString("N")[..6]}" });
        res.EnsureSuccessStatusCode();
        return (string)(await res.Content.ReadFromJsonAsync<JsonObject>())!["slug"]!;
    }

    public async Task<JsonNode> Get(string rota) => (await Api.GetFromJsonAsync<JsonNode>(rota))!;

    public async Task Enviar(string metodo, string rota, object corpo)
    {
        var res = await Api.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), rota) { Content = JsonContent.Create(corpo) });
        res.EnsureSuccessStatusCode();
    }

    /// <summary>Abre o diagrama no navegador e espera o canvas mostrar <paramref name="retangulos"/> retângulos.</summary>
    public async Task<IPage> Abrir(string slug, int retangulos = 0)
    {
        var page = await Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = 1400, Height = 900 } });
        var erros = new List<string>();
        page.PageError += (_, e) => erros.Add(e);
        await page.GotoAsync($"{Url}/?d={slug}");
        await Assertions.Expect(page.Locator("[data-node]")).ToHaveCountAsync(retangulos);
        // O painel de código já recebeu o código gerado no servidor (senão ele chega depois e sobrescreve o que o teste digitar).
        await Assertions.Expect(page.Locator("#code")).ToHaveValueAsync(new System.Text.RegularExpressions.Regex("^flowchart LR"));
        Assert.Empty(erros);
        return page;
    }

    /// <summary>Repete a leitura até a condição valer (o navegador grava no servidor de forma assíncrona).</summary>
    public static async Task<T> Esperar<T>(Func<Task<T>> ler, Func<T, bool> ok, int timeoutMs = 5000)
    {
        var fim = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (true)
        {
            var v = await ler();
            if (ok(v) || DateTime.UtcNow > fim) return v;
            await Task.Delay(100);
        }
    }
}

/// <summary>O motor "api" de mentira: responde na hora, devagar o bastante para o chat mostrar "pensando".</summary>
public sealed class ApiFalsa : IMotor
{
    public string Nome => "api";

    public Task<Disponibilidade> Disponivel(CancellationToken ct = default) => Task.FromResult(new Disponibilidade(true, "falsa"));

    /// <summary>Respostas factuais para as perguntas que os testes do orquestrador fazem.</summary>
    private static readonly (string Trecho, string Resposta)[] Fatos =
    [
        ("capital da França", "A capital da França é Paris."),
        ("capital do Japão", "A capital do Japão é Tóquio."),
        ("moeda da Argentina", "A moeda da Argentina é o peso argentino."),
        ("símbolo químico do ferro", "O símbolo químico do ferro é Fe."),
    ];

    public async Task<Resposta> Atender(Pedido p, Action<string> delta, CancellationToken ct)
    {
        // Pedido do orquestrador por template (o system de geração pede uma frase factual): responde com o fato.
        var fato = p.System.Contains("uma única frase") ? Fatos.FirstOrDefault(f => p.Contexto.Contains(f.Trecho)).Resposta : null;
        var texto = fato ?? $"{p.Persona.Nome} fala ({p.Tipo}).";
        foreach (var parte in texto.Split(' '))
        {
            delta(parte + " ");
            await Task.Delay(30, ct);
        }
        return new Resposta { Texto = texto, Uso = new Uso { Entrada = 100, Saida = 20, Custo = 0.0042 } };
    }
}

[CollectionDefinition("app")]
public sealed class AppCollection : ICollectionFixture<App>;
