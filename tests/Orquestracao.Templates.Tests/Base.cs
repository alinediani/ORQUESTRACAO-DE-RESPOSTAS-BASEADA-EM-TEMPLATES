using Orquestracao.Templates;

namespace Orquestracao.Templates.Tests;

/// <summary>A base de fatos do repositório (dados/fatos.json) e um LLM simulado que responde por ela, sem rede.</summary>
public static class Base
{
    private static readonly Lazy<FonteDeFatosJson> _fonte = new(() => FonteDeFatosJson.Ler(FonteDeFatosJson.Achar(null, AppContext.BaseDirectory)));

    public static IFonteDeFatos Fonte => _fonte.Value;

    public static Entidade E(string nome) => Fonte.PorNome(nome) ?? throw new ArgumentException(nome);

    /// <summary>
    /// Um "LLM perfeito" para as famílias do protótipo: acha a entidade e o atributo pedidos e responde em uma frase
    /// (como o system prompt de geração pede). Para o resto, uma resposta sem dado da fonte (não reutilizável).
    /// </summary>
    public static string ResponderComoLlm(string pergunta)
    {
        var parser = new ParserHeuristico(Fonte);
        var e = parser.ExtrairSemLlm(pergunta).Entidade;
        if (e is null) return "Não sei responder a isso com segurança.";
        var n = Texto.Normalizar(pergunta);
        var de = e.Forma("de_entidade");
        var em = e.Forma("em_entidade");
        string? Attr(string a) => e.Atributos.GetValueOrDefault(a);
        if (n.Contains("capital")) return $"A capital {de} é {Attr("capital")}.";
        if (n.Contains("moeda")) return $"A moeda {de} é o {Attr("moeda")}.";
        if (n.Contains("lingua") || n.Contains("idioma")) return $"{Entidade.Maiuscula(em!)} fala-se {Attr("idioma")}.";
        if (n.Contains("continente")) return $"{Entidade.Maiuscula(e.Forma("o_entidade") ?? e.Nome)} fica na {Attr("continente")}.";
        if (n.Contains("simbolo")) return $"O símbolo químico {de} é {Attr("simbolo")}.";
        if (n.Contains("numero atomico")) return $"O número atômico {de} é {Attr("numero_atomico")}.";
        return "Não sei responder a isso com segurança.";
    }

    public static LlmSimulado Llm(double custo = 0.002) => new((_, pergunta) => ResponderComoLlm(pergunta), tokensEntrada: 60, tokensSaida: 15, custo: custo);

    public static Orquestrador Orquestrador(ITemplateStore? store = null, ConfiguracaoOrquestrador? cfg = null) =>
        new(new ParserHeuristico(Fonte), store ?? new TemplateStoreMemoria(), Fonte, cfg);
}
