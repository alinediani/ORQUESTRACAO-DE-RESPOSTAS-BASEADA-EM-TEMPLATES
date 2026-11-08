using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Orquestracao.Templates;

/// <summary>Uma entidade da base de fatos: um país, um elemento químico… com os atributos que preenchem os slots.</summary>
public sealed record Entidade
{
    public required string Nome { get; init; }
    /// <summary>pais · elemento · …: entra na chave (perguntas sobre países e sobre elementos têm estruturas diferentes).</summary>
    public required string Tipo { get; init; }
    /// <summary>a · o · os · as · "" (sem artigo): dá "da França", "do Japão", "de Portugal", "nos Estados Unidos".</summary>
    public string Artigo { get; init; } = "";
    public List<string> Apelidos { get; init; } = [];
    public Dictionary<string, string> Atributos { get; init; } = [];

    /// <summary>
    /// As formas da entidade num texto e o slot de cada uma, da mais longa para a mais curta:
    /// "da França" → {de_entidade}, "na França" → {em_entidade}, "a França" → {o_entidade}, "França" → {entidade}.
    /// Começando com maiúscula ("Na França"), o slot também (Em_entidade).
    /// </summary>
    public IEnumerable<(string Texto, string Slot)> Formas()
    {
        var (de, em) = Artigo switch
        {
            "a" => ("da", "na"),
            "o" => ("do", "no"),
            "as" => ("das", "nas"),
            "os" => ("dos", "nos"),
            _ => ("de", "em"),
        };
        var formas = new List<(string, string)>
        {
            ($"{de} {Nome}", "de_entidade"),
            ($"{em} {Nome}", "em_entidade"),
        };
        if (Artigo.Length > 0) formas.Add(($"{Artigo} {Nome}", "o_entidade"));
        foreach (var (texto, slot) in formas.ToList()) formas.Add((Maiuscula(texto), Maiuscula(slot)));
        formas.Add((Nome, "entidade"));
        if (char.IsLower(Nome[0])) formas.Add((Maiuscula(Nome), "Entidade"));
        return formas;
    }

    /// <summary>O texto de um slot de entidade (o inverso de <see cref="Formas"/>).</summary>
    public string? Forma(string slot)
    {
        foreach (var (texto, s) in Formas())
            if (s == slot) return texto;
        return null;
    }

    public static string Maiuscula(string s) => s.Length == 0 ? s : char.ToUpper(s[0], CultureInfo.GetCultureInfo("pt-BR")) + s[1..];
}

/// <summary>A fonte externa do TCC: de onde vem o dado de cada resposta (o template só define a forma).</summary>
public interface IFonteDeFatos
{
    IReadOnlyList<Entidade> Entidades { get; }

    /// <summary>O valor de um atributo da entidade, ou null se a fonte não tem.</summary>
    string? Buscar(Entidade entidade, string atributo) => entidade.Atributos.GetValueOrDefault(atributo);

    Entidade? PorNome(string nome) =>
        Entidades.FirstOrDefault(e => Texto.Normalizar(e.Nome) == Texto.Normalizar(nome) || e.Apelidos.Any(a => Texto.Normalizar(a) == Texto.Normalizar(nome)));
}

/// <summary>A base de fatos local e controlada (dados/fatos.json), o que garante testes reprodutíveis.</summary>
public sealed class FonteDeFatosJson : IFonteDeFatos
{
    public IReadOnlyList<Entidade> Entidades { get; }

    public FonteDeFatosJson(IEnumerable<Entidade> entidades) => Entidades = entidades.ToList();

    private sealed record Arquivo(List<Entidade> Entidades);

    public static FonteDeFatosJson Ler(string caminho)
    {
        var a = JsonSerializer.Deserialize<Arquivo>(File.ReadAllText(caminho), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException($"base de fatos vazia: {caminho}");
        return new FonteDeFatosJson(a.Entidades);
    }

    /// <summary>A pasta dados/ do repositório (a configurada, ou subindo a partir de <paramref name="inicio"/>).</summary>
    public static string Achar(string? configurado, string inicio)
    {
        if (!string.IsNullOrEmpty(configurado)) return Path.GetFullPath(configurado);
        for (var d = new DirectoryInfo(inicio); d is not null; d = d.Parent)
        {
            var f = Path.Combine(d.FullName, "dados", "fatos.json");
            if (File.Exists(f)) return f;
        }
        return Path.Combine(inicio, "dados", "fatos.json");
    }
}

/// <summary>Normalização de texto para comparar e montar chaves: minúsculas, sem acento, sem pontuação.</summary>
public static partial class Texto
{
    public static string SemAcento(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex(@"[^\p{L}\p{N}{}_]+")]
    private static partial Regex NaoPalavra();

    /// <summary>"Qual é a capital da França?" → "qual e a capital da franca".</summary>
    public static string Normalizar(string s) => NaoPalavra().Replace(SemAcento(s).ToLowerInvariant(), " ").Trim();
}
