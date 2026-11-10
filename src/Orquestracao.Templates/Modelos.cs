namespace Orquestracao.Templates;

/// <summary>
/// O que o parser extrai da pergunta (R01): a estrutura, que vira a chave na base chave-valor, e os slots.
/// "Qual é a capital da França?" → chave "capital {pais}", entidade França.
/// </summary>
public sealed record Estrutura(string Chave, Entidade? Entidade, string Pergunta)
{
    /// <summary>Sem entidade conhecida não há o que buscar na fonte: a pergunta vai para o LLM e não vira template.</summary>
    public bool TemSlot => Entidade is not null;
}

/// <summary>Um template de resposta guardado na base chave-valor (R04): a forma da resposta, com slots, e o peso.</summary>
public sealed record TemplateResposta
{
    public required string Chave { get; init; }
    /// <summary>"A capital {de_entidade} é {capital}." — os slots de entidade e os atributos da fonte.</summary>
    public required string Template { get; init; }
    /// <summary>A confiança no template, de 0 a 1: começa em 0,5 e muda com o feedback (R05).</summary>
    public double Peso { get; init; } = 0.5;
    /// <summary>Quantas vezes respondeu sem chamar o LLM de geração.</summary>
    public int Usos { get; init; }
    public int FeedbacksBons { get; init; }
    public int FeedbacksRuins { get; init; }
    public string CriadoEm { get; init; } = "";
    public string AtualizadoEm { get; init; } = "";
}

/// <summary>Uma etapa do fluxo da Figura 1, para a visualização (F6): o canvas acende a seta correspondente.</summary>
public sealed record EtapaFluxo(string De, string Para, string? Rotulo = null);

/// <summary>Caminho seguido por uma pergunta.</summary>
public static class Caminho
{
    /// <summary>Chave existente: template + fonte, sem LLM de geração (o caminho verde da Figura 1).</summary>
    public const string Template = "template";
    /// <summary>Chave nova (ou template ruim): o LLM padrão responde (o caminho roxo).</summary>
    public const string Llm = "llm";
}

/// <summary>A resposta do orquestrador para uma pergunta, com tudo o que a visualização e o experimento precisam.</summary>
public sealed record ResultadoOrquestrador
{
    public string Pergunta { get; init; } = "";
    public required string Texto { get; init; }
    public required string Caminho { get; init; }
    public required string Chave { get; init; }
    public string? Entidade { get; init; }
    public string? Template { get; init; }
    public double? Peso { get; init; }
    /// <summary>No caminho do LLM: se o template foi extraído e salvo (nem toda resposta é reutilizável).</summary>
    public bool TemplateSalvo { get; init; }
    /// <summary>Por que o caminho do template não foi usado (chave nova, peso baixo, fonte sem o dado, feedback ruim…).</summary>
    public string? Motivo { get; init; }
    public int ChamadasGeracao { get; init; }
    public int ChamadasParser { get; init; }
    /// <summary>No segundo fluxo: a chamada ao LLM normalizador (0 ou 1) e a pergunta que ele devolveu ao parser.</summary>
    public int ChamadasNormalizador { get; init; }
    public string? PerguntaNormalizada { get; init; }
    /// <summary>A trava: chamada ao LLM validador (0 ou 1) e se ele recusou o template extraído.</summary>
    public int ChamadasValidador { get; init; }
    public bool TemplateRecusado { get; init; }
    /// <summary>O estilizador: o pedido de estilo separado da pergunta, a resposta do núcleo (antes do estilo) e a chamada (0 ou 1).</summary>
    public string? Estilo { get; init; }
    public string? TextoNucleo { get; init; }
    public int ChamadasEstilizador { get; init; }
    public long TokensEntrada { get; init; }
    public long TokensSaida { get; init; }
    public double Custo { get; init; }
    public double LatenciaMs { get; init; }
    public List<EtapaFluxo> Etapas { get; init; } = [];
}

/// <summary>As métricas do experimento (R07): quantas vezes cada LLM foi chamado, tokens, custo e latência.</summary>
public sealed class Estatisticas
{
    private readonly Lock _trava = new();

