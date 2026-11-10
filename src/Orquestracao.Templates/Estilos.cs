using System.Text.RegularExpressions;

namespace Orquestracao.Templates;

/// <summary>
/// O pedido de estilo de uma pergunta ("Responda como um pirata: qual é a capital da França?"). Com o estilizador ligado,
/// o orquestrador separa o estilo do núcleo factual: o núcleo segue o fluxo normal (template + fonte) e, no fim, um LLM
/// pequeno aplica o estilo sobre a resposta, sem mudar os dados. É a analogia do texto com "fundo fixo + pessoa estilizada".
/// </summary>
public static partial class Estilos
{
    /// <summary>"Responda/Explique/Escreva/Reescreva … : pergunta" — o que vem antes dos dois-pontos é o pedido de estilo.</summary>
    [GeneratedRegex(@"^\s*((?:responda|explique|escreva|reescreva|conte)\b[^:?]{0,100}):\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PedidoDeEstilo();

    /// <summary>(núcleo, estilo) quando a pergunta traz um pedido de estilo; null quando não traz.</summary>
    public static (string Nucleo, string Estilo)? Separar(string pergunta)
    {
        var m = PedidoDeEstilo().Match(pergunta);
        if (!m.Success) return null;
        var nucleo = m.Groups[2].Value.Trim();
        return nucleo.Length == 0 ? null : (Entidade.Maiuscula(nucleo), m.Groups[1].Value.Trim());
    }

    /// <summary>Os estilos do cenário 7b: o nome e a instrução que vai antes da pergunta.</summary>
    public static readonly (string Nome, string Instrucao)[] DoCenario =
    [
        ("pirata", "Responda como um pirata"),
        ("cordel", "Responda em forma de cordel"),
        ("crianca", "Explique para uma criança de 5 anos"),
        ("novela", "Responda como numa cena de novela"),
        ("teleprompter", "Responda em estilo teleprompter, como um âncora de telejornal"),
    ];
}
