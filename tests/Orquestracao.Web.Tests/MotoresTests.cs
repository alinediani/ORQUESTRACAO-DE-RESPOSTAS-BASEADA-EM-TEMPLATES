using System.Net;
using System.Text;
using Anthropic.Models.Messages;
using Orquestracao.Core.Conversas;
using Orquestracao.Motores;

namespace Orquestracao.Web.Tests;

/// <summary>Portado de legado/server/motores/*.test.ts: os motores sem rede (custo, parâmetros, Ollama falso, MCP).</summary>
public class MotoresTests
{
    internal static Pedido Pedido(string motor = "api", string yaml = "", string id = "t-1", string diagrama = "d") => new()
    {
        Id = id, Conversa = "c-1", Diagrama = diagrama, Tipo = TipoPedido.Fala, System = "Você é a Cética.", Contexto = "Assunto: café",
        Persona = Personas.Ler("n1", $"# Cética\n\nPrompt.\n\n```yaml\nmotor: {motor}\n{yaml}\n```", "Cética"),
    };

    [Fact]
    public void Api_CustoPorModelo_ComCache()
    {
        Assert.Equal(4 * 1000 / 1e6 + 20 * 500 / 1e6, MotorApi.CustoDe("claude-opus-5-5", 1000, 500), 9);
        // Leitura de cache a 0,2/M; escrita de cache custa 1,25× a entrada.
        Assert.Equal((100 * 1.25 * 4 + 1000 * 0.2) / 1e6, MotorApi.CustoDe("claude-opus-5-5", 0, 0, cacheLido: 1000, cacheEscrito: 100), 9);
        Assert.Equal(0, MotorApi.CustoDe("modelo-desconhecido", 1000, 1000));
    }

    [Fact]
    public void Api_Parametros_ModeloEsforcoEWebSearch()
    {
        var p = MotorApi.Parametros(Pedido(yaml: "modelo: claude-sonnet-5-5\nesforco: high"));
        Assert.Equal("claude-sonnet-5-5", p.Model.Raw());
        Assert.Equal(64000, p.MaxTokens);
        Assert.Equal(Effort.High, p.OutputConfig!.Effort!.Value());
        Assert.Null(p.Tools);
        Assert.NotNull(MotorApi.Parametros(Pedido(yaml: "ferramentas: [web_search]")).Tools);
    }

    [Fact]
    public async Task Api_SemCredencial_FicaIndisponivel_EOTurnoFalhaComAMensagem()
    {
        var m = new MotorApi(credencial: () => false);
        var d = await m.Disponivel();
        Assert.False(d.Ok);
        Assert.Contains("ANTHROPIC_API_KEY", d.Detalhe);
        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => m.Atender(Pedido(), _ => { }, CancellationToken.None));
        Assert.Contains("sem credencial", e.Message);
    }

    /// <summary>Um Ollama falso: /api/tags e /api/chat em NDJSON.</summary>
    private sealed class OllamaFalso(string? modeloAusente = null) : HttpMessageHandler
    {
        public List<string> Corpos { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            if (req.RequestUri!.AbsolutePath == "/api/tags")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"models":[{"name":"qwen2.5:3b"}]}""") };
            Corpos.Add(await req.Content!.ReadAsStringAsync(ct));
            if (modeloAusente is not null) return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("not found") };
            var ndjson = """
                {"message":{"content":"Olá"},"done":false}
                {"message":{"content":", mundo"},"done":false}
                {"message":{"content":""},"done":true,"prompt_eval_count":12,"eval_count":20,"eval_duration":4000000000}
                """;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ndjson, Encoding.UTF8, "application/x-ndjson") };
        }
    }

    [Fact]
    public async Task Ollama_FalaEmStreaming_EContaTokensPorSegundo()
    {
        var falso = new OllamaFalso();
        var m = new MotorOllama(["http://ollama.falso"], new HttpClient(falso));
        var pedacos = new List<string>();
        var r = await m.Atender(Pedido("ollama"), pedacos.Add, CancellationToken.None);
        Assert.Equal("Olá, mundo", r.Texto);
        Assert.Equal(["Olá", ", mundo"], pedacos);
        Assert.Equal((12L, 20L, 5.0, 0.0), (r.Uso!.Entrada, r.Uso.Saida, r.Uso.TokensPorSegundo!.Value, r.Uso.Custo));
        Assert.Contains("\"model\":\"qwen2.5:3b\"", falso.Corpos[0]);
        Assert.Contains("\"role\":\"system\"", falso.Corpos[0]);
        var d = await m.Disponivel();
        Assert.True(d.Ok);
        Assert.Contains("qwen2.5:3b", d.Detalhe);
    }

    [Fact]
    public async Task Ollama_ModeloAusenteOuForaDoAr_DizComoResolver()
    {
        var m = new MotorOllama(["http://ollama.falso"], new HttpClient(new OllamaFalso(modeloAusente: "x")));
        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => m.Atender(Pedido("ollama"), _ => { }, CancellationToken.None));
        Assert.Contains("ollama pull qwen2.5:3b", e.Message);

        var fora = new MotorOllama(["http://127.0.0.1:1"]);
        var d = await fora.Disponivel();
        Assert.False(d.Ok);
        Assert.Contains("Ollama fora do ar", d.Detalhe);
    }

    [Fact]
    public void Ollama_AchaOWindowsPeloGatewayDoWsl()
    {
        const string rotas = "Iface\tDestination\tGateway\tFlags\neth0\t00000000\t0100A8C0\t0003\neth0\t0000A8C0\t00000000\t0001\n";
        Assert.Equal("http://192.168.0.1:11434", MotorOllama.GatewayDoWsl(rotas));
        Assert.Null(MotorOllama.GatewayDoWsl("Iface\tDestination\tGateway\n"));
    }

    [Fact]
    public async Task Mcp_OTurnoFicaPendente_AteOClienteResponder()
    {
        var m = new MotorMcp();
        Assert.False((await m.Disponivel()).Ok);
        m.Tocar();
        Assert.True((await m.Disponivel()).Ok);

        var esperando = m.Aguardar("d", TimeSpan.FromSeconds(5));
        var resposta = m.Atender(Pedido("mcp", id: "t-9"), _ => { }, CancellationToken.None);
        Assert.Equal(["t-9"], (await esperando).Select(p => p.Id)); // acordou com o turno novo
        Assert.Empty(m.Pendentes("outro-diagrama"));
        Assert.Throws<InvalidOperationException>(() => m.Responder("t-nao-existe", "x"));
        m.Responder("t-9", "minha fala", [new Delegacao("Redatora", "escrever")]);
        var r = await resposta;
        Assert.Equal(("minha fala", "Redatora"), (r.Texto, r.Delegacoes![0].Para));
        Assert.Empty(m.Pendentes());
    }

    [Fact]
    public async Task Mcp_PararAConversa_CancelaOTurnoPendente()
    {
        var m = new MotorMcp();
        using var cts = new CancellationTokenSource();
        var resposta = m.Atender(Pedido("mcp"), _ => { }, cts.Token);
        Assert.Single(m.Pendentes());
        cts.Cancel();
        await Assert.ThrowsAsync<InvalidOperationException>(() => resposta);
        Assert.Empty(m.Pendentes());
        Assert.Empty(await m.Aguardar(null, TimeSpan.FromMilliseconds(50)));
    }
}
