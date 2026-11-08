using System.Collections.Concurrent;
using Orquestracao.Core.Conversas;
using Orquestracao.Core.Diagramas;
using Orquestracao.Templates;
using static Orquestracao.Templates.Tests.Base;

namespace Orquestracao.Templates.Tests;

/// <summary>A base chave-valor (SQLite e memória) e o orquestrador como motor de persona numa conversa.</summary>
public class StoreEMotorTests
{
    [Theory]
    [InlineData("sqlite")]
    [InlineData("memoria")]
    public void Store_GuardaAtualizaListaERemove(string tipo)
    {
        var dir = Directory.CreateTempSubdirectory("kv-").FullName;
        try
        {
            ITemplateStore Novo() => tipo == "sqlite" ? new TemplateStoreSqlite(Path.Combine(dir, "kv.db")) : new TemplateStoreMemoria();
            var s = Novo();
            Assert.Null(s.Obter("capital {pais}"));
            s.Salvar(new TemplateResposta { Chave = "capital {pais}", Template = "A capital {de_entidade} é {capital}." });
            s.Salvar(new TemplateResposta { Chave = "moeda {pais}", Template = "A moeda {de_entidade} é o {moeda}.", Peso = 0.7 });
            var t = s.Obter("capital {pais}")!;
            Assert.Equal((0.5, 0), (t.Peso, t.Usos));
            Assert.NotEmpty(t.CriadoEm);
            s.Salvar(t with { Peso = 0.65, Usos = 3, FeedbacksBons = 1 });
            Assert.Equal((0.65, 3, 1), (s.Obter("capital {pais}")!.Peso, s.Obter("capital {pais}")!.Usos, s.Obter("capital {pais}")!.FeedbacksBons));
            Assert.Equal(["capital {pais}", "moeda {pais}"], s.Listar().Select(x => x.Chave));
            s.Remover("moeda {pais}");
            Assert.Single(s.Listar());
            if (tipo == "sqlite") Assert.Equal(0.65, Novo().Obter("capital {pais}")!.Peso); // persiste no arquivo
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Sqlite_GuardaCadaPerguntaEFeedback_EAsEstatisticasSobrevivemAoReinicio()
    {
        var dir = Directory.CreateTempSubdirectory("hist-").FullName;
        try
        {
            var banco = Path.Combine(dir, "orq.db");
            var orq = Orquestrador(new TemplateStoreSqlite(banco));
            await orq.Responder("Qual é a capital da França?", Llm());
            await orq.Responder("Qual é a capital do Japão?", Llm());
            await orq.Responder("Qual o símbolo químico do ferro?", Llm());
            orq.Feedback("capital {pais}", bom: true);
            orq.Feedback("capital {pais}", bom: false);

            // "Reinício": outro orquestrador sobre o mesmo arquivo.
            var s = Orquestrador(new TemplateStoreSqlite(banco)).Estatisticas;
            Assert.Equal((3, 1, 2, 2), (s.Perguntas, s.AcertosTemplate, s.ChamadasGeracao, s.TemplatesSalvos));
            Assert.Equal((1, 1), (s.FeedbacksBons, s.FeedbacksRuins));
            Assert.Equal(orq.Estatisticas.TokensEntrada, s.TokensEntrada);
            Assert.Equal(Math.Round(orq.Estatisticas.Custo, 9), Math.Round(s.Custo, 9));

            // O histórico fica legível no próprio SQLite.
            using var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={banco};Pooling=False");
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT pergunta, caminho, resposta FROM perguntas ORDER BY id";
            using var r = cmd.ExecuteReader();
            var linhas = new List<(string, string, string)>();
            while (r.Read()) linhas.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
            Assert.Equal(("Qual é a capital do Japão?", "template", "A capital do Japão é Tóquio."), linhas[1]);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private sealed class Fallback(string nome) : IMotor
    {
        public ConcurrentQueue<Pedido> Pedidos { get; } = new();
        public string Nome => nome;

        public Task<Disponibilidade> Disponivel(CancellationToken ct = default) => Task.FromResult(new Disponibilidade(true, ""));

        public Task<Resposta> Atender(Pedido p, Action<string> delta, CancellationToken ct)
        {
            Pedidos.Enqueue(p);
            var texto = p.Tipo == TipoPedido.Fala ? ResponderComoLlm(p.Contexto) : $"síntese de {p.Persona.Nome}";
            return Task.FromResult(new Resposta { Texto = texto, Uso = new Uso { Entrada = 60, Saida = 15, Custo = 0.002 } });
        }
    }

    private static Pedido Pedido(string assunto, string conversa = "c1", string tipo = TipoPedido.Fala, List<string>? respondendoA = null, string contexto = "") => new()
    {
        Id = $"t-{Guid.NewGuid():N}"[..10], Conversa = conversa, Diagrama = "d", Tipo = tipo, System = "", Contexto = contexto, Assunto = assunto,
        RespondendoA = respondendoA ?? [],
        Persona = Personas.Ler("n1", "# Respondedor\n\nResponda.\n\n```yaml\nmotor: template\n```", "Respondedor"),
    };

    [Fact]
    public async Task MotorTemplate_UsaOFallbackSoNaChaveNova_EContaNoDetalhe()
    {
        var api = new Fallback("api");
        var m = new MotorTemplate(Orquestrador(), _ => api);
        var r1 = await m.Atender(Pedido("Qual é a capital da França?"), _ => { }, CancellationToken.None);
        Assert.Equal(("A capital da França é Paris.", "llm"), (r1.Texto, (string?)r1.Detalhe!["caminho"]));
        Assert.Equal("claude-sonnet-5-5", api.Pedidos.Single().Persona.Modelo);
        Assert.Contains("uma única frase", api.Pedidos.Single().System);

        var r2 = await m.Atender(Pedido("Qual é a capital do Japão?", "c2"), _ => { }, CancellationToken.None);
        Assert.Equal(("A capital do Japão é Tóquio.", "template", 0.5), (r2.Texto, (string?)r2.Detalhe!["caminho"], (double?)r2.Detalhe["peso"]));
        Assert.Single(api.Pedidos);
        Assert.Equal(0, r2.Uso!.Custo);
        Assert.True((await m.Disponivel()).Ok);
    }

    [Fact]
    public async Task MotorTemplate_FeedbackRuimReenviaAoLlm_AteOLimite()
    {
        var api = new Fallback("api");
        var m = new MotorTemplate(Orquestrador(), _ => api);
        await m.Atender(Pedido("Qual é a capital da França?"), _ => { }, CancellationToken.None);
        await m.Atender(Pedido("Qual é a capital da França?"), _ => { }, CancellationToken.None); // agora pelo template
        Assert.Single(api.Pedidos);

        for (var tentativa = 1; tentativa <= 2; tentativa++)
        {
            m.RegistrarFeedback("c1", "n1", bom: false);
            var r = await m.Atender(Pedido("Qual é a capital da França?"), _ => { }, CancellationToken.None);
            Assert.Equal(("llm", tentativa), ((string?)r.Detalhe!["caminho"], (int?)r.Detalhe["tentativas"]));
        }
        m.RegistrarFeedback("c1", "n1", bom: false);
        var limite = await m.Atender(Pedido("Qual é a capital da França?"), _ => { }, CancellationToken.None);
        Assert.StartsWith("Não consegui uma resposta melhor depois de 2 tentativas", limite.Texto);
        Assert.Equal(3, api.Pedidos.Count);
    }

    [Fact]
    public async Task MotorTemplate_SinteseVaiDiretoAoFallback_EAPerguntaDoChatValeQuandoChamadaComArroba()
    {
        var ollama = new Fallback("ollama");
        var m = new MotorTemplate(Orquestrador(), nome => nome == "ollama" ? ollama : throw new InvalidOperationException(nome));
        var p = Pedido("x", tipo: TipoPedido.Sintese) with
        {
            Persona = Personas.Ler("n1", "# R\n\nx\n\n```yaml\nmotor: template\nfallback: ollama\n```", "R"),
        };
        Assert.Equal("síntese de R", (await m.Atender(p, _ => { }, CancellationToken.None)).Texto);
        Assert.Equal(("ollama", "qwen2.5:3b"), (ollama.Pedidos.Single().Persona.Motor, ollama.Pedidos.Single().Persona.Modelo));

        var chat = Pedido("Assunto antigo", respondendoA: ["usuario"], contexto: "Assunto: x\n\nConversa até agora:\nUsuário: Assunto antigo\n\nUsuário: Qual a moeda do Japão?\n\nSua vez");
        Assert.Equal("Qual a moeda do Japão?", MotorTemplate.PerguntaDe(chat));
    }

    [Fact]
    public async Task NaConversa_PedirMudancaNaVezHumana_EFeedbackRuim_EARespostaVoltaAoLlm()
    {
        var api = new Fallback("api");
        var orq = Orquestrador();
        var template = new MotorTemplate(orq, _ => api);
        var d = new Diagram();
        Ops.AddNode(d, new NewNode { Id = "r", Text = "Respondedor", X = 0, Y = 0 });
        Ops.AddNode(d, new NewNode { Id = "voce", Text = "Você", X = 200, Y = 0 });
        Ops.AddEdge(d, new NewEdge { From = "r", To = "voce" });
        var dir = Directory.CreateTempSubdirectory("conv-").FullName;
        var conversas = new Core.Conversas.Conversas(new OpcoesConversas
        {
            Dir = dir, Diagrama = "d", Ler = () => d.Clone(), Motores = new RegistroDeMotores([template, api]), Emitir = _ => { },
            Persona = (id, nome) => Personas.Ler(id, id == "voce" ? "# Você\n\n```yaml\nhumana: true\n```" : "# R\n\nx\n\n```yaml\nmotor: template\n```", nome),
            AoFeedbackHumano = template.RegistrarFeedback,
        });
        // Já existe o template da família: a primeira resposta sai dele.
        await orq.Responder("Qual a capital do Japão?", Llm());

        var id = conversas.Iniciar("Qual é a capital da França?", "debate", 1).Id;
        for (var i = 0; i < 200 && conversas.Obter(id).Status != "aguardando"; i++) await Task.Delay(20);
        Assert.Equal("template", (string?)conversas.Obter(id).Mensagens.Last()!.Detalhe!["caminho"]);
        conversas.ResponderHumano(id, new RespostaHumana("mudar", "confira"));
        // Pedir mudança: o Respondedor refaz (agora pelo LLM) e, com uma rodada só, a conversa termina.
        var fim = await conversas.Esperar(id);
        var refeita = fim.Mensagens.Last(m => m.Persona == "r");
        Assert.Equal(2, fim.Mensagens.Count(m => m.Persona == "r"));
        Assert.Equal(("llm", "feedback ruim: a resposta volta ao LLM"), ((string?)refeita.Detalhe!["caminho"], (string?)refeita.Detalhe["motivo"]));
        Assert.Equal(0.35, orq.Store.Obter("capital {pais}")!.Peso, 6);
        Assert.Single(api.Pedidos); // só a resposta refeita foi ao LLM

        // Aprovar depois (o mesmo gancho que a vez humana usa): o peso sobe pela média móvel.
        template.RegistrarFeedback(id, "r", bom: true);
        Assert.Equal(0.545, orq.Store.Obter("capital {pais}")!.Peso, 6); // 0,3·1 + 0,7·0,35
        Directory.Delete(dir, true);
    }
}
