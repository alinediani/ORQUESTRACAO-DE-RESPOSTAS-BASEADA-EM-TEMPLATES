using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Orquestracao.Core.Conversas;

namespace Orquestracao.Web.Tests;

/// <summary>Um motor "api" de mentira que responde algumas perguntas factuais (para o orquestrador extrair o template).</summary>
public sealed class ApiFactual : IMotor
{
    private static readonly (string Chave, string Resposta)[] Respostas =
    [
        ("capital da França", "A capital da França é Paris."),
        ("moeda do Japão", "A moeda do Japão é o iene."),
    ];

    public int Chamadas;
    public string Nome => "api";

    public Task<Disponibilidade> Disponivel(CancellationToken ct = default) => Task.FromResult(new Disponibilidade(true, "factual"));

    public Task<Resposta> Atender(Pedido p, Action<string> delta, CancellationToken ct)
    {
        Interlocked.Increment(ref Chamadas);
        var texto = Respostas.FirstOrDefault(r => p.Contexto.Contains(r.Chave)).Resposta ?? "Não sei.";
        return Task.FromResult(new Resposta { Texto = texto, Uso = new Uso { Entrada = 80, Saida = 12, Custo = 0.0015 } });
    }
}

/// <summary>O orquestrador pela API HTTP, no diagrama da Figura 1, pelo MCP (R06) e como persona numa conversa.</summary>
public class OrquestradorApiTests : IDisposable
{
    private readonly ApiFactual _api = new();
    private readonly Servidor _s;

    public OrquestradorApiTests() => _s = new Servidor(servicos: s => s.AddSingleton<IMotor>(_api), diagramaDaFigura: true);

    public void Dispose() => _s.Dispose();

    private Task<Servidor.Resposta> Perguntar(string pergunta) => _s.Call("POST", "/orquestrador/perguntar", new { pergunta });

    [Fact]
    public async Task Perguntar_ChaveNovaVaiAoLlm_ADepoisSaiDoTemplate_EFeedbackMudaOPeso()
    {
        var r1 = (await Perguntar("Qual é a capital da França?")).Json!;
        Assert.Equal(("llm", "capital {pais}", "A capital da França é Paris.", true), ((string?)r1["caminho"], (string?)r1["chave"], (string?)r1["texto"], (bool)r1["templateSalvo"]!));
        var r2 = (await Perguntar("Me diga a capital do Japão")).Json!;
        Assert.Equal(("template", "A capital do Japão é Tóquio."), ((string?)r2["caminho"], (string?)r2["texto"]));
        Assert.Equal(1, _api.Chamadas);

        var estado = (await _s.Call("GET", "/orquestrador")).Json!;
        Assert.Equal((2, 1, 1, 0.5), ((int)estado["estatisticas"]!["perguntas"]!, (int)estado["estatisticas"]!["acertosTemplate"]!, (int)estado["estatisticas"]!["chamadasGeracao"]!, (double)estado["estatisticas"]!["taxaAcertoTemplate"]!));
        Assert.Equal("A capital {de_entidade} é {capital}.", (string?)estado["templates"]![0]!["template"]);
        Assert.Equal(58, (int)estado["fonte"]!["entidades"]!);
        Assert.Equal(0.5, (double)estado["configuracao"]!["pesoInicial"]!);

        var f = (await _s.Call("POST", "/orquestrador/feedback", new { chave = "capital {pais}", bom = false })).Json!;
        Assert.Equal(0.35, (double)f["template"]!["peso"]!, 6);
        Assert.Equal(400, (await _s.Call("POST", "/orquestrador/feedback", new { chave = "capital {pais}" })).Status);
        Assert.Equal(400, (await Perguntar(" ")).Status);
        Assert.Equal(400, (await _s.Call("POST", "/orquestrador/perguntar", new { pergunta = "x", motor = "gpt" })).Status);

        Assert.Equal(404, (await _s.Call("DELETE", "/orquestrador/templates/nada")).Status);
        Assert.Equal(204, (await _s.Call("DELETE", "/orquestrador/templates/capital%20%7Bpais%7D")).Status);
        Assert.Empty((await _s.Call("GET", "/orquestrador")).Json!["templates"]!.AsArray());
    }

