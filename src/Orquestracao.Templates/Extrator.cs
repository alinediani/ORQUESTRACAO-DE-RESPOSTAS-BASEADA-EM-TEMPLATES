using System.Text.RegularExpressions;

namespace Orquestracao.Templates;

/// <summary>
/// Extrair o template de uma resposta (R04) e preencher os slots de um template com a fonte (R03), sem LLM.
/// <code>
/// "A capital da França é Paris."  ⇄  "A capital {de_entidade} é {capital}."
/// </code>
/// </summary>
public static partial class Extrator
{
    [GeneratedRegex(@"\{([A-Za-z0-9_]+)\}")]
    private static partial Regex Slot();

    private static Regex PalavraInteira(string valor) => new($@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(valor)}(?![\p{{L}}\p{{N}}])");

    /// <summary>
    /// Troca, na resposta, os valores da fonte pelos slots dos atributos e a entidade pelos slots de entidade.
    /// Devolve null quando nenhum atributo da fonte aparece: a resposta não é reutilizável (não dá para montá-la
    /// de novo com outro dado).
    /// </summary>
    public static string? ExtrairTemplate(string resposta, Entidade entidade)
    {
        var t = resposta.Trim();
        var algum = false;
        // Os atributos primeiro e os valores mais longos antes ("Cidade do México" antes de "México").
        foreach (var (atributo, valor) in entidade.Atributos.Where(a => a.Value.Length > 0).OrderByDescending(a => a.Value.Length))
        {
            var re = PalavraInteira(valor);
            // Valores de uma ou duas letras ("O", "Fe") não valem no começo da frase, onde seriam artigo ("O símbolo…").
            var curto = valor.Length <= 2;
            // Um valor mais curto que o nome não é trocado dentro do nome ("hotel" em "ashley hotel"); o inverso vale
            // ("Cidade do México", a capital, contém "México", a entidade).
            var nomes = valor.Length < entidade.Nome.Length
                ? new Regex($@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(entidade.Nome)}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase).Matches(t).Select(n => (n.Index, Fim: n.Index + n.Length)).ToList()
                : [];
            t = re.Replace(t, m =>
            {
                if (curto && EhComecoDeFrase(t, m.Index)) return m.Value;
                if (nomes.Any(n => m.Index >= n.Index && m.Index + m.Length <= n.Fim)) return m.Value;
                algum = true;
                return $"{{{atributo}}}";
            });
        }
        if (!algum) return null;
        foreach (var (texto, slot) in entidade.Formas().OrderByDescending(f => f.Texto.Length))
            t = PalavraInteira(texto).Replace(t, $"{{{slot}}}");
        // O nome pode vir com outra caixa na resposta ("La Mimosa" para "la mimosa" na fonte): sem essa segunda passada, o
        // nome da primeira entidade ficaria congelado no template e apareceria nas respostas de outras.
        foreach (var (texto, slot) in entidade.Formas().Where(f => char.IsLower(f.Slot[0])).OrderByDescending(f => f.Texto.Length))
            t = new Regex($@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(texto)}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase).Replace(t, $"{{{slot}}}");
        return t;
    }

    private static bool EhComecoDeFrase(string t, int i)
    {
        var antes = t[..i].TrimEnd();
        return antes.Length == 0 || antes[^1] is '.' or '!' or '?' or ':' or '\n';
    }

    /// <summary>Os atributos que o template usa (os slots que não são da entidade).</summary>
    public static IEnumerable<string> Atributos(string template) =>
        Slot().Matches(template).Select(m => m.Groups[1].Value).Where(s => !s.EndsWith("ntidade", StringComparison.Ordinal)).Distinct();

    /// <summary>Monta a resposta com o dado atual da fonte; null quando a fonte não tem algum atributo pedido.</summary>
    public static string? Preencher(string template, Entidade entidade, IFonteDeFatos fonte)
    {
        var faltou = false;
        var texto = Slot().Replace(template, m =>
        {
            var slot = m.Groups[1].Value;
            var valor = slot.EndsWith("ntidade", StringComparison.Ordinal) ? entidade.Forma(slot) : fonte.Buscar(entidade, slot);
            if (valor is null) faltou = true;
            return valor ?? m.Value;
        });
        return faltou ? null : texto;
    }
}
