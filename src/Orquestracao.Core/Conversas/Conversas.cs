using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Orquestracao.Core.Diagramas;

namespace Orquestracao.Core.Conversas;

public sealed class OpcoesConversas
{
    /// <summary>Pasta onde cada conversa é salva (&lt;id&gt;.json).</summary>
    public required string Dir { get; init; }
    public required string Diagrama { get; init; }
    public required Func<Diagram> Ler { get; init; }
    public required Func<string, string, Persona> Persona { get; init; }
    public required RegistroDeMotores Motores { get; init; }
    /// <summary>Manda um evento do chat aos navegadores (pelo SSE do diagrama).</summary>
    public required Action<JsonObject> Emitir { get; init; }
    /// <summary>Turnos no máximo por conversa: protege de um ciclo que não acabaria.</summary>
    public int LimiteTurnos { get; init; } = 40;
    /// <summary>O template de saída da persona (personas/&lt;id&gt;.saida.md).</summary>
    public Func<string, string>? Template { get; init; }
    /// <summary>Grava o .md que a persona de saída gerou.</summary>
    public Action<string, string, string>? GravarSaida { get; init; }
    /// <summary>
    /// O feedback da persona humana sobre quem ela respondeu (conversa, persona, bom): Aprovar é bom, Pedir
    /// mudança é ruim. O orquestrador por template ajusta o peso do template com ele (R05).
    /// </summary>
    public Action<string, string, bool>? AoFeedbackHumano { get; init; }
}

/// <summary>
/// As conversas entre as personas de um diagrama (equivale ao createConversas de legado/server/conversa.ts).
/// A conversa não sabe falar: a cada vez, monta um pedido (quem fala, o prompt, o contexto) e o entrega ao
/// motor daquela persona, que devolve o texto. Por isso motores diferentes se misturam na mesma conversa.
/// <list type="bullet">
/// <item>debate: N rodadas pelas setas; cada persona responde a quem aponta para ela</item>
/// <item>resposta: o debate e, no fim, a síntese (pela moderadora, ou pela última a falar)</item>
/// <item>tarefa: a orquestradora delega pelas setas (DELEGAR &lt;persona&gt;: &lt;tarefa&gt;), as delegadas
/// trabalham em paralelo (e podem delegar adiante) e ela consolida a entrega</item>
/// </list>
/// Nesta versão ficam de fora a persona de memória, a curadoria e o pós-fluxo (os campos são lidos, mas não agem).
/// </summary>
public sealed partial class Conversas(OpcoesConversas opts)
{
    public static readonly string[] Modos = ["debate", "resposta", "tarefa"];
    private static readonly string[] AcoesHumanas = ["aprovar", "mudar", "encerrar"];
    private static readonly JsonSerializerOptions Indentado = new(Conversa.JsonOpcoes) { WriteIndented = true };

    private sealed class Viva(Conversa dados)
    {
        public Conversa Dados { get; } = dados;
        public CancellationTokenSource Parar { get; set; } = new();
        /// <summary>Quem fala antes da próxima vez: chamadas com @ no chat ou de volta por um pedido de mudança.</summary>
        public readonly ConcurrentQueue<(string Id, List<string> A)> Chamadas = new();
        public readonly ConcurrentDictionary<string, TaskCompletionSource<RespostaHumana>> Esperas = new();
        public Posicao? Posicao { get; set; }
        public volatile bool SintetizarJa;
        public int Turnos;
        public Task Execucao { get; set; } = Task.CompletedTask;
        public readonly ConcurrentDictionary<string, Pendente> EmAndamento = new();
    }

    private sealed class Encerrada(string msg) : Exception(msg);

    private sealed class LimiteAtingido : Exception;

    private readonly ConcurrentDictionary<string, Viva> _vivas = new();
    private int Limite => opts.LimiteTurnos;

    private static string Agora() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static string NovoId(string prefixo) => prefixo + Guid.NewGuid().ToString("N")[..8];

    private string Arquivo(string id) => Path.Combine(opts.Dir, $"{id}.json");

    private void Salvar(Viva v)
    {
        lock (v.Dados.Mensagens)
        {
            v.Dados.AtualizadaEm = Agora();
            Directory.CreateDirectory(opts.Dir);
            File.WriteAllText(Arquivo(v.Dados.Id), JsonSerializer.Serialize(v.Dados, Indentado) + "\n");
        }
    }