    [Fact]
    public async Task CadaEtapa_PulsaASetaDoDiagramaDaFigura1_EChegaAoPainel()
    {
        Assert.Contains("orquestrador-por-template", (await _s.Call("GET", "/diagrams")).Json!.AsArray().Select(d => (string?)d!["slug"]));
        Assert.Equal(12, (await _s.Call("GET", "/d/orquestrador-por-template/nodes")).Json!.AsArray().Count);
        Assert.Empty((await _s.Call("GET", "/d/orquestrador-por-template/personas")).Json!.AsArray()); // etapas, não personas
        Assert.Equal(400, (await _s.Call("POST", "/d/orquestrador-por-template/conversas", new { assunto = "x" })).Status);

        using var canvas = await _s.Ouvir("/d/orquestrador-por-template/events");
        await canvas.Proximo(); // snapshot
        using var painel = await _s.Ouvir("/orquestrador/events");
        await Perguntar("Qual é a capital da França?");

        var pulsos = new List<string?>();
        for (var i = 0; i < 6; i++)
        {
            var ev = await canvas.Proximo();
            Assert.Equal(("edge.pulse", "orquestrador"), ((string?)ev["type"], (string?)ev["source"]));
            pulsos.Add((string?)ev["id"]);
        }
        Assert.Equal(["ui-parser", "parser-motor", "motor-llm", "llm-extrai", "extrai-salva", "salva-entrega"], pulsos);

        var tipos = new List<string?>();
        while (!tipos.Contains("resultado")) tipos.Add((string?)(await painel.Proximo())["type"]);
        Assert.Equal(6, tipos.Count(t => t == "etapa"));
    }

    // ---- MCP (R06) -----------------------------------------------------------

