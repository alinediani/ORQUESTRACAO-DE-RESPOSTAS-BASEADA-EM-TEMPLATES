using System.Diagnostics;

namespace Orquestracao.Templates;

/// <summary>
/// O orquestrador de respostas por template (o fluxo da Figura 1 do TCC):
/// <list type="number">
/// <item>o parser extrai a estrutura e os slots da pergunta e gera a chave (R01);</item>
/// <item>o motor consulta a chave na base chave-valor (R02);</item>
/// <item>chave existe (e o peso está acima do limiar): busca o template, o dado na fonte externa, preenche os slots e entrega — sem chamar o LLM de geração (R03);</item>
/// <item>chave nova: o LLM padrão responde, o template é extraído da resposta e salvo com peso inicial (R04);</item>
/// <item>o feedback ajusta o peso por média móvel, e template abaixo do limiar é descartado (R05);</item>
/// <item>cada chamada ao LLM, os tokens, o custo e a latência são contados (R07).</item>
/// </list>
/// Nós da visualização: ui, parser, motor, busca, fonte, adapta, entrega, feedback, llm, extrai, salva, fim.
/// </summary>
public sealed class Orquestrador(IParser parser, ITemplateStore store, IFonteDeFatos fonte, ConfiguracaoOrquestrador? configuracao = null)
{
    public ConfiguracaoOrquestrador Configuracao { get; } = configuracao ?? new();
    public Estatisticas Estatisticas { get; } = new();
    public ITemplateStore Store => store;
    public IFonteDeFatos Fonte => fonte;

    /// <summary>Cada etapa do fluxo, para a visualização: (de, para, rótulo).</summary>
    public event Action<EtapaFluxo>? Etapa;

    /// <summary>Cada pergunta respondida (pelo painel, pelo MCP ou por uma persona numa conversa).</summary>
    public event Action<ResultadoOrquestrador>? Respondido;

    /// <summary>O system prompt do LLM padrão: respostas factuais curtas, que repetem o assunto (reaproveitáveis).</summary>
    public const string SystemGeracao =
        "Responda à pergunta em uma única frase completa e factual, em português do Brasil, retomando o assunto da pergunta " +
        "(por exemplo: \"A capital da França é Paris.\"). Não acrescente explicações nem comentários.";