    public int Perguntas { get; private set; }
    /// <summary>Respondidas pelo template, sem chamar o LLM de geração.</summary>
    public int AcertosTemplate { get; private set; }
    public int ChamadasGeracao { get; private set; }
    public int ChamadasParser { get; private set; }
    public long TokensEntrada { get; private set; }
    public long TokensSaida { get; private set; }
    public double Custo { get; private set; }
    public double LatenciaTotalMs { get; private set; }
    public int TemplatesSalvos { get; private set; }
    public int FeedbacksBons { get; private set; }
    public int FeedbacksRuins { get; private set; }
    public int TemplatesDescartados { get; private set; }

    public double TaxaAcertoTemplate => Perguntas == 0 ? 0 : (double)AcertosTemplate / Perguntas;
    public double LatenciaMediaMs => Perguntas == 0 ? 0 : LatenciaTotalMs / Perguntas;

    /// <summary>Começa a partir dos totais já gravados (o SQLite), para as estatísticas sobreviverem ao reinício.</summary>
    public static Estatisticas De(IRegistroDePerguntas? registro)
    {
        var e = new Estatisticas();
        if (registro?.Totais() is not { } t) return e;
        (e.Perguntas, e.AcertosTemplate, e.ChamadasGeracao, e.ChamadasParser) = (t.Perguntas, t.AcertosTemplate, t.ChamadasGeracao, t.ChamadasParser);
        (e.TokensEntrada, e.TokensSaida, e.Custo, e.LatenciaTotalMs) = (t.TokensEntrada, t.TokensSaida, t.Custo, t.LatenciaTotalMs);
        (e.TemplatesSalvos, e.FeedbacksBons, e.FeedbacksRuins, e.TemplatesDescartados) = (t.TemplatesSalvos, t.FeedbacksBons, t.FeedbacksRuins, t.TemplatesDescartados);
        return e;
    }

    public void Registrar(ResultadoOrquestrador r)
    {
        lock (_trava)
        {
            Perguntas++;
            if (r.Caminho == Caminho.Template) AcertosTemplate++;
            ChamadasGeracao += r.ChamadasGeracao;
            ChamadasParser += r.ChamadasParser;
            TokensEntrada += r.TokensEntrada;
            TokensSaida += r.TokensSaida;
            Custo += r.Custo;
            LatenciaTotalMs += r.LatenciaMs;
            if (r.TemplateSalvo) TemplatesSalvos++;
        }
    }

    public void RegistrarFeedback(bool bom, bool descartou)
    {
        lock (_trava)
        {
            if (bom) FeedbacksBons++;
            else FeedbacksRuins++;
            if (descartou) TemplatesDescartados++;
        }
    }

    public object Resumo()
    {
        lock (_trava)
            return new
            {
                perguntas = Perguntas, acertosTemplate = AcertosTemplate, taxaAcertoTemplate = TaxaAcertoTemplate,
                chamadasGeracao = ChamadasGeracao, chamadasParser = ChamadasParser, tokensEntrada = TokensEntrada, tokensSaida = TokensSaida,
                custo = Custo, latenciaMediaMs = LatenciaMediaMs, templatesSalvos = TemplatesSalvos,
                feedbacksBons = FeedbacksBons, feedbacksRuins = FeedbacksRuins, templatesDescartados = TemplatesDescartados,
            };
    }
}

/// <summary>Os parâmetros do mecanismo de peso (R05), calibráveis no experimento.</summary>
public sealed record ConfiguracaoOrquestrador
{
    /// <summary>Peso com que todo template novo nasce.</summary>
    public double PesoInicial { get; init; } = 0.5;
    /// <summary>Fator da média móvel exponencial: peso = α·nota + (1 − α)·peso, com nota 1 (bom) ou 0 (ruim).</summary>
    public double Alfa { get; init; } = 0.3;
    /// <summary>Abaixo deste peso o template é descartado (e a próxima pergunta da família volta ao LLM).</summary>
    public double Limiar { get; init; } = 0.2;
    /// <summary>Quantas vezes uma resposta mal avaliada volta ao LLM numa conversa.</summary>
    public int LimiteTentativas { get; init; } = 2;
}