    [GeneratedRegex(@"^[\w-]+$")]
    private static partial Regex IdConversaRe();

    private Conversa Carregar(string id)
    {
        if (_vivas.TryGetValue(id, out var v)) return v.Dados;
        if (!IdConversaRe().IsMatch(id) || !File.Exists(Arquivo(id))) throw new OpError(404, $"Conversa \"{id}\" não encontrada.");
        try
        {
            return JsonSerializer.Deserialize<Conversa>(File.ReadAllText(Arquivo(id)), Conversa.JsonOpcoes)!;
        }
        catch (JsonException)
        {
            throw new OpError(404, $"Conversa \"{id}\" não encontrada.");
        }
    }

    /// <summary>A conversa em memória (carregando do disco uma que já terminou).</summary>
    private Viva VivaDe(string id) => _vivas.GetOrAdd(id, _ => new Viva(Carregar(id)));

    private void Emitir(Conversa c, object ev)
    {
        var obj = JsonSerializer.SerializeToNode(ev, Conversa.JsonOpcoes)!.AsObject();
        obj["conversa"] = c.Id;
        opts.Emitir(obj);
    }

    // ---- quem participa ------------------------------------------------------

    private Persona PersonaDe(Diagram d, string id) => opts.Persona(id, NomeDe(d, id));

    /// <summary>As personas de saída e de memória não falam nas rodadas.</summary>
    private List<string> Saidas(Diagram d) => d.Nodes.Where(n => opts.Persona(n.Id, n.Text).Saida).Select(n => n.Id).ToList();

    private List<string> Memorias(Diagram d) => d.Nodes.Where(n => opts.Persona(n.Id, n.Text).Memoria).Select(n => n.Id).ToList();

    /// <summary>O desenho das rodadas e das delegações: sem as personas de saída e de memória.</summary>
    private Diagram Rodada(Diagram d) => Turnos.SemNos(d, [.. Saidas(d), .. Memorias(d)]);

    /// <summary>A ordem da rodada, começando pelos blocos de início marcados nas personas.</summary>
    private List<string> Ordem(Diagram d)
    {
        var r = Rodada(d);
        return Turnos.OrdemDaRodada(r, r.Nodes.Where(n => opts.Persona(n.Id, n.Text).Inicio).Select(n => n.Id));
    }

    private static string NomeDe(Diagram d, string id) => id == "usuario" ? "o usuário" : d.Nodes.Find(n => n.Id == id)?.Text ?? id;

    private Mensagem Registrar(Viva v, Mensagem m)
    {
        lock (v.Dados.Mensagens) v.Dados.Mensagens.Add(m);
        Salvar(v);
        Emitir(v.Dados, new { type = "chat.mensagem", mensagem = m });
        return m;
    }

    private static Mensagem Nova(string tipo, string nome, string texto) => new() { Id = NovoId("m-"), Em = Agora(), Tipo = tipo, Nome = nome, Texto = texto };

    private Mensagem Sistema(Viva v, string texto) => Registrar(v, Nova("sistema", "Conversa", texto));

    // ---- prompts ---------------------------------------------------------------

    private static string Transcricao(Conversa c)
    {
        List<Mensagem> ms;
        lock (c.Mensagens) ms = [.. c.Mensagens];
        return string.Join("\n\n", ms.Where(m => m.Tipo is not ("sistema" or "erro")).Select(m => $"{(m.Tipo == "usuario" ? "Usuário" : m.Nome)}: {m.Texto}"));
    }

    private static string ConversaAteAgora(Conversa c)
    {
        var t = Transcricao(c);
        return t.Length > 0 ? $"\n\nConversa até agora:\n{t}" : "";
    }

    public static string MontarSystem(Persona p, Diagram d)
    {
        var outras = d.Nodes.Where(n => n.Id != p.Id).Select(n => n.Text).ToList();
        if (p.Saida)
        {
            return string.Join('\n',
                p.Prompt.Trim(), "---",
                $"Você é {p.Nome}, a persona de saída de uma conversa em grupo{(outras.Count > 0 ? $" (entre: {string.Join(", ", outras)})" : "")}.",
                $"Sua tarefa é preencher o template de saída (o arquivo {p.Arquivo}) com o resultado da conversa.",
                "Regras: responda em português do Brasil; mantenha os títulos e a ordem do template; troque cada",
                "{{marcação}} pelo conteúdo; não invente o que não está na conversa (escreva \"a definir\");",
                "devolva só o markdown preenchido, sem comentários antes ou depois.");
        }
        return string.Join('\n',
            p.Prompt.Trim(), "---",
            $"Você é {p.Nome}, uma das personas de uma conversa em grupo{(outras.Count > 0 ? $" (as outras: {string.Join(", ", outras)})" : "")}.",
            "Regras: responda em português do Brasil; fale só por você (nunca escreva as falas das outras personas);",
            "não comece com o seu nome; não repita o que já foi dito; seja direta, com 1 a 3 parágrafos curtos,",
            "a menos que peçam uma entrega maior (síntese, tarefa ou resultado).");
    }

