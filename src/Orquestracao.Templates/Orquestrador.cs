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
    /// <summary>Onde cada pergunta e cada feedback ficam gravados (o SQLite), quando a base suporta.</summary>
    private readonly IRegistroDePerguntas? _registro = store as IRegistroDePerguntas;

    public Estatisticas Estatisticas { get; } = Estatisticas.De(store as IRegistroDePerguntas);
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

    /// <summary>
    /// O system prompt do normalizador (o segundo fluxo): um LLM pequeno reescreve a pergunta numa forma padronizada antes
    /// do parser, para perguntas iguais escritas de jeitos diferentes caírem na mesma chave.
    /// </summary>
    public const string SystemNormalizacao =
        "Reescreva a pergunta do usuário na forma mais simples e padronizada possível para o que ela pede, em português do Brasil: " +
        "tire cumprimentos, histórias e detalhes que não mudam o que é pedido, mantenha os nomes próprios e os números, e use sempre " +
        "a mesma redação para o mesmo tipo de pedido (por exemplo: \"Onde X nasceu?\", \"Qual é a capital de X?\", \"X tem cura?\"). " +
        "Devolva só a pergunta reescrita, em uma linha.";

    /// <summary>
    /// O system prompt do validador (a trava): antes de salvar um template novo, um LLM pequeno confere se ele pode ser
    /// reaproveitado, isto é, se o que a pergunta pede está num slot e se nenhum trecho fixo depende da entidade.
    /// </summary>
    public const string SystemValidacao =
        "Você valida templates de resposta que serão reaproveitados para outras entidades. Recebe a pergunta, a resposta original e o template, " +
        "em que os trechos entre chaves (como {capital}, {especialista}, {p19}, {de_entidade}) serão preenchidos com dados de uma base para cada nova entidade. " +
        "Responda SIM somente se: (1) o que a pergunta pede está num slot, e (2) nenhum trecho fixo do template depende da entidade, isto é, o texto fixo " +
        "continuaria verdadeiro e adequado para qualquer outra entidade do mesmo tipo. Responda NÃO se algum trecho fixo afirma algo que só vale para a " +
        "entidade original (por exemplo, se tem cura, uma definição, um tipo de obra, um detalhe específico). Responda com SIM ou NÃO, seguido de um motivo curto.";

    /// <summary>
    /// O system prompt do estilizador: aplica o estilo pedido sobre a resposta do núcleo factual, sem mudar nenhum dado.
    /// </summary>
    public const string SystemEstilizacao =
        "Reescreva a resposta no estilo pedido, em português do Brasil, em no máximo três frases curtas. Não mude, não acrescente e não omita " +
        "nenhum dado da resposta (nomes, números, símbolos). Devolva só o texto reescrito.";

    /// <summary>As chaves cujo template o validador recusou: as perguntas delas vão sempre ao LLM.</summary>
    private readonly HashSet<string> _recusadas = [];
    private readonly Lock _travaRecusadas = new();

    /// <summary>
    /// Responde uma pergunta. <paramref name="forcarLlm"/>: um feedback ruim manda a próxima resposta ao LLM.
    /// <paramref name="normalizador"/>: o segundo fluxo, em que a pergunta passa por um LLM normalizador antes do parser; a
    /// normalização só decide a rota (a chave), e o LLM de geração continua recebendo a pergunta original.
    /// </summary>
    public async Task<ResultadoOrquestrador> Responder(string pergunta, ILlm geracao, CancellationToken ct = default, bool forcarLlm = false, string? systemExtra = null,
        ILlm? normalizador = null, ILlm? validador = null, ILlm? estilizador = null)
    {
        // O estilizador: o núcleo factual segue o fluxo (template + fonte) e só no fim o estilo é aplicado por um LLM pequeno.
        if (estilizador is not null && Estilos.Separar(pergunta) is var (nucleo, estilo))
        {
            var rn = await Responder(nucleo, geracao, ct, forcarLlm, systemExtra, normalizador, validador);
            var re = await estilizador.Completar(SystemEstilizacao, $"Estilo pedido: {estilo}\nResposta: {rn.Texto}", ct);
            Etapa?.Invoke(new EtapaFluxo("entrega", "fim", "estilo: " + Curto(estilo)));
            return rn with
            {
                Pergunta = pergunta, Texto = re.Texto.Trim(), Estilo = estilo, TextoNucleo = rn.Texto, ChamadasEstilizador = 1,
                TokensEntrada = rn.TokensEntrada + re.Entrada, TokensSaida = rn.TokensSaida + re.Saida, Custo = rn.Custo + re.Custo,
            };
        }
        var relogio = Stopwatch.StartNew();
        var etapas = new List<EtapaFluxo>();
        void Passo(string de, string para, string? rotulo = null)
        {
            var e = new EtapaFluxo(de, para, rotulo);
            etapas.Add(e);
            Etapa?.Invoke(e);
        }

        RespostaLlm? usoNorm = null;
        var paraParser = pergunta;
        if (normalizador is not null)
        {
            usoNorm = await normalizador.Completar(SystemNormalizacao, pergunta, ct);
            paraParser = usoNorm.Texto.Trim() is { Length: > 0 } n ? n : pergunta;
            Passo("ui", "parser", "normalizada: " + Curto(paraParser));
        }
        else Passo("ui", "parser", Curto(pergunta));
        var (estrutura, usoParserBruto) = await parser.Extrair(paraParser, ct);
        // O uso do normalizador entra junto com o do parser (os dois decidem a rota).
        var usoParser = Somar(usoParserBruto, usoNorm);
        var chamadasRota = (usoParserBruto is null ? 0 : 1);
        var chamadasNorm = usoNorm is null ? 0 : 1;
        Passo("parser", "motor", estrutura.Chave);

        string? motivo;
        var existente = store.Obter(estrutura.Chave);
        bool Recusada() { lock (_travaRecusadas) return _recusadas.Contains(estrutura.Chave); }
        if (forcarLlm) motivo = "feedback ruim: a resposta volta ao LLM";
        else if (validador is not null && Recusada()) motivo = "o validador recusou o template desta chave";
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
                    Template = existente.Template, Peso = existente.Peso, ChamadasParser = chamadasRota, ChamadasNormalizador = chamadasNorm, PerguntaNormalizada = usoNorm is null ? null : paraParser,
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
        string? template = estrutura.Entidade is null || (validador is not null && Recusada()) ? null : Extrator.ExtrairTemplate(resposta, estrutura.Entidade);
        var salvo = false;
        RespostaLlm? usoValidador = null;
        var recusado = false;
        if (template is not null && validador is not null && (existente is null || existente.Template != template))
        {
            // A trava: o template novo só é salvo se o validador confirmar que ele serve para outras entidades.
            usoValidador = await validador.Completar(SystemValidacao, $"Pergunta: {pergunta}\nResposta: {resposta}\nTemplate: {template}", ct);
            var veredito = Texto.Normalizar(usoValidador.Texto);
            if (!veredito.StartsWith("sim", StringComparison.Ordinal))
            {
                recusado = true;
                lock (_travaRecusadas) _recusadas.Add(estrutura.Chave);
                Passo("extrai", "entrega", "recusado pelo validador");
                template = null;
            }
        }
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
        else if (!recusado)
        {
            Passo("extrai", "entrega", "não reutilizável");
        }
        usoParser = Somar(usoParser, usoValidador);
        return Fechar(new ResultadoOrquestrador
        {
            Pergunta = pergunta, Texto = resposta, Caminho = Caminho.Llm, Chave = estrutura.Chave, Entidade = estrutura.Entidade?.Nome, Template = template, Peso = peso,
            TemplateSalvo = salvo, Motivo = motivo, ChamadasGeracao = 1, ChamadasParser = chamadasRota, ChamadasNormalizador = chamadasNorm, PerguntaNormalizada = usoNorm is null ? null : paraParser,
            ChamadasValidador = usoValidador is null ? 0 : 1, TemplateRecusado = recusado,
            TokensEntrada = r.Entrada + (usoParser?.Entrada ?? 0), TokensSaida = r.Saida + (usoParser?.Saida ?? 0), Custo = r.Custo + (usoParser?.Custo ?? 0),
            Etapas = etapas,
        }, relogio);
    }

    private ResultadoOrquestrador Fechar(ResultadoOrquestrador r, Stopwatch relogio)
    {
        var final = r with { LatenciaMs = relogio.Elapsed.TotalMilliseconds };
        Estatisticas.Registrar(final);
        _registro?.Registrar(final);
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
            _registro?.RegistrarFeedback(chave, bom, null, false);
            Etapa?.Invoke(bom ? new EtapaFluxo("feedback", "fim", "bom") : new EtapaFluxo("feedback", "llm", "ruim"));
            return null;
        }
        var a = Configuracao.Alfa;
        var peso = Math.Round(a * (bom ? 1 : 0) + (1 - a) * t.Peso, 6);
        var descartou = peso < Configuracao.Limiar;
        Estatisticas.RegistrarFeedback(bom, descartou);
        _registro?.RegistrarFeedback(chave, bom, peso, descartou);
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

    private static RespostaLlm? Somar(RespostaLlm? a, RespostaLlm? b) =>
        a is null ? b : b is null ? a : a with { Entrada = a.Entrada + b.Entrada, Saida = a.Saida + b.Saida, Custo = a.Custo + b.Custo };

    private static string Curto(string s) => s.Length <= 40 ? s : s[..39] + "…";
}
