using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Orquestracao.Core.Conversas;

namespace Orquestracao.Web.Tests;

/// <summary>Um motor "api" de mentira: responde na hora, sem rede.</summary>
public sealed class ApiFalsa : IMotor
{
    public string Nome => "api";
    public List<Pedido> Pedidos { get; } = [];

    public Task<Disponibilidade> Disponivel(CancellationToken ct = default) => Task.FromResult(new Disponibilidade(true, "falsa"));

    public Task<Resposta> Atender(Pedido p, Action<string> delta, CancellationToken ct)
    {
        lock (Pedidos) Pedidos.Add(p);
        delta("…");
        var texto = p.Tipo == TipoPedido.Saida ? "# Resultado\n\nPronto." : $"{p.Persona.Nome} fala ({p.Tipo}).";
        return Task.FromResult(new Resposta { Texto = texto, Uso = new Uso { Entrada = 10, Saida = 5, Custo = 0.001 } });
    }
}

/// <summary>Portado de legado/server/ai.test.ts: personas, templates, conversas e o MCP pela API HTTP.</summary>
public class IaApiTests : IDisposable
{
    private readonly ApiFalsa _api = new();
    private readonly Servidor _s;
    private const string D = "/d/meu-diagrama";

    public IaApiTests() => _s = new Servidor(servicos: s => s.AddSingleton<IMotor>(_api));

    public void Dispose() => _s.Dispose();

    private async Task<string> Persona(string papel, string motor = "api", double x = 100)
    {
        var r = await _s.Call("POST", $"{D}/templates/{papel}", new { motor, x, y = 100 });
        Assert.Equal(201, r.Status);
        return (string)r.Json!["node"]!["id"]!;
    }

    private async Task<JsonNode> AteStatus(string conversa, params string[] status)
    {
        for (var i = 0; i < 250; i++)
        {
            var c = (await _s.Call("GET", $"{D}/conversas/{conversa}")).Json!;
            if (status.Contains((string?)c["status"])) return c;
            await Task.Delay(20);
        }
        throw new TimeoutException($"a conversa não chegou a {string.Join("/", status)}");
    }

    [Fact]
    public async Task Templates_ListamPapeisEspeciaisEMotores()
    {
        var t = (await _s.Call("GET", "/templates")).Json!;
        Assert.Superset(new HashSet<string?> { "mcp", "ollama", "api" }, t["motores"]!.AsArray().Select(m => (string?)m!["nome"]).ToHashSet());
        Assert.Contains("cetica", t["papeis"]!.AsArray().Select(p => (string?)p!["nome"]));
        Assert.Contains("humana", t["especiais"]!.AsArray().Select(p => (string?)p!["especial"]));
        var motores = (await _s.Call("GET", "/motores")).Json!.AsArray();
        Assert.True((bool)motores.First(m => (string?)m!["nome"] == "api")!["ok"]!);
        Assert.False((bool)motores.First(m => (string?)m!["nome"] == "mcp")!["ok"]!); // nenhum Claude Code conectado
    }

    [Fact]
    public async Task CriarPersonaDoTemplate_EditarOMd_ERemoverORetangulo()
    {
        var r = await _s.Call("POST", $"{D}/templates/cetica", new { motor = "ollama", x = 300, y = 200 });
        Assert.Equal(("Cética", "ollama", "qwen2.5:3b"), ((string?)r.Json!["node"]!["text"], (string?)r.Json["persona"]!["motor"], (string?)r.Json["persona"]!["modelo"]));
        Assert.Equal(404, (await _s.Call("POST", $"{D}/templates/nao-existe", new { motor = "api" })).Status);
        Assert.Equal(400, (await _s.Call("POST", $"{D}/templates/cetica", new { motor = "gpt" })).Status);

        var md = (string)(await _s.Call("GET", $"{D}/personas/n1")).Json!["markdown"]!;
        Assert.Contains("# Cética", md);
        Assert.Equal(400, (await _s.Call("PUT", $"{D}/personas/n1", md.Replace("motor: ollama", "motor: gpt"))).Status);
        var editada = await _s.Call("PUT", $"{D}/personas/n1", md.Replace("motor: ollama", "motor: api").Replace("modelo: qwen2.5:3b", "modelo: claude-haiku-5-5"));
        Assert.Equal("api", (string?)editada.Json!["persona"]!["motor"]);
        Assert.Single((await _s.Call("GET", $"{D}/personas")).Json!.AsArray());
        Assert.Equal(404, (await _s.Call("GET", $"{D}/personas/n9")).Status);

        await _s.Call("DELETE", "/nodes/n1");
        Assert.Single(Directory.GetFiles(Path.Combine(_s.Diagramas, "meu-diagrama", "personas", ".removidos")));
    }