    /// <summary>Responde uma pergunta. <paramref name="forcarLlm"/>: um feedback ruim manda a próxima resposta ao LLM.</summary>
    public async Task<ResultadoOrquestrador> Responder(string pergunta, ILlm geracao, CancellationToken ct = default, bool forcarLlm = false, string? systemExtra = null)
    {
        var relogio = Stopwatch.StartNew();
        var etapas = new List<EtapaFluxo>();
        void Passo(string de, string para, string? rotulo = null)
        {
            var e = new EtapaFluxo(de, para, rotulo);
            etapas.Add(e);
            Etapa?.Invoke(e);
        }

        Passo("ui", "parser", Curto(pergunta));
        var (estrutura, usoParser) = await parser.Extrair(pergunta, ct);
        Passo("parser", "motor", estrutura.Chave);

        string? motivo;
        var existente = store.Obter(estrutura.Chave);
        if (forcarLlm) motivo = "feedback ruim: a resposta volta ao LLM";
        else if (!estrutura.TemSlot) motivo = "sem entidade conhecida na pergunta";
        else if (existente is null) motivo = "chave nova";
        else if (existente.Peso < Configuracao.Limiar) motivo = "peso abaixo do limiar";
        else
        {
            // Caminho verde: template + fonte, sem LLM de geração.
            Passo("motor", "busca", "chave existe");
            Passo("busca", "fonte", estrutura.Entidade!.Nome);
            var texto = Extrator.Preencher(existente.Template, estrutura.Entidade, fonte);
            if (texto is not null)
            {
                Passo("fonte", "adapta");
                Passo("adapta", "entrega");
                store.Salvar(existente with { Usos = existente.Usos + 1 });
                return Fechar(new ResultadoOrquestrador
                {
                    Pergunta = pergunta, Texto = texto, Caminho = Caminho.Template, Chave = estrutura.Chave, Entidade = estrutura.Entidade.Nome,
                    Template = existente.Template, Peso = existente.Peso, ChamadasParser = usoParser is null ? 0 : 1,
                    TokensEntrada = usoParser?.Entrada ?? 0, TokensSaida = usoParser?.Saida ?? 0, Custo = usoParser?.Custo ?? 0, Etapas = etapas,
                }, relogio);
            }
            motivo = "a fonte não tem o dado para preencher o template";
        }

        // Caminho roxo: o LLM padrão responde, e o template é extraído e salvo.
        Passo("motor", "llm", motivo);
        var system = systemExtra is null ? SystemGeracao : $"{systemExtra}\n---\n{SystemGeracao}";
        var r = await geracao.Completar(system, pergunta, ct);
        var resposta = r.Texto.Trim();
        Passo("llm", "extrai");
        string? template = estrutura.Entidade is null ? null : Extrator.ExtrairTemplate(resposta, estrutura.Entidade);
        var salvo = false;
        double? peso = null;
        if (template is not null)
        {
            Passo("extrai", "salva", estrutura.Chave);
            // O LLM chegou ao mesmo template: o peso (e o feedback que ele já recebeu) continua valendo.
            if (existente is not null && existente.Template == template) peso = existente.Peso;
            else
            {
                // Chave nova, ou um template reformulado depois de um feedback ruim: nasce com o peso inicial.
                store.Salvar(new TemplateResposta { Chave = estrutura.Chave, Template = template, Peso = Configuracao.PesoInicial });
                peso = Configuracao.PesoInicial;
                salvo = true;
            }
            Passo("salva", "entrega");
        }
        else
        {
            Passo("extrai", "entrega", "não reutilizável");
        }
        return Fechar(new ResultadoOrquestrador
        {
            Pergunta = pergunta, Texto = resposta, Caminho = Caminho.Llm, Chave = estrutura.Chave, Entidade = estrutura.Entidade?.Nome, Template = template, Peso = peso,
            TemplateSalvo = salvo, Motivo = motivo, ChamadasGeracao = 1, ChamadasParser = usoParser is null ? 0 : 1,
            TokensEntrada = r.Entrada + (usoParser?.Entrada ?? 0), TokensSaida = r.Saida + (usoParser?.Saida ?? 0), Custo = r.Custo + (usoParser?.Custo ?? 0),
            Etapas = etapas,
        }, relogio);
    }

    private ResultadoOrquestrador Fechar(ResultadoOrquestrador r, Stopwatch relogio)
    {
        var final = r with { LatenciaMs = relogio.Elapsed.TotalMilliseconds };
        Estatisticas.Registrar(final);
        Respondido?.Invoke(final);
        return final;
    }

    /// <summary>
    /// R05: o feedback do usuário sobre a resposta da chave. Peso novo = α·nota + (1 − α)·peso (nota 1 ou 0);
    /// abaixo do limiar, o template é descartado. Devolve o template atualizado (null se não havia, ou se foi descartado).
    /// </summary>
    public TemplateResposta? Feedback(string chave, bool bom)
    {
        var t = store.Obter(chave);
        Etapa?.Invoke(new EtapaFluxo("entrega", "feedback"));
        if (t is null)
        {
            Estatisticas.RegistrarFeedback(bom, descartou: false);
            Etapa?.Invoke(bom ? new EtapaFluxo("feedback", "fim", "bom") : new EtapaFluxo("feedback", "llm", "ruim"));
            return null;
        }
        var a = Configuracao.Alfa;
        var peso = Math.Round(a * (bom ? 1 : 0) + (1 - a) * t.Peso, 6);
        var descartou = peso < Configuracao.Limiar;
        Estatisticas.RegistrarFeedback(bom, descartou);
        if (descartou)
        {
            store.Remover(chave);
            Etapa?.Invoke(new EtapaFluxo("feedback", "llm", $"descartado (peso {peso:0.00})"));
            return null;
        }
        var novo = t with { Peso = peso, FeedbacksBons = t.FeedbacksBons + (bom ? 1 : 0), FeedbacksRuins = t.FeedbacksRuins + (bom ? 0 : 1) };
        store.Salvar(novo);
        Etapa?.Invoke(bom ? new EtapaFluxo("feedback", "fim", $"bom · peso {peso:0.00}") : new EtapaFluxo("feedback", "llm", $"ruim · peso {peso:0.00}"));
        return novo;
    }

    private static string Curto(string s) => s.Length <= 40 ? s : s[..39] + "…";
}
