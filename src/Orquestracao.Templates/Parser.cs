using System.Text.Json;
using System.Text.RegularExpressions;

namespace Orquestracao.Templates;

/// <summary>R01: extrai a estrutura e os slots de uma pergunta e gera a chave correspondente.</summary>
public interface IParser
{
    /// <summary>A estrutura da pergunta e, quando o parser usou um LLM, o uso dele.</summary>
    Task<(Estrutura Estrutura, RespostaLlm? Uso)> Extrair(string pergunta, CancellationToken ct = default);
}

/// <summary>
/// Parser sem LLM: acha a entidade da pergunta na fonte de fatos (o nome mais longo que aparecer), troca a
/// entidade (com a preposição: "da França", "no Japão") pelo tipo dela e tira as palavras de preenchimento.
/// "Qual é a capital da França?" e "Me diga a capital do Japão" geram a mesma chave: "capital {pais}".
/// </summary>
public sealed partial class ParserHeuristico(IFonteDeFatos fonte) : IParser
{
    /// <summary>Palavras que não mudam a estrutura da pergunta.</summary>
    private static readonly HashSet<string> Preenchimento =
    [
        "qual", "quais", "e", "eh", "a", "o", "os", "as", "um", "uma", "me", "diga", "diz", "fala", "fale", "voce", "sabe", "sabes",
        "por", "favor", "ai", "entao", "eu", "quero", "saber", "gostaria", "pode", "poderia", "dizer", "informar", "seria", "atual",
        "oficial", "hoje", "la",
    ];

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacos();

    public Task<(Estrutura Estrutura, RespostaLlm? Uso)> Extrair(string pergunta, CancellationToken ct = default) => Task.FromResult<(Estrutura Estrutura, RespostaLlm? Uso)>((ExtrairSemLlm(pergunta), null));

    public Estrutura ExtrairSemLlm(string pergunta)
    {
        var n = Texto.Normalizar(pergunta);
        Entidade? achada = null;
        string? nomeAchado = null;
        foreach (var e in fonte.Entidades)
            foreach (var nome in e.Apelidos.Prepend(e.Nome).Select(Texto.Normalizar))
                if ((nomeAchado is null || nome.Length > nomeAchado.Length) && Regex.IsMatch(n, $@"(^|\s){Regex.Escape(nome)}($|\s)"))
                    (achada, nomeAchado) = (e, nome);
        if (achada is not null)
            n = Regex.Replace(n, $@"(^|\s)((de|da|do|das|dos|na|no|nas|nos|em|o|a|os|as)\s+)?{Regex.Escape(nomeAchado!)}($|\s)", $" {{{achada.Tipo}}} ");
        var palavras = Espacos().Split(n.Trim()).Where(p => p.Length > 0 && !Preenchimento.Contains(p));
        return new Estrutura(string.Join(' ', palavras), achada, pergunta);
    }
}

/// <summary>
/// Parser pelo LLM (o Claude Haiku, no TCC): pede a estrutura com a entidade no lugar de {entidade} e o nome
/// dela. A entidade é conferida na fonte de fatos, para a chave usar o tipo ("capital {pais}").
/// </summary>
public sealed class ParserLlm(ILlm llm, IFonteDeFatos fonte) : IParser
{
    public const string System =
        "Você extrai a estrutura de perguntas factuais. Devolva só um JSON, sem comentários: " +
        "{\"estrutura\": \"a pergunta com a entidade trocada por {entidade}, sem artigos nem preposições antes dela\", \"entidade\": \"o nome da entidade, ou null\"}. " +
        "Exemplo: \"Qual é a capital da França?\" → {\"estrutura\": \"qual é a capital de {entidade}\", \"entidade\": \"França\"}.";

    private sealed record Saida(string? Estrutura, string? Entidade);

    public async Task<(Estrutura Estrutura, RespostaLlm? Uso)> Extrair(string pergunta, CancellationToken ct = default)
    {
        var r = await llm.Completar(System, pergunta, ct);
        var texto = r.Texto.Trim();
        var inicio = texto.IndexOf('{');
        var fim = texto.LastIndexOf('}');
        Saida? s = null;
        if (inicio >= 0 && fim > inicio)
        {
            try
            {
                s = JsonSerializer.Deserialize<Saida>(texto[inicio..(fim + 1)], new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (JsonException) { }
        }
        var entidade = s?.Entidade is { Length: > 0 } nome ? fonte.PorNome(nome) : null;
        var estrutura = s?.Estrutura ?? pergunta;
        estrutura = estrutura.Replace("{entidade}", entidade is null ? "{entidade}" : $"{{{entidade.Tipo}}}");
        return (new Estrutura(Texto.Normalizar(estrutura), entidade, pergunta), r);
    }
}

/// <summary>Heurístico primeiro; só quando ele não acha a entidade, o LLM (que custa uma chamada).</summary>
public sealed class ParserHibrido(ParserHeuristico heuristico, ParserLlm llm) : IParser
{
    public async Task<(Estrutura Estrutura, RespostaLlm? Uso)> Extrair(string pergunta, CancellationToken ct = default)
    {
        var e = heuristico.ExtrairSemLlm(pergunta);
        return e.TemSlot ? (e, null) : await llm.Extrair(pergunta, ct);
    }
}