    [Fact]
    public async Task Conversa_PelaApi_DebateESintese_ComEventosNoSse()
    {
        var a = await Persona("otimista");
        var m = await Persona("moderadora", x: 300);
        await _s.Call("POST", "/edges", new { from = a, to = m });
        using var sse = await _s.Ouvir($"{D}/events");
        await sse.Proximo(); // snapshot

        var c = await _s.Call("POST", $"{D}/conversas", new { assunto = "Abrir uma cafeteria?", modo = "resposta", rodadas = 1 });
        Assert.Equal(201, c.Status);
        var id = (string)c.Json!["id"]!;
        var fim = await AteStatus(id, "terminada");
        Assert.Equal("Moderadora fala (sintese).", (string?)fim["resposta"]);
        var tipos = fim["mensagens"]!.AsArray().Select(x => (string?)x!["tipo"]).ToList();
        Assert.Equal(["usuario", "fala", "fala", "sintese"], tipos);
        Assert.Equal(0.001, (double)fim["mensagens"]![1]!["uso"]!["custo"]!);

        var eventos = new List<string?>();
        while (!eventos.Contains("chat.fim")) eventos.Add((string?)(await sse.Proximo())["type"]);
        Assert.Superset(new HashSet<string?> { "chat.inicio", "chat.turno", "chat.delta", "chat.mensagem", "chat.fim" }, eventos.ToHashSet());

        Assert.Single((await _s.Call("GET", $"{D}/conversas")).Json!.AsArray());
        Assert.Equal(400, (await _s.Call("POST", $"{D}/conversas", new { assunto = " " })).Status);
        Assert.Equal(404, (await _s.Call("GET", $"{D}/conversas/c-nada")).Status);

        // Depois do fim, escrever no chat faz alguém responder.
        Assert.Equal(202, (await _s.Call("POST", $"{D}/conversas/{id}/mensagens", new { texto = "@Otimista e o aluguel?" })).Status);
        var depois = await AteStatus(id, "terminada");
        Assert.Equal("Otimista", (string?)depois["mensagens"]!.AsArray().Last()!["nome"]);
    }

    [Fact]
    public async Task PersonaHumana_PelaApi_AprovarSegue_ESaidaGeraOMd()
    {
        var a = await Persona("otimista");
        var voce = await Persona("voce", x: 300);
        var saida = await Persona("saida-readme", x: 500);
        await _s.Call("POST", "/edges", new { from = a, to = voce });
        await _s.Call("POST", "/edges", new { from = voce, to = saida });
        var id = (string)(await _s.Call("POST", $"{D}/conversas", new { assunto = "Lançar?", rodadas = 1 })).Json!["id"]!;

        var esperando = await AteStatus(id, "aguardando");
        Assert.Equal(voce, (string?)esperando["espera"]!["persona"]);
        Assert.Equal(404, (await _s.Call("POST", $"{D}/conversas/c-outra/humano", new { acao = "aprovar" })).Status);
        Assert.Equal(202, (await _s.Call("POST", $"{D}/conversas/{id}/humano", new { acao = "aprovar", texto = "ok" })).Status);
        var fim = await AteStatus(id, "terminada");
        Assert.Equal(409, (await _s.Call("POST", $"{D}/conversas/{id}/humano", new { acao = "aprovar" })).Status); // já não espera
        var arquivo = (string?)fim["mensagens"]!.AsArray().Last(m => (string?)m!["tipo"] == "saida")!["arquivo"];
        Assert.Equal("README.md", arquivo);
        var md = await _s.Call("GET", $"{D}/conversas/{id}/saidas/README.md");
        Assert.Equal(200, md.Status);
        Assert.Contains("# Resultado", md.Texto);
        Assert.Equal(404, (await _s.Call("GET", $"{D}/conversas/{id}/saidas/..%2Fmeta.json")).Status);
        Assert.Equal(404, (await _s.Call("GET", $"{D}/conversas/{id}/saidas/outro.md")).Status);
    }