    // ---- um turno --------------------------------------------------------------

    private sealed record Turno(string Tipo, string Contexto, List<string>? RespondendoA = null, List<string>? Delegaveis = null);

    private sealed record Feito(Resposta Resposta, Persona Persona, string TurnoId);

    /// <summary>Um turno: monta o pedido, chama o motor da persona e devolve a resposta (ou null, se falhou).</summary>
    private async Task<Feito?> FazerTurno(Viva v, string personaId, Turno t)
    {
        var ct = v.Parar.Token;
        ct.ThrowIfCancellationRequested();
        var d = opts.Ler();
        var no = d.Nodes.Find(n => n.Id == personaId);
        if (no is null) return null;
        var persona = opts.Persona(personaId, no.Text);
        var turnoId = NovoId("t-");
        var respondendoA = t.RespondendoA ?? [];
        var pedido = new Pedido
        {
            Id = turnoId, Conversa = v.Dados.Id, Diagrama = opts.Diagrama, Tipo = t.Tipo, Persona = persona,
            System = MontarSystem(persona, d), Contexto = t.Contexto, Assunto = v.Dados.Assunto, RespondendoA = respondendoA,
            Delegaveis = (t.Delegaveis ?? []).Select(id => new Pessoa(id, NomeDe(d, id))).ToList(),
        };
        if (persona.Humana) return await VezHumana(v, persona, turnoId, t, respondendoA, d);

        object Base(string status) => new
        {
            type = "chat.turno", turno = turnoId, persona = personaId, nome = persona.Nome, tipo = t.Tipo, motor = persona.Motor, status,
            respondendoA, arestas = Turnos.Arestas(d, respondendoA, personaId),
        };
        Emitir(v.Dados, Base("pensando"));
        Interlocked.Increment(ref v.Turnos);
        v.EmAndamento[turnoId] = new Pendente(turnoId, personaId, persona.Nome, t.Tipo, persona.Motor);
        try
        {
            var motor = opts.Motores[persona.Motor];
            // Parar não espera o motor: um motor que ignore o cancelamento não prende a conversa.
            var resposta = await motor.Atender(pedido, texto => Emitir(v.Dados, new { type = "chat.delta", turno = turnoId, persona = personaId, texto }), ct).WaitAsync(ct);
            Emitir(v.Dados, Base("fim"));
            return new Feito(resposta, persona, turnoId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception err)
        {
            Emitir(v.Dados, Base("erro"));
            Registrar(v, Nova("erro", persona.Nome, $"Não consegui a vez de {persona.Nome} (motor {persona.Motor}): {err.Message}") with
            {
                Persona = personaId, Turno = turnoId, Motor = persona.Motor,
            });
            return null;
        }
        finally
        {
            v.EmAndamento.TryRemove(turnoId, out _);
        }
    }

    /// <summary>O texto que entra na conversa pela resposta do usuário na vez humana.</summary>
    private static string TextoHumano(RespostaHumana r)
    {
        var t = r.Texto?.Trim() ?? "";
        if (r.Venceu) return "(sem resposta no prazo: a conversa seguiu)";
        return r.Acao switch
        {
            "mudar" => $"Pedido de mudança: {(t.Length > 0 ? t : "refaça")}",
            "encerrar" => t.Length > 0 ? t : "Encerrar a conversa.",
            _ => t.Length > 0 ? $"Aprovado. {t}" : "Aprovado.",
        };
    }

    /// <summary>A vez de uma persona humana: a conversa para e espera a resposta (ou o prazo).</summary>
    private async Task<Feito> VezHumana(Viva v, Persona persona, string turnoId, Turno t, List<string> respondendoA, Diagram d)
    {
        var espera = new Espera { Turno = turnoId, Persona = persona.Id, Nome = persona.Nome, Tipo = t.Tipo, Contexto = t.Contexto, Posicao = v.Posicao, Em = Agora() };
        v.Dados.Status = "aguardando";
        v.Dados.Espera = espera;
        Salvar(v);
        object Base(string status) => new
        {
            type = "chat.turno", turno = turnoId, persona = persona.Id, nome = persona.Nome, tipo = t.Tipo, motor = persona.Motor, status,
            respondendoA, arestas = Turnos.Arestas(d, respondendoA, persona.Id), espera = status == "aguardando" ? espera : null,
        };
        Emitir(v.Dados, Base("aguardando"));
        Interlocked.Increment(ref v.Turnos);
        var tcs = new TaskCompletionSource<RespostaHumana>(TaskCreationOptions.RunContinuationsAsynchronously);
        v.Esperas[turnoId] = tcs;
        RespostaHumana r;
        try
        {
            var aguardar = tcs.Task;
            if (persona.Prazo is { } minutos)
            {
                var prazo = Task.Delay(TimeSpan.FromMinutes(minutos), v.Parar.Token)
                    .ContinueWith(_ => new RespostaHumana(persona.AoVencer == "encerrar" ? "encerrar" : "aprovar", Venceu: true), TaskContinuationOptions.OnlyOnRanToCompletion);
                aguardar = await Task.WhenAny(tcs.Task, prazo) == prazo ? prazo : tcs.Task;
            }
            r = await aguardar.WaitAsync(v.Parar.Token);
        }
        finally
        {
            v.Esperas.TryRemove(turnoId, out _);
            v.Dados.Espera = null;
            if (v.Dados.Status == "aguardando") v.Dados.Status = "rodando";
        }
        Emitir(v.Dados, Base("fim"));
        if (!r.Venceu && r.Acao is "aprovar" or "mudar")
            foreach (var p in respondendoA.Where(x => x != "usuario")) opts.AoFeedbackHumano?.Invoke(v.Dados.Id, p, r.Acao == "aprovar");
        if (r.Acao == "encerrar")
        {
            if (!r.Venceu)
                Registrar(v, Nova(t.Tipo == TipoPedido.Execucao ? "resultado" : "fala", persona.Nome, TextoHumano(r)) with { Persona = persona.Id, Turno = turnoId });
            throw new Encerrada(r.Venceu ? $"O prazo de {persona.Nome} venceu e a conversa foi encerrada." : $"Conversa encerrada por {persona.Nome}.");
        }
        // Pedir mudança: quem falou antes responde de novo, ao pedido.
        if (r.Acao == "mudar")
            foreach (var p in respondendoA.Where(x => x != "usuario")) v.Chamadas.Enqueue((p, [persona.Id]));
        return new Feito(new Resposta { Texto = TextoHumano(r) }, persona, turnoId);
    }

    private static Mensagem MensagemDe(Feito r, string tipo) => Nova(tipo, r.Persona.Nome, r.Resposta.Texto.Trim()) with
    {
        Persona = r.Persona.Id,
        Turno = r.TurnoId,
        Motor = r.Persona.Humana ? null : r.Persona.Motor,
        Modelo = r.Persona.Humana ? null : r.Persona.Modelo,
        Uso = r.Resposta.Uso,
        Detalhe = r.Resposta.Detalhe,
    };

    // ---- debate e resposta -----------------------------------------------------

    /// <summary>Quem já falou (para saber a quem cada uma responde).</summary>
    private static HashSet<string> Falaram(Conversa c)
    {
        lock (c.Mensagens) return c.Mensagens.Where(m => m is { Tipo: "fala", Persona: not null }).Select(m => m.Persona!).ToHashSet();
    }

    private async Task<Feito?> Fala(Viva v, string id, List<string>? respondendoA = null)
    {
        if (v.Turnos >= Limite) throw new LimiteAtingido();
        var d = opts.Ler();
        var ja = Falaram(v.Dados);
        var responde = respondendoA ?? Turnos.Predecessores(Rodada(d), id).Where(ja.Contains).ToList();
        var pedido = responde.Count > 0
            ? $"Sua vez: responda a {string.Join(" e ", responde.Select(r => NomeDe(d, r)))}{(responde.Contains("usuario") ? ", que falou diretamente com você" : "")}."
            : "Sua vez: abra a conversa com a sua visão sobre o assunto.";
        var r = await FazerTurno(v, id, new Turno(TipoPedido.Fala, $"Assunto: {v.Dados.Assunto}{ConversaAteAgora(v.Dados)}\n\n{pedido}", responde));
        if (r is not null) Registrar(v, MensagemDe(r, "fala") with { RespondendoA = responde });
        return r;
    }

    private async Task Sintese(Viva v)
    {
        var d = opts.Ler();
        var seq = Ordem(d);
        var moderadora = seq.FirstOrDefault(id => PersonaDe(d, id).Papel == "moderadora");
        string? ultima;
        lock (v.Dados.Mensagens) ultima = v.Dados.Mensagens.LastOrDefault(m => m is { Tipo: "fala", Persona: not null })?.Persona;
        var quem = moderadora ?? ultima ?? seq.FirstOrDefault();
        if (quem is null) return;
        var r = await FazerTurno(v, quem, new Turno(TipoPedido.Sintese,
            $"Assunto: {v.Dados.Assunto}{ConversaAteAgora(v.Dados)}\n\nAgora escreva a síntese final: a resposta polida ao assunto, clara e bem estruturada, com a recomendação, os principais argumentos de cada lado e os próximos passos. Pode usar títulos e listas."));
        if (r is not null)
        {
            Registrar(v, MensagemDe(r, "sintese"));
            v.Dados.Resposta = r.Resposta.Texto.Trim();
        }
    }

    /// <summary>As personas chamadas (com @, ou de volta por um pedido de mudança) falam antes da próxima vez.</summary>
    private async Task AtenderChamadas(Viva v)
    {
        while (v.Chamadas.TryDequeue(out var c)) await Fala(v, c.Id, c.A);
    }

    /// <summary>As rodadas, a partir de <paramref name="desde"/> (para retomar na vez seguinte à humana).</summary>
    private async Task Debate(Viva v, Posicao? desde = null)
    {
        desde ??= new Posicao(0, 0);
        for (var r = desde.Rodada; r < v.Dados.Rodadas && !v.SintetizarJa; r++)
        {
            var seq = Ordem(opts.Ler());
            for (var i = r == desde.Rodada ? desde.Indice : 0; i < seq.Count; i++)
            {
                v.Posicao = new Posicao(r, i);
                await AtenderChamadas(v);
                if (v.SintetizarJa) break;
                await Fala(v, seq[i]);
                if (v.SintetizarJa) break;
            }
            await AtenderChamadas(v);
        }
        v.Posicao = null;
        await AtenderChamadas(v);
        if (v.Dados.Modo == "resposta" || v.SintetizarJa)
        {
            v.SintetizarJa = false;
            await Sintese(v);
        }
    }

    // ---- tarefa ----------------------------------------------------------------

    private async Task<string?> TarefaDe(Viva v, string id, string tarefa, string? pai, bool topo, string? quemPediu = null)
    {
        var d = opts.Ler();
        var alcance = Turnos.Delegaveis(Rodada(d), id);
        var objetivo = topo ? "" : $"\n\nObjetivo geral do grupo: {v.Dados.Assunto}";

        // Sem para quem delegar (e não sendo a orquestradora): executa e entrega.
        if (alcance.Count == 0 && !topo)
        {
            var ex = await FazerTurno(v, id, new Turno(TipoPedido.Execucao,
                $"Tarefa delegada por {quemPediu ?? "a orquestradora"}: {tarefa}{objetivo}\n\nEntregue o resultado completo, pronto para usar."));
            if (ex is null) return null;
            Registrar(v, MensagemDe(ex, "resultado") with { Pai = pai });
            return ex.Resposta.Texto.Trim();
        }

        var nomes = alcance.Select(a => NomeDe(d, a)).ToList();
        var r = await FazerTurno(v, id, new Turno(TipoPedido.Tarefa, string.Join("\n\n",
            $"Tarefa{(quemPediu is null ? "" : $" delegada por {quemPediu}")}: {tarefa}{objetivo}",
            alcance.Count > 0
                ? $"Você pode delegar partes para: {string.Join(", ", nomes)}. Para delegar, escreva uma linha por parte, exatamente assim:\nDELEGAR <nome>: <o que essa persona deve fazer>\nAs delegadas trabalham em paralelo e você recebe os resultados para consolidar. Se der para fazer sozinha, entregue o resultado direto, sem linhas DELEGAR."
                : "Entregue o resultado completo."), Delegaveis: alcance));
        if (r is null) return null;

        var pessoas = alcance.Select(a => new Pessoa(a, NomeDe(d, a))).ToList();
        var lidas = Turnos.LerDelegacoes(r.Resposta.Texto, pessoas);
        var recusadas = new List<string>(lidas.Recusadas);
        var delegacoes = new List<Delegacao>();
        foreach (var dl in (r.Resposta.Delegacoes ?? []).Concat(lidas.Delegacoes))
        {
            var p = Turnos.AcharPessoa(dl.Para, pessoas);
            if (p is null) recusadas.Add(dl.Para);
            else if (!delegacoes.Exists(x => x.Para == p.Id)) delegacoes.Add(new Delegacao(p.Id, dl.Tarefa));
        }
        foreach (var nome in recusadas)
            Sistema(v, $"{r.Persona.Nome} tentou delegar para {nome}, que não está ao alcance das setas dela: delegação ignorada.");

        if (delegacoes.Count == 0)
        {
            Registrar(v, MensagemDe(r, topo ? "sintese" : "resultado") with { Pai = pai });
            return r.Resposta.Texto.Trim();
        }

        var plano = Registrar(v, MensagemDe(r, "tarefa") with { Pai = pai, Texto = lidas.Resto.Length > 0 ? lidas.Resto : r.Resposta.Texto.Trim() });
        foreach (var dl in delegacoes)
            Emitir(v.Dados, new
            {
                type = "chat.delegacao", de = id, deNome = r.Persona.Nome, para = dl.Para, paraNome = NomeDe(d, dl.Para), tarefa = dl.Tarefa,
                pai = plano.Id, arestas = Turnos.Arestas(d, [id], dl.Para),
            });
        // Em paralelo: cada delegada no seu motor.
        var resultados = await Task.WhenAll(delegacoes.Select(dl => TarefaDe(v, dl.Para, dl.Tarefa, plano.Id, false, r.Persona.Nome)));
        var juntos = string.Join("\n\n", delegacoes.Select((dl, i) =>
            $"### {NomeDe(d, dl.Para)} (tarefa: {dl.Tarefa})\n{resultados[i] ?? "(não entregou: veja o erro no chat)"}"));
        var c = await FazerTurno(v, id, new Turno(TipoPedido.Consolidar,
            $"Tarefa: {tarefa}{objetivo}\n\nResultados das delegações:\n\n{juntos}\n\nJunte tudo numa entrega final única, coerente e completa, resolvendo contradições entre os resultados."));
        if (c is null) return null;
        Registrar(v, MensagemDe(c, topo ? "sintese" : "resultado") with { Pai = pai });
        return c.Resposta.Texto.Trim();
    }

    private async Task Tarefa(Viva v)
    {
        var d = opts.Ler();
        var seq = Ordem(d);
        var orq = seq.FirstOrDefault(id => PersonaDe(d, id).Papel == "orquestradora") ?? seq.FirstOrDefault();
        if (orq is null) return;
        var entrega = await TarefaDe(v, orq, v.Dados.Assunto, null, true);
        if (entrega is not null) v.Dados.Resposta = entrega;
    }

    // ---- saída -------------------------------------------------------------------

    [GeneratedRegex(@"^```[\w-]*\n([\s\S]*?)\n```$")]
    private static partial Regex CercaRe();

    /// <summary>Tira a cerca de código (```markdown … ```) que o modelo às vezes põe em volta do documento.</summary>
    public static string SemCerca(string texto)
    {
        var t = texto.Trim();
        var m = CercaRe().Match(t);
        return (m.Success ? m.Groups[1].Value : t).Trim();
    }

    /// <summary>No fim: cada persona de saída preenche o seu template com o resultado da conversa.</summary>
    private async Task GerarSaidas(Viva v)
    {
        var d = opts.Ler();
        foreach (var id in Saidas(d))
        {
            var template = opts.Template?.Invoke(id) ?? "";
            var final = v.Dados.Resposta is { } resp ? $"\n\nResposta final:\n{resp}" : "";
            var r = await FazerTurno(v, id, new Turno(TipoPedido.Saida,
                $"Assunto: {v.Dados.Assunto}{ConversaAteAgora(v.Dados)}{final}\n\nTemplate de saída:\n\n{template}\n\nAgora preencha o template com o resultado da conversa e devolva só o markdown.",
                Turnos.Predecessores(d, id)));
            if (r is null) continue;
            var texto = SemCerca(r.Resposta.Texto);
            Registrar(v, MensagemDe(r, "saida") with { Arquivo = r.Persona.Arquivo, Texto = texto });
            opts.GravarSaida?.Invoke(v.Dados.Id, r.Persona.Arquivo, texto);
        }
    }

    // ---- ciclo de vida -------------------------------------------------------------

    /// <summary>Roda <paramref name="fn"/> como a execução da conversa, cuidando de parar, limite e fim.</summary>
    private void Executar(Viva v, Func<Task> fn)
    {
        v.Dados.Status = "rodando";
        v.Execucao = Task.Run(async () =>
        {
            try
            {
                await fn();
                // @menções feitas durante a última vez (ex.: a síntese) ainda são respondidas.
                await AtenderChamadas(v);
                v.Dados.Status = "terminada";
            }
            catch (OperationCanceledException) when (v.Parar.IsCancellationRequested)
            {
                return;
            }
            catch (Encerrada e)
            {
                Sistema(v, e.Message);
                v.Dados.Status = "terminada";
            }
            catch (LimiteAtingido)
            {
                Sistema(v, $"A conversa atingiu o limite de {Limite} turnos e foi encerrada.");
                v.Dados.Status = "terminada";
            }
            catch (Exception e)
            {
                Sistema(v, $"Erro na conversa: {e.Message}");
                v.Dados.Status = "terminada";
            }
            Salvar(v);
            Emitir(v.Dados, new { type = "chat.fim", status = v.Dados.Status, resposta = v.Dados.Resposta });
        });
    }

    public Conversa Iniciar(string? assunto, string? modo = null, int? rodadas = null)
    {
        assunto = assunto?.Trim() ?? "";
        if (assunto.Length == 0) throw new OpError(400, "Escreva o assunto da conversa.");
        modo ??= "debate";
        if (!Modos.Contains(modo)) throw new OpError(400, $"\"modo\" deve ser {string.Join(", ", Modos)}.");
        var n = rodadas ?? 2;
        if (n is < 1 or > 10) throw new OpError(400, "\"rodadas\" deve ser de 1 a 10.");
        var d = opts.Ler();
        if (d.Nodes.Count == 0) throw new OpError(400, "O diagrama não tem nenhuma persona: arraste um template para o canvas.");
        var dados = new Conversa
        {
            Id = $"c-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString("x", CultureInfo.InvariantCulture)}-{Guid.NewGuid().ToString("N")[..4]}",
            Diagrama = opts.Diagrama, Assunto = assunto, Modo = modo, Rodadas = n, CriadaEm = Agora(), AtualizadaEm = Agora(),
        };
        var v = new Viva(dados);
        _vivas[dados.Id] = v;
        Emitir(dados, new { type = "chat.inicio", assunto, modo, rodadas = n, ordem = Ordem(d).Select(id => NomeDe(d, id)).ToList() });
        Registrar(v, Nova("usuario", "Você", assunto));
        Executar(v, async () =>
        {
            await (modo == "tarefa" ? Tarefa(v) : Debate(v));
            await GerarSaidas(v);
        });
        return dados.Copia();
    }

    /// <summary>O usuário fala no chat. Com @persona, ela responde a seguir; depois do fim, alguém responde.</summary>
    public Mensagem Intervir(string id, string? texto)
    {
        var v = VivaDe(id);
        var t = texto?.Trim() ?? "";
        if (t.Length == 0) throw new OpError(400, "Mensagem vazia.");
        var d = opts.Ler();
        var alvo = Turnos.Mencionada(t, d.Nodes.Select(n => new Pessoa(n.Id, n.Text)));
        var msg = Registrar(v, Nova("usuario", "Você", t));
        if (v.Dados.Status is "rodando" or "aguardando")
        {
            if (alvo is not null) v.Chamadas.Enqueue((alvo, ["usuario"]));
        }
        else
        {
            // Conversa encerrada: responde a mencionada, a moderadora ou a última que falou.
            var seq = Ordem(d);
            string? ultima;
            lock (v.Dados.Mensagens) ultima = v.Dados.Mensagens.LastOrDefault(m => m.Persona is not null && seq.Contains(m.Persona))?.Persona;
            var quem = alvo ?? seq.FirstOrDefault(p => PersonaDe(d, p).Papel == "moderadora") ?? ultima ?? seq.FirstOrDefault();
            v.Parar = new CancellationTokenSource();
            v.Turnos = 0;
            Executar(v, async () =>
            {
                if (quem is not null) await Fala(v, quem, ["usuario"]);
            });
        }
        return msg;
    }

    /// <summary>Pede a resposta polida agora: depois da vez em andamento (ou já, se a conversa acabou).</summary>
    public void Sintetizar(string id)
    {
        var v = VivaDe(id);
        if (v.Dados.Status is "rodando" or "aguardando") v.SintetizarJa = true;
        else
        {
            v.Parar = new CancellationTokenSource();
            v.Turnos = 0;
            Executar(v, () => Sintese(v));
        }
    }

    public void Parar(string id)
    {
        var v = VivaDe(id);
        if (v.Dados.Status is not ("rodando" or "aguardando")) return;
        v.Dados.Status = "parada";
        v.Dados.Espera = null;
        v.Parar.Cancel();
        Sistema(v, "Conversa parada.");
        Emitir(v.Dados, new { type = "chat.fim", status = v.Dados.Status, resposta = v.Dados.Resposta });
    }

    /// <summary>A resposta do usuário na vez da persona humana (aprovar, pedir mudança ou encerrar).</summary>
    public void ResponderHumano(string id, RespostaHumana r)
    {
        var v = VivaDe(id);
        var e = v.Dados.Espera;
        if (v.Dados.Status != "aguardando" || e is null) throw new OpError(409, "A conversa não está esperando a sua resposta.");
        if (!AcoesHumanas.Contains(r.Acao)) throw new OpError(400, $"\"acao\" deve ser {string.Join(", ", AcoesHumanas)}.");
        if (v.Esperas.TryGetValue(e.Turno, out var tcs))
        {
            tcs.TrySetResult(r);
            return;
        }
        // O servidor reiniciou durante a espera: registra a resposta e retoma do ponto salvo.
        v.Dados.Espera = null;
        v.Parar = new CancellationTokenSource();
        v.Turnos = 0;
        Executar(v, async () =>
        {
            var d = opts.Ler();
            var persona = PersonaDe(d, e.Persona);
            Registrar(v, Nova(e.Tipo == TipoPedido.Execucao ? "resultado" : "fala", persona.Nome, TextoHumano(r)) with { Persona = e.Persona, Turno = e.Turno });
            if (r.Acao == "encerrar") throw new Encerrada($"Conversa encerrada por {persona.Nome}.");
            if (v.Dados.Modo == "tarefa")
            {
                Sistema(v, "A tarefa não pode continuar depois que o servidor reiniciou: a sua resposta ficou registrada.");
                return;
            }
            if (r.Acao == "mudar")
                foreach (var p in Turnos.Predecessores(Rodada(d), e.Persona)) v.Chamadas.Enqueue((p, [e.Persona]));
            var pos = e.Posicao ?? new Posicao(v.Dados.Rodadas, 0);
            await Debate(v, new Posicao(pos.Rodada, pos.Indice + 1));
            await GerarSaidas(v);
        });
    }

    public Conversa Obter(string id)
    {
        var c = Carregar(id).Copia();
        c.Pendentes = _vivas.TryGetValue(id, out var v) ? [.. v.EmAndamento.Values] : [];
        return c;
    }

    public List<ResumoConversa> Listar()
    {
        var ids = new HashSet<string>(_vivas.Keys);
        if (Directory.Exists(opts.Dir))
            foreach (var f in Directory.EnumerateFiles(opts.Dir, "*.json")) ids.Add(Path.GetFileNameWithoutExtension(f));
        var lista = new List<ResumoConversa>();
        foreach (var id in ids)
        {
            try
            {
                var c = Carregar(id);
                lista.Add(new ResumoConversa(c.Id, c.Assunto, c.Modo, c.Status, c.Mensagens.Count, c.CriadaEm, c.AtualizadaEm));
            }
            catch (OpError) { }
        }
        return lista.OrderByDescending(c => c.CriadaEm, StringComparer.Ordinal).ToList();
    }

    /// <summary>Espera a execução atual da conversa terminar (testes e MCP).</summary>
    public async Task<Conversa> Esperar(string id)
    {
        var v = VivaDe(id);
        Task atual;
        do
        {
            atual = v.Execucao;
            await atual;
        } while (atual != v.Execucao);
        return v.Dados.Copia();
    }

    /// <summary>Para tudo (ao fechar o diagrama).</summary>
    public void Fechar()
    {
        foreach (var v in _vivas.Values)
            if (v.Dados.Status is "rodando" or "aguardando") v.Parar.Cancel();
    }
}
