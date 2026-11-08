using System.Text.Json.Nodes;

namespace Orquestracao.Web.Tests;

/// <summary>Portado de legado/server/api.test.ts: a API granular de um diagrama (rotas sem prefixo = o primeiro).</summary>
public class ApiTests : IDisposable
{
    private const string Sample = """
        flowchart LR
            n1("Início")
            n2("Meio")
            n1 -->|"processa"| n2
        %% @pos n1 100 100
        %% @pos n2 300 100
        """;

    private readonly Servidor _s = new();

    public void Dispose() => _s.Dispose();

    internal static void Igual(string esperado, JsonNode? atual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(esperado), atual), $"esperado {esperado}\nveio     {atual?.ToJsonString()}");

    [Fact]
    public async Task ComecaVazia_ECriaRetangulosESetasUmAUm()
    {
        // Um diagrama sem diagram.json ainda não tem código: 204.
        Directory.CreateDirectory(Path.Combine(_s.Diagramas, "vazio"));
        File.WriteAllText(Path.Combine(_s.Diagramas, "vazio", "meta.json"), """{"name":"Vazio","createdAt":"2026-01-01T00:00:00.000Z"}""");
        Assert.Equal(204, (await _s.Call("GET", "/d/vazio/diagram")).Status);

        var a = await _s.Call("POST", "/nodes", new { text = "Início", x = 100, y = 100 });
        Assert.Equal(201, a.Status);
        Igual("""{"id":"n1","text":"Início","active":false,"owner":null,"x":100,"y":100}""", a.Json);
        await _s.Call("POST", "/nodes", new { text = "Meio" });
        var e = await _s.Call("POST", "/edges", new { from = "Início", to = "meio", text = "processa" });
        Igual("""{"id":"e1","from":"n1","to":"n2","text":"processa"}""", e.Json);
        var loop = await _s.Call("POST", "/edges", new { from = "n2", to = "n2" });
        Assert.Equal(("n2", "n2"), ((string?)loop.Json!["from"], (string?)loop.Json!["to"]));
        Assert.Equal(4, loop.Rev);
        Assert.Contains("n2 --> n2", (await _s.Call("GET", "/diagram")).Texto);
    }

    [Fact]
    public async Task Patch_AlteraSoORetanguloPedido()
    {
        await _s.Call("PUT", "/diagram", Sample);
        var before = (await _s.Call("GET", "/diagram.json")).Json!["diagram"]!;
        var r = await _s.Call("PATCH", "/nodes/n1", new { text = "Começo" });
        Igual("""{"id":"n1","text":"Começo","active":false,"owner":null,"x":100,"y":100}""", r.Json);
        var after = (await _s.Call("GET", "/diagram.json")).Json!["diagram"]!;
        Assert.True(JsonNode.DeepEquals(before["nodes"]![1], after["nodes"]![1]));
        Assert.True(JsonNode.DeepEquals(before["edges"], after["edges"]));
        var esperado = before["nodes"]![0]!.DeepClone();
        esperado["text"] = "Começo";
        Assert.True(JsonNode.DeepEquals(esperado, after["nodes"]![0]));
    }

    [Fact]
    public async Task AtivarDesativarAlternar_ContinuamFuncionando()
    {
        await _s.Call("PUT", "/diagram", Sample);
        Assert.True((bool)(await _s.Call("POST", "/nodes/Meio/ativar")).Json!["active"]!);
        Assert.False((bool)(await _s.Call("POST", "/nodes/n2/alternar")).Json!["active"]!);
        Assert.True((bool)(await _s.Call("PATCH", "/nodes/n2", new { active = true })).Json!["active"]!);
        Assert.Contains("class n2 ativo", (await _s.Call("GET", "/diagram")).Texto);
    }

    [Fact]
    public async Task Responsavel_DefineTrocaERemove_SoNaqueleRetangulo()
    {
        await _s.Call("PUT", "/diagram", Sample);
        var r = await _s.Call("PATCH", "/nodes/Meio", new { owner = "  Maria   Souza ", active = true });
        Assert.Equal(("n2", true, "Maria Souza"), ((string?)r.Json!["id"], (bool)r.Json["active"]!, (string?)r.Json["owner"]));
        Assert.Null((await _s.Call("GET", "/nodes/n1")).Json!["owner"]);
        Assert.Contains("n2(\"Meio<br/><small>👤 Maria Souza</small>\")", (await _s.Call("GET", "/diagram")).Texto);
        Assert.Equal("João", (string?)(await _s.Call("PATCH", "/nodes/n2", new { owner = "João" })).Json!["owner"]);
        Assert.Null((await _s.Call("PATCH", "/nodes/n2", """{"owner":null}""")).Json!["owner"]);
        Assert.Equal(200, (await _s.Call("PATCH", "/nodes/n2", new { owner = "x" })).Status);
        Assert.Null((await _s.Call("PATCH", "/nodes/n2", new { owner = "" })).Json!["owner"]);
        Assert.DoesNotContain("👤", (await _s.Call("GET", "/diagram")).Texto);
        Assert.Equal(400, (await _s.Call("PATCH", "/nodes/n2", new { owner = 42 })).Status);
        Assert.Equal(400, (await _s.Call("PATCH", "/nodes/n2", new { owner = new string('x', 61) })).Status);
        Assert.Equal("Ana", (string?)(await _s.Call("POST", "/nodes", new { text = "Novo", owner = "Ana" })).Json!["owner"]);
    }

    [Fact]
    public async Task PutDeCodigo_LeOResponsavelDoTextoEDoComentarioAntigo()
    {
        await _s.Call("PUT", "/diagram", Sample.Replace("n1(\"Início\")", "n1(\"Início<br/><small>👤 Ana Lima</small>\")"));
        var n1 = (await _s.Call("GET", "/nodes/n1")).Json!;
        Assert.Equal(("Início", "Ana Lima"), ((string?)n1["text"], (string?)n1["owner"]));
        await _s.Call("PUT", "/diagram", Sample + "\n%% @owner n2 Bia");
        Assert.Equal("Bia", (string?)(await _s.Call("GET", "/nodes/n2")).Json!["owner"]);
        Assert.Contains("n2(\"Meio<br/><small>👤 Bia</small>\")", (await _s.Call("GET", "/diagram")).Texto);
    }

    [Fact]
    public async Task DeleteDeRetangulo_RemoveAsSetasDele()
    {
        await _s.Call("PUT", "/diagram", Sample);
        var r = await _s.Call("DELETE", "/nodes/n2");
        Igual("""{"type":"node.removed","id":"n2","edges":["e1"]}""", r.Json);
        Igual("[]", (await _s.Call("GET", "/edges")).Json);
        Assert.Equal(404, (await _s.Call("DELETE", "/edges/e1")).Status);
    }

    [Fact]
    public async Task PutDeCodigo_MantemOsIdsDasSetasExistentes()
    {
        await _s.Call("PUT", "/diagram", Sample);
        await _s.Call("PATCH", "/edges/e1", new { text = "vai" });
        await _s.Call("PUT", "/diagram", Sample + "\n    n2 --> n1");
        var ids = (await _s.Call("GET", "/edges")).Json!.AsArray().Select(e => (string?)e!["id"]);
        Assert.Equal(["e1", "e2"], ids);
    }

    [Fact]
    public async Task Erros_400_404_409()
    {
        await _s.Call("PUT", "/diagram", Sample);
        Assert.Equal(400, (await _s.Call("PATCH", "/nodes/n1", "{quebrado")).Status);
        Assert.Equal(400, (await _s.Call("PATCH", "/nodes/n1", new { cor = "azul" })).Status);
        Assert.Equal(404, (await _s.Call("PATCH", "/nodes/nada", new { text = "x" })).Status);
        Assert.Equal(409, (await _s.Call("POST", "/edges", new { from = "n1", to = "n2" })).Status);
        Assert.Equal(409, (await _s.Call("POST", "/nodes", new { id = "n1" })).Status);
        var semRota = await _s.Call("PATCH", "/nodes", new { });
        Assert.Equal(404, semRota.Status);
        Assert.Contains("Rota não encontrada", (string?)semRota.Json!["error"]);
    }

    [Fact]
    public async Task PersisteEmJson_EMigraOMmdAntigo()
    {
        using var s = new Servidor();
        var antigo = Path.Combine(s.Dir, "diagram.json");
        File.WriteAllText(Path.ChangeExtension(antigo, ".mmd"), Sample);
        using var comAntigo = new Servidor(arquivoAntigo: antigo);
        Assert.Equal(["Início", "Meio"], (await comAntigo.Call("GET", "/nodes")).Json!.AsArray().Select(n => (string?)n!["text"]));
        await comAntigo.Call("POST", "/nodes/n1/ativar");
        var salvo = JsonNode.Parse(File.ReadAllText(Path.Combine(comAntigo.Diagramas, "diagrama", "diagram.json")))!;
        Assert.Equal(1, (int)salvo["rev"]!);
        Assert.Equal(("n1", true), ((string?)salvo["diagram"]!["nodes"]![0]!["id"], (bool)salvo["diagram"]!["nodes"]![0]!["active"]!));
    }

    [Fact]
    public async Task SetaAnimadaEPulso_OPulsoESoUmEvento_SemSalvarNemSubirORev()
    {
        await _s.Call("PUT", "/diagram", Sample);
        Assert.True((bool)(await _s.Call("PATCH", "/edges/e1", new { animated = true })).Json!["animated"]!);
        Assert.Contains("e1@{ animate: true }", (await _s.Call("GET", "/diagram")).Texto);
        var rev = (int)(await _s.Call("GET", "/diagram.json")).Json!["rev"]!;

        using (var sse = await _s.Ouvir("/d/meu-diagrama/events"))
        {
            await sse.Proximo(); // snapshot
            Assert.Equal(204, (await _s.Call("POST", "/edges/e1/pulse", new { label = "pedido #42" })).Status);
            Igual("""{"type":"edge.pulse","id":"e1","label":"pedido #42","source":"api"}""", await sse.Proximo());
        }

        Assert.Equal(rev, (int)(await _s.Call("GET", "/diagram.json")).Json!["rev"]!);
        Assert.Equal(404, (await _s.Call("POST", "/edges/e9/pulse")).Status);
        Assert.Equal(400, (await _s.Call("POST", "/edges/e1/pulse", new { label = 7 })).Status);
    }

    [Fact]
    public async Task Sse_SnapshotAoConectar_EDepoisSoOEventoDeCadaMudanca()
    {
        await _s.Call("PUT", "/diagram", Sample);
        using var sse = await _s.Ouvir("/d/meu-diagrama/events");
        Assert.Equal("text/event-stream", sse.Http.Content.Headers.ContentType?.MediaType);
        var snapshot = await sse.Proximo();
        Assert.Equal(("snapshot", 1), ((string?)snapshot["type"], (int)snapshot["rev"]!));
        await _s.Call("PATCH", "/nodes/n2", new { x = 333 }, clientId: "aba-1");
        Igual("""{"type":"node.updated","node":{"id":"n2","text":"Meio","x":333,"y":100},"rev":2,"source":"aba-1"}""", await sse.Proximo());
    }
}
