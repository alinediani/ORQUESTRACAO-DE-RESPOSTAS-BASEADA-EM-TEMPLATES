using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Orquestracao.Core.Conversas;
using Orquestracao.Core.Diagramas;
using static Orquestracao.Core.Tests.Conversas.TurnosTests;

namespace Orquestracao.Core.Tests.Conversas;

/// <summary>Um motor de mentira: registra cada pedido e responde pelo que o teste mandar.</summary>
public sealed class MotorFalso(string nome, Func<Pedido, Task<Resposta>>? responder = null) : IMotor
{
    public ConcurrentQueue<Pedido> Pedidos { get; } = new();
    public string Nome => nome;

    public Task<Disponibilidade> Disponivel(CancellationToken ct = default) => Task.FromResult(new Disponibilidade(true, "falso"));

    public Task<Resposta> Atender(Pedido p, Action<string> delta, CancellationToken ct)
    {
        Pedidos.Enqueue(p);
        delta("…");
        return responder?.Invoke(p) ?? Task.FromResult(new Resposta { Texto = $"{p.Persona.Nome} ({p.Tipo})" });
    }
}

/// <summary>Portado de legado/server/conversa.test.ts: debate, resposta, tarefa, persona humana, saída e o ciclo de vida.</summary>
public sealed class ConversasTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("conversas-").FullName;
    private readonly Dictionary<string, string> _yaml = [];
    private readonly ConcurrentQueue<JsonObject> _eventos = new();
    private readonly Dictionary<string, string> _saidas = [];
    private Diagram _d = new();

    public void Dispose() => Directory.Delete(_dir, true);

    private Core.Conversas.Conversas Criar(MotorFalso motor, int limite = 40) => new(new OpcoesConversas
    {
        Dir = _dir,
        Diagrama = "teste",
        Ler = () => _d.Clone(),
        Persona = (id, nome) => Personas.Ler(id, $"# {nome}\n\nVocê é {nome}.\n\n```yaml\n{Yaml(id)}\n```", nome),
        Motores = new RegistroDeMotores([motor]),
        Emitir = _eventos.Enqueue,
        LimiteTurnos = limite,
        Template = _ => "# {{título}}",
        GravarSaida = (c, arquivo, texto) => _saidas[arquivo] = texto,
    });

    /// <summary>O yaml da persona no teste: motor api, a menos que o teste diga outro.</summary>
    private string Yaml(string id)
    {
        var y = _yaml.GetValueOrDefault(id, "");
        return y.Contains("motor:") ? y : "motor: api\n" + y;
    }

    private static List<string> Falas(Conversa c, string tipo = "fala") => c.Mensagens.Where(m => m.Tipo == tipo).Select(m => m.Nome).ToList();

    private static int Status(Action a)
    {
        try
        {
            a();
            return 0;
        }
        catch (OpError e)
        {
            return e.Status;
        }
    }

    [Fact]
    public async Task Debate_RodadasPelasSetas_CadaUmRespondeAQuemApontaParaEle()
    {
        _d = D("a,b,c", ("a", "b"), ("b", "c"), ("c", "a"));
        var motor = new MotorFalso("api");
        var conversas = Criar(motor);
        var c = await conversas.Esperar(conversas.Iniciar("Abrir uma cafeteria?", "debate", 2).Id);

        Assert.Equal("terminada", c.Status);
        Assert.Equal(["A", "B", "C", "A", "B", "C"], Falas(c));
        var pedidos = motor.Pedidos.ToList();
        Assert.Contains("abra a conversa", pedidos[0].Contexto);
        Assert.Contains("responda a A", pedidos[1].Contexto);
        Assert.Equal(["c"], pedidos[3].RespondendoA); // na 2ª rodada, A responde a C
        Assert.Contains("Conversa até agora:\nUsuário: Abrir uma cafeteria?", pedidos[1].Contexto);
        Assert.Contains("as outras: B, C", pedidos[0].System);
        Assert.Null(c.Resposta);
        // O chat recebe o início, os turnos, o texto aos poucos e o fim.
        var tipos = _eventos.Select(e => (string?)e["type"]).ToHashSet();
        Assert.Superset(new HashSet<string?> { "chat.inicio", "chat.turno", "chat.delta", "chat.mensagem", "chat.fim" }, tipos);
        Assert.All(_eventos, e => Assert.Equal(c.Id, (string?)e["conversa"]));
    }

    [Fact]
    public async Task Resposta_ADebateTerminaComASintesePelaModeradora()
    {
        _d = D("a,b,m", ("a", "b"), ("b", "m"));
        _yaml["m"] = "papel: moderadora";
        var conversas = Criar(new MotorFalso("api"));
        var c = await conversas.Esperar(conversas.Iniciar("Pergunta", "resposta", 1).Id);
        Assert.Equal(["M"], Falas(c, "sintese"));
        Assert.Equal("M (sintese)", c.Resposta);
    }

    [Fact]
    public async Task Tarefa_AOrquestradoraDelegaEmParalelo_EConsolida()
    {
        _d = D("o,p,r", ("o", "p"), ("o", "r"));
        _yaml["o"] = "papel: orquestradora";
        var portao = new TaskCompletionSource();
        var executando = 0;
        var maximo = 0;
        var motor = new MotorFalso("api", async p =>
        {
            if (p.Tipo == TipoPedido.Tarefa) return new Resposta { Texto = "Plano.\nDELEGAR P: pesquisar\nDELEGAR R: redigir\nDELEGAR X: nada" };
            if (p.Tipo == TipoPedido.Execucao)
            {
                var agora = Interlocked.Increment(ref executando);
                maximo = Math.Max(maximo, agora);
                if (agora == 2) portao.TrySetResult();
                await portao.Task.WaitAsync(TimeSpan.FromSeconds(5)); // só passa se as duas estiverem rodando juntas
                return new Resposta { Texto = $"feito por {p.Persona.Nome}" };
            }
            return new Resposta { Texto = "Entrega final" };
        });
        var conversas = Criar(motor);
        var c = await conversas.Esperar(conversas.Iniciar("Lançar o app", "tarefa").Id);

        Assert.Equal(2, maximo);
        Assert.Equal("Entrega final", c.Resposta);
        Assert.Equal(["P", "R"], Falas(c, "resultado").Order());
        var plano = c.Mensagens.Single(m => m.Tipo == "tarefa");
        Assert.Equal("Plano.", plano.Texto);
        Assert.All(c.Mensagens.Where(m => m.Tipo == "resultado"), m => Assert.Equal(plano.Id, m.Pai));
        Assert.Contains(c.Mensagens, m => m.Tipo == "sistema" && m.Texto.Contains("tentou delegar para X"));
        Assert.Contains("feito por P", motor.Pedidos.Single(p => p.Tipo == TipoPedido.Consolidar).Contexto);
        Assert.Equal(2, _eventos.Count(e => (string?)e["type"] == "chat.delegacao"));
    }

    [Fact]
    public async Task Tarefa_SemDelegar_AEntregaEDireta()
    {
        _d = D("o,p", ("o", "p"));
        var conversas = Criar(new MotorFalso("api", _ => Task.FromResult(new Resposta { Texto = "Fiz sozinha." })));
        var c = await conversas.Esperar(conversas.Iniciar("Algo simples", "tarefa").Id);
        Assert.Equal("Fiz sozinha.", c.Resposta);
        Assert.Equal(["O"], Falas(c, "sintese"));
    }

    private async Task<Conversa> AteEsperar(Core.Conversas.Conversas conversas, string id)
    {
        for (var i = 0; i < 200; i++)
        {
            var c = conversas.Obter(id);
            if (c.Status == "aguardando") return c;
            await Task.Delay(20);
        }
        throw new TimeoutException("a conversa não chegou à vez humana");
    }

    [Fact]
    public async Task PersonaHumana_AConversaEspera_EAprovarSegue()
    {
        _d = D("a,voce,b", ("a", "voce"), ("voce", "b"));
        _yaml["voce"] = "humana: true";
        var motor = new MotorFalso("api");
        var conversas = Criar(motor);
        var id = conversas.Iniciar("Assunto", "debate", 1).Id;
        var esperando = await AteEsperar(conversas, id);
        Assert.Equal(("voce", "VOCE"), (esperando.Espera!.Persona, esperando.Espera.Nome));
        Assert.Equal(400, Status(() => conversas.ResponderHumano(id, new RespostaHumana("talvez"))));

        conversas.ResponderHumano(id, new RespostaHumana("aprovar", "pode seguir"));
        var c = await conversas.Esperar(id);
        Assert.Equal(["A", "VOCE", "B"], Falas(c));
        Assert.Equal("Aprovado. pode seguir", c.Mensagens.Single(m => m.Nome == "VOCE").Texto);
        Assert.DoesNotContain(motor.Pedidos, p => p.Persona.Id == "voce"); // a humana não usa motor
        Assert.Equal(409, Status(() => conversas.ResponderHumano(id, new RespostaHumana("aprovar"))));
    }

    [Fact]
    public async Task PersonaHumana_PedirMudanca_QuemFalouAntesRefaz()
    {
        _d = D("a,voce", ("a", "voce"));
        _yaml["voce"] = "humana: true";
        var motor = new MotorFalso("api");
        var conversas = Criar(motor);
        var id = conversas.Iniciar("Assunto", "debate", 1).Id;
        await AteEsperar(conversas, id);
        conversas.ResponderHumano(id, new RespostaHumana("mudar", "mais curto"));
        var c = await conversas.Esperar(id);
        Assert.Equal(["A", "VOCE", "A"], Falas(c));
        Assert.Contains("Pedido de mudança: mais curto", motor.Pedidos.Last().Contexto);
        Assert.Equal(["voce"], motor.Pedidos.Last().RespondendoA);
    }

    [Fact]
    public async Task PersonaHumana_Encerrar_TerminaAConversa()
    {
        _d = D("a,voce,b", ("a", "voce"), ("voce", "b"));
        _yaml["voce"] = "humana: true";
        var conversas = Criar(new MotorFalso("api"));
        var id = conversas.Iniciar("Assunto", "debate", 2).Id;
        await AteEsperar(conversas, id);
        conversas.ResponderHumano(id, new RespostaHumana("encerrar"));
        var c = await conversas.Esperar(id);
        Assert.Equal("terminada", c.Status);
        Assert.Equal(["A", "VOCE"], Falas(c));
        Assert.Contains(c.Mensagens, m => m.Texto == "Conversa encerrada por VOCE.");
    }

    [Fact]
    public async Task Parar_CancelaOTurnoEmAndamento()
    {
        _d = D("a,b", ("a", "b"));
        var conversas = Criar(new MotorFalso("api", async _ =>
        {
            await Task.Delay(Timeout.Infinite);
            return new Resposta { Texto = "nunca" };
        }));
        var id = conversas.Iniciar("Assunto").Id;
        for (var i = 0; i < 100 && conversas.Obter(id).Pendentes!.Count == 0; i++) await Task.Delay(20);
        Assert.Single(conversas.Obter(id).Pendentes!);
        conversas.Parar(id);
        var c = await conversas.Esperar(id);
        Assert.Equal("parada", c.Status);
        Assert.Empty(Falas(c));
        Assert.Contains(c.Mensagens, m => m.Texto == "Conversa parada.");
    }

    [Fact]
    public async Task ErroDoMotor_ViraMensagemDeErro_EAConversaContinua()
    {
        _d = D("a,b", ("a", "b"));
        var conversas = Criar(new MotorFalso("api", p => p.Persona.Id == "a"
            ? Task.FromException<Resposta>(new InvalidOperationException("sem credencial"))
            : Task.FromResult(new Resposta { Texto = "ok" })));
        var c = await conversas.Esperar(conversas.Iniciar("Assunto", "debate", 1).Id);
        var erro = c.Mensagens.Single(m => m.Tipo == "erro");
        Assert.Contains("Não consegui a vez de A (motor api): sem credencial", erro.Texto);
        Assert.Equal(["B"], Falas(c));
    }

    [Fact]
    public async Task MotorNaoConfigurado_ViraErro_SemDerrubarAConversa()
    {
        _d = D("a");
        _yaml["a"] = "motor: ollama";
        var conversas = Criar(new MotorFalso("api"));
        var c = await conversas.Esperar(conversas.Iniciar("Assunto", "debate", 1).Id);
        Assert.Contains("motor \"ollama\" não está configurado", c.Mensagens.Single(m => m.Tipo == "erro").Texto);
        Assert.Equal("terminada", c.Status);
    }

    [Fact]
    public async Task LimiteDeTurnos_EncerraUmCicloSemFim()
    {
        _d = D("a,b", ("a", "b"), ("b", "a"));
        var conversas = Criar(new MotorFalso("api"), limite: 3);
        var c = await conversas.Esperar(conversas.Iniciar("Assunto", "debate", 10).Id);
        Assert.Equal(3, Falas(c).Count);
        Assert.Contains(c.Mensagens, m => m.Texto == "A conversa atingiu o limite de 3 turnos e foi encerrada.");
    }

    [Fact]
    public async Task Intervir_DepoisDoFim_AMencionadaResponde()
    {
        _d = D("a,b", ("a", "b"));
        var motor = new MotorFalso("api");
        var conversas = Criar(motor);
        var id = conversas.Iniciar("Assunto", "debate", 1).Id;
        await conversas.Esperar(id);
        Assert.Equal(400, Status(() => conversas.Intervir(id, "  ")));
        conversas.Intervir(id, "E aí, @b, e o aluguel?");
        var c = await conversas.Esperar(id);
        Assert.Equal(["A", "B", "B"], Falas(c));
        Assert.Equal(["usuario"], motor.Pedidos.Last().RespondendoA);
        Assert.Contains("falou diretamente com você", motor.Pedidos.Last().Contexto);
    }

    [Fact]
    public async Task Sintetizar_DepoisDoFim_GeraARespostaPolida()
    {
        _d = D("a,b", ("a", "b"));
        var conversas = Criar(new MotorFalso("api"));
        var id = conversas.Iniciar("Assunto", "debate", 1).Id;
        await conversas.Esperar(id);
        conversas.Sintetizar(id);
        var c = await conversas.Esperar(id);
        Assert.Equal("B (sintese)", c.Resposta); // sem moderadora: a última que falou
    }

    [Fact]
    public async Task PersonaDeSaida_NaoFalaNasRodadas_EPreencheOTemplateNoFim()
    {
        _d = D("a,b,s", ("a", "b"), ("b", "s"));
        _yaml["s"] = "saida: true\narquivo: README.md";
        var motor = new MotorFalso("api", p => Task.FromResult(new Resposta { Texto = p.Tipo == TipoPedido.Saida ? "```markdown\n# Pronto\n```" : "fala" }));
        var conversas = Criar(motor);
        var c = await conversas.Esperar(conversas.Iniciar("Assunto", "debate", 1).Id);
        Assert.Equal(["A", "B"], Falas(c));
        var saida = c.Mensagens.Single(m => m.Tipo == "saida");
        Assert.Equal(("README.md", "# Pronto"), (saida.Arquivo, saida.Texto)); // sem a cerca de código
        Assert.Equal("# Pronto", _saidas["README.md"]);
        Assert.Contains("Template de saída:\n\n# {{título}}", motor.Pedidos.Last().Contexto);
        Assert.Contains("persona de saída", motor.Pedidos.Last().System);
    }

    [Fact]
    public async Task Persiste_EmJson_ELista()
    {
        _d = D("a");
        var conversas = Criar(new MotorFalso("api"));
        var id = conversas.Iniciar("Primeira", "debate", 1).Id;
        await conversas.Esperar(id);
        Assert.True(File.Exists(Path.Combine(_dir, $"{id}.json")));
        // Outra instância (o servidor reiniciou) lê do disco.
        var outra = Criar(new MotorFalso("api"));
        Assert.Equal(("Primeira", "terminada", 2), (outra.Obter(id).Assunto, outra.Obter(id).Status, outra.Obter(id).Mensagens.Count));
        Assert.Equal([id], outra.Listar().Select(c => c.Id));
        Assert.Equal(404, Status(() => outra.Obter("c-nao-existe")));
        Assert.Equal(404, Status(() => outra.Obter("../fora")));
    }

    [Fact]
    public void Iniciar_ValidaAEntrada()
    {
        var conversas = Criar(new MotorFalso("api"));
        Assert.Equal(400, Status(() => conversas.Iniciar("assunto"))); // sem personas
        _d = D("a");
        Assert.Equal(400, Status(() => conversas.Iniciar("  ")));
        Assert.Equal(400, Status(() => conversas.Iniciar("x", "conversa")));
        Assert.Equal(400, Status(() => conversas.Iniciar("x", "debate", 11)));
    }
}