    private async Task<JsonNode> Ferramenta(string nome, object argumentos)
    {
        var corpo = new JsonObject
        {
            ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = nome, ["arguments"] = System.Text.Json.JsonSerializer.SerializeToNode(argumentos) },
        };
        var req = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json") };
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        var texto = await (await _s.Cliente.SendAsync(req)).Content.ReadAsStringAsync();
        var json = texto.TrimStart().StartsWith('{') ? texto : texto.Split('\n').Last(l => l.StartsWith("data: "))["data: ".Length..];
        var r = JsonNode.Parse(json)!["result"]!;
        Assert.False((bool?)r["isError"] ?? false, (string?)r["content"]![0]!["text"]);
        return JsonNode.Parse((string)r["content"]![0]!["text"]!)!;
    }

    [Fact]
    public async Task R06_PeloMcp_OClaudeCodePerguntaAvaliaEVeAsEstatisticas()
    {
        var r = await Ferramenta("perguntar_orquestrador", new { pergunta = "Qual a moeda do Japão?" });
        Assert.Equal(("llm", "moeda {pais}"), ((string?)r["caminho"], (string?)r["chave"]));
        Assert.Equal("template", (string?)(await Ferramenta("perguntar_orquestrador", new { pergunta = "Qual a moeda da França?" }))["caminho"]);
        var f = await Ferramenta("avaliar_resposta", new { chave = "moeda {pais}", bom = true });
        Assert.Equal(0.65, (double)f["template"]!["peso"]!, 6);
        var estado = await Ferramenta("ver_orquestrador", new { });
        Assert.Equal(2, (int)estado["estatisticas"]!["perguntas"]!);
    }

    // ---- como persona numa conversa ---------------------------------------------

    private async Task<JsonNode> Conversa(string assunto)
    {
        var id = (string)(await _s.Call("POST", "/d/meu-diagrama/conversas", new { assunto, rodadas = 1 })).Json!["id"]!;
        for (var i = 0; i < 250; i++)
        {
            var c = (await _s.Call("GET", $"/d/meu-diagrama/conversas/{id}")).Json!;
            if ((string?)c["status"] == "terminada") return c;
            await Task.Delay(20);
        }
        throw new TimeoutException();
    }

    [Fact]
    public async Task PersonaComMotorTemplate_RespondeNaConversa_EODetalheVaiNaMensagem()
    {
        var r = await _s.Call("POST", "/d/meu-diagrama/templates/respondedor", new { motor = "template" });
        Assert.Equal(("template", "api", "claude-sonnet-5-5"), ((string?)r.Json!["persona"]!["motor"], (string?)r.Json["persona"]!["fallback"], (string?)r.Json["persona"]!["modelo"]));
        Assert.Contains("template", (await _s.Call("GET", "/templates")).Json!["motores"]!.AsArray().Select(m => (string?)m!["nome"]));

        var c1 = await Conversa("Qual é a capital da França?");
        var m1 = c1["mensagens"]!.AsArray().Last()!;
        Assert.Equal(("A capital da França é Paris.", "template", "llm"), ((string?)m1["texto"], (string?)m1["motor"], (string?)m1["detalhe"]!["caminho"]));
        var m2 = (await Conversa("Qual é a capital do Peru?"))["mensagens"]!.AsArray().Last()!;
        Assert.Equal(("A capital do Peru é Lima.", "template", 0.0), ((string?)m2["texto"], (string?)m2["detalhe"]!["caminho"], (double)m2["uso"]!["custo"]!));
        Assert.Equal(1, _api.Chamadas);
    }

    [Fact]
    public async Task Experimento_RodaNoServidor_EGravaRelatorioEPlanilhasDaAvaliacaoHumana()
    {
        var saida = Path.Combine(_s.Dir, "experimento");
        using var s = new Servidor(servicos: x => x.AddSingleton<IMotor>(new ApiFactual()), configuracoes: new() { ["Experimento:Saida"] = saida });
        Assert.Equal(400, (await s.Call("POST", "/experimento", new { llm = "gpt" })).Status);
        Assert.Equal("parado", (string?)(await s.Call("GET", "/experimento")).Json!["status"]);

        Assert.Equal(202, (await s.Call("POST", "/experimento", new { llm = "api", perguntas = 12 })).Status);
        JsonNode st = null!;
        for (var i = 0; i < 250; i++)
        {
            st = (await s.Call("GET", "/experimento")).Json!;
            if ((string?)st["status"] != "rodando") break;
            await Task.Delay(20);
        }
        Assert.Equal("concluido", (string?)st["status"]);
        Assert.Equal(12, (int)st["resumo"]!["linhaDeBase"]!["chamadasGeracao"]!);
        foreach (var f in new[] { "resultados-api.md", "resultados-api.json", "avaliacao-humana.csv", "avaliacao-humana-chave.csv", "experimento-api.db" })
            Assert.True(File.Exists(Path.Combine(saida, f)), f);
    }

    [Fact]
    public async Task AvaliacaoHumana_ParesSorteados_EAChaveDesembaralha()
    {
        var fonte = Orquestracao.Templates.FonteDeFatosJson.Ler(Orquestracao.Templates.FonteDeFatosJson.Achar(null, AppContext.BaseDirectory));
        var conjunto = Orquestracao.Templates.Experimento.GerarConjunto(fonte, 2026, 12);
        var r = await Orquestracao.Templates.Experimento.Rodar(fonte, conjunto, new Orquestracao.Templates.LlmDeFatos(fonte), new Orquestracao.Templates.LlmDeFatos(fonte),
            new Orquestracao.Templates.JuizPorGabarito(), "simulado", 2026, 0, 0);
        var (planilha, chave) = Orquestracao.Templates.AvaliacaoHumana.Gerar(r, 2026);
        var linhas = planilha.TrimStart('\uFEFF').Trim().Split('\n');
        Assert.Equal(13, linhas.Length);
        Assert.StartsWith("id;pergunta;resposta_a;resposta_b;melhor", linhas[0]);
        Assert.DoesNotContain("prototipo", planilha); // cega: a planilha não diz de onde veio cada resposta
        var k = chave.TrimStart('\uFEFF').Trim().Split('\n').Skip(1).Select(l => l.Split(';')).ToList();
        Assert.All(k, c => Assert.Contains(c[1], new[] { "prototipo", "linha_de_base" }));
        Assert.True(k.Select(c => c[1]).Distinct().Count() == 2); // as duas ordens aparecem
    }
}