    [Fact]
    public async Task DuplicarEExportar_LevamAsPersonas()
    {
        await Persona("cetica");
        var copia = (string)(await _s.Call("POST", "/diagrams/meu-diagrama/duplicate")).Json!["slug"]!;
        Assert.Equal("Cética", (string?)(await _s.Call("GET", $"/d/{copia}/personas/n1")).Json!["persona"]!["nome"]);

        var zip = await _s.Call("GET", "/diagrams/meu-diagrama/export");
        using (var lido = new ZipArchive(new MemoryStream(zip.Bytes)))
            Assert.Contains("personas/n1.md", lido.Entries.Select(e => e.FullName));
        var importado = (string)(await _s.Call("POST", "/diagrams/import?name=Trazido", zip.Bytes)).Json!["slug"]!;
        Assert.Equal("api", (string?)(await _s.Call("GET", $"/d/{importado}/personas/n1")).Json!["persona"]!["motor"]);
    }

    [Fact]
    public async Task Memoria_LeGravaEZera()
    {
        var m = (await _s.Call("GET", $"{D}/memoria")).Json!;
        Assert.False((bool)m["existe"]!);
        Assert.Equal(400, (await _s.Call("PUT", $"{D}/memoria", " ")).Status);
        Assert.Equal(1, (int)(await _s.Call("PUT", $"{D}/memoria", "# Memória\n\n## Decisões\n- abrir em março\n")).Json!["itens"]!);
        Assert.True((bool)(await _s.Call("GET", $"{D}/memoria")).Json!["existe"]!);
        await _s.Call("DELETE", $"{D}/memoria");
        Assert.False((bool)(await _s.Call("GET", $"{D}/memoria")).Json!["existe"]!);
    }

    // ---- MCP ----------------------------------------------------------------

    private int _rpc;

    /// <summary>Uma chamada JSON-RPC ao /mcp (Streamable HTTP); a resposta pode vir como JSON ou como SSE.</summary>
    private async Task<JsonNode> Mcp(string metodo, object? parametros = null)
    {
        var corpo = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = ++_rpc, ["method"] = metodo };
        if (parametros is not null) corpo["params"] = System.Text.Json.JsonSerializer.SerializeToNode(parametros);
        var req = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json") };
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        var res = await _s.Cliente.SendAsync(req);
        var texto = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, texto);
        var json = texto.TrimStart().StartsWith('{') ? texto : texto.Split('\n').Last(l => l.StartsWith("data: "))["data: ".Length..];
        var r = JsonNode.Parse(json)!;
        Assert.Null(r["error"]);
        return r["result"]!;
    }

    private async Task<JsonNode> Ferramenta(string nome, object argumentos)
    {
        var r = await Mcp("tools/call", new { name = nome, arguments = argumentos });
        var texto = (string)r["content"]![0]!["text"]!;
        Assert.False((bool?)r["isError"] ?? false, texto);
        return JsonNode.Parse(texto)!;
    }

    [Fact]
    public async Task Mcp_ListaAsFerramentas_EDaVozAsPersonasDoMotorMcp()
    {
        await Mcp("initialize", new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "teste", version = "1" } });
        var nomes = (await Mcp("tools/list"))["tools"]!.AsArray().Select(t => (string?)t!["name"]).ToHashSet();
        Assert.Superset(new HashSet<string?> { "listar_diagramas", "ver_personas", "iniciar_conversa", "proxima_vez", "responder", "ver_conversa", "sintetizar", "parar" }, nomes);

        await Persona("cetica", motor: "mcp");
        Assert.Contains("meu-diagrama", (await Ferramenta("listar_diagramas", new { })).AsArray().Select(d => (string?)d!["slug"]));
        Assert.Equal("mcp", (string?)(await Ferramenta("ver_personas", new { diagrama = "meu-diagrama" }))["personas"]![0]!["motor"]);

        var conversa = (string)(await Ferramenta("iniciar_conversa", new { diagrama = "meu-diagrama", assunto = "Vale a pena?", rodadas = 1 }))["conversa"]!;
        var turno = (await Ferramenta("proxima_vez", new { diagrama = "meu-diagrama", aguardar_s = 5 }))["turnos"]![0]!;
        Assert.Equal(("fala", "Cética", "opus"), ((string?)turno["tipo"], (string?)turno["persona"]!["nome"], (string?)turno["persona"]!["subagente_sugerido"]));
        await Ferramenta("responder", new { turno = (string)turno["turno"]!, texto = "Depende do aluguel." });

        var fim = await AteStatus(conversa, "terminada");
        Assert.Equal("Depende do aluguel.", (string?)fim["mensagens"]![1]!["texto"]);
        Assert.Equal("Depende do aluguel.", (string?)(await Ferramenta("ver_conversa", new { diagrama = "meu-diagrama" }))["mensagens"]![1]!["texto"]);
        Assert.True((bool)(await _s.Call("GET", "/motores")).Json!.AsArray().First(m => (string?)m!["nome"] == "mcp")!["ok"]!); // agora conectado
    }
}
