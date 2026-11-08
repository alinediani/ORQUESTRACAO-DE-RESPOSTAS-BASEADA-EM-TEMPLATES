using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Orquestracao.Templates;

/// <summary>Uma pergunta do conjunto de teste, com a família (a estrutura) e o gabarito.</summary>
public sealed record PerguntaTeste(int N, string Pergunta, string Familia, string Entidade, string Atributo, string Gabarito);

/// <summary>A resposta de uma estratégia (protótipo ou linha de base) para uma pergunta.</summary>
public sealed record RespostaTeste(int N, string Texto, bool Correta, string Caminho, int ChamadasGeracao, int ChamadasParser, long Tokens, double Custo, double LatenciaMs);

/// <summary>As métricas de uma estratégia.</summary>
public sealed record Metricas
{
    public required string Estrategia { get; init; }
    public int Perguntas { get; init; }
    public int ChamadasGeracao { get; init; }
    public int ChamadasParser { get; init; }
    public int AcertosTemplate { get; init; }
    public double TaxaAcertoTemplate => Perguntas == 0 ? 0 : (double)AcertosTemplate / Perguntas;
    public long Tokens { get; init; }
    public double Custo { get; init; }
    /// <summary>Latência média estimada: o tempo medido do código mais a latência do LLM em cada chamada.</summary>
    public double LatenciaMediaMs { get; init; }
    public int Corretas { get; init; }
    public double TaxaCorretas => Perguntas == 0 ? 0 : (double)Corretas / Perguntas;
}

public sealed record ComparacaoCega(int VitoriasPrototipo, int VitoriasLinhaDeBase, int Empates);

public sealed record ResultadoExperimento
{
    public required string Modo { get; init; }
    public required int Semente { get; init; }
    public required double TaxaErroLlm { get; init; }
    public required double LatenciaLlmMs { get; init; }
    public required Metricas Prototipo { get; init; }
    public required Metricas LinhaDeBase { get; init; }
    public required ComparacaoCega Comparacao { get; init; }
    /// <summary>T01: quantas chaves distintas cada família gerou (o esperado é 1).</summary>
    public required Dictionary<string, int> ChavesPorFamilia { get; init; }
    public required Dictionary<string, (int Perguntas, int Template)> PorFamilia { get; init; }
    public required int TemplatesSalvos { get; init; }
    public required List<PerguntaTeste> Conjunto { get; init; }
    public required List<RespostaTeste> RespostasPrototipo { get; init; }
    public required List<RespostaTeste> RespostasLinhaDeBase { get; init; }
}

/// <summary>Quem julga, às cegas, qual de duas respostas é melhor (T06).</summary>
public interface IJuiz
{
    /// <summary>"A", "B" ou "empate".</summary>
    Task<string> Comparar(PerguntaTeste p, string respostaA, string respostaB, CancellationToken ct = default);
}

/// <summary>O juiz do modo simulado: ganha a resposta que traz o gabarito; as duas certas (ou erradas) empatam.</summary>
public sealed class JuizPorGabarito : IJuiz
{
    public Task<string> Comparar(PerguntaTeste p, string a, string b, CancellationToken ct = default)
    {
        var (ca, cb) = (Experimento.Correta(a, p.Gabarito), Experimento.Correta(b, p.Gabarito));
        return Task.FromResult(ca == cb ? "empate" : ca ? "A" : "B");
    }
}

/// <summary>O juiz com um LLM de verdade: recebe as duas respostas sem saber de onde vieram.</summary>
public sealed class JuizLlm(ILlm llm) : IJuiz
{
    public const string System =
        "Você avalia respostas a perguntas factuais. Compare a resposta A e a resposta B quanto a correção, clareza e completude. " +
        "Responda só com uma palavra: A, B ou EMPATE.";

    public async Task<string> Comparar(PerguntaTeste p, string a, string b, CancellationToken ct = default)
    {
        var r = await llm.Completar(System, $"Pergunta: {p.Pergunta}\n\nResposta A: {a}\n\nResposta B: {b}", ct);
        var t = r.Texto.Trim().ToUpperInvariant();
        return t.StartsWith("EMPATE", StringComparison.Ordinal) ? "empate" : t.StartsWith('A') ? "A" : t.StartsWith('B') ? "B" : "empate";
    }
}

/// <summary>
/// O LLM simulado do experimento: responde às famílias do conjunto pela base de fatos (como o system de geração
/// pede: uma frase factual) e erra uma fração das vezes, trocando o dado pelo de outra entidade — a "alucinação".
/// Os tokens são estimados pelo tamanho do texto (≈ 4 caracteres por token).
/// </summary>
public sealed class LlmDeFatos(IFonteDeFatos fonte, double taxaErro = 0, int semente = 7) : ILlm
{
    private readonly ParserHeuristico _parser = new(fonte);
    private readonly Random _rnd = new(semente);
    private readonly Lock _trava = new();
    private int _chamadas;

    /// <summary>Preço do LLM de geração (Claude Sonnet 5.5): dólares por milhão de tokens de entrada e de saída.</summary>
    public (double Entrada, double Saida) Preco { get; init; } = (2, 10);

    public int Chamadas => _chamadas;

    public static long Tokens(string s) => (s.Length + 3) / 4;

    public Task<RespostaLlm> Completar(string system, string usuario, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _chamadas);
        var texto = Responder(usuario);
        var (entrada, saida) = (Tokens(system) + Tokens(usuario), Tokens(texto));
        return Task.FromResult(new RespostaLlm(texto, entrada, saida, (entrada * Preco.Entrada + saida * Preco.Saida) / 1_000_000));
    }

    private string Responder(string pergunta)
    {
        var e = _parser.ExtrairSemLlm(pergunta).Entidade;
        var familia = Experimento.FamiliaDe(pergunta);
        if (e is null || familia is null) return "Não sei responder a isso com segurança.";
        var atributo = Experimento.Familias.First(f => f.Nome == familia).Atributo;
        var valor = e.Atributos.GetValueOrDefault(atributo) ?? "?";
        bool errar;
        lock (_trava) errar = _rnd.NextDouble() < taxaErro;
        if (errar)
        {
            // Um dado plausível, mas de outra entidade do mesmo tipo.
            var outras = fonte.Entidades.Where(x => x.Tipo == e.Tipo && x.Atributos.GetValueOrDefault(atributo) is { } v && v != valor).ToList();
            lock (_trava) valor = outras[_rnd.Next(outras.Count)].Atributos[atributo];
        }
        return Experimento.Frase(familia, e, valor);
    }
}

/// <summary>
/// O experimento do TCC (T05/T06): o mesmo conjunto de perguntas respondido pelo protótipo (orquestrador por
/// template) e pela linha de base (o LLM responde todas), comparando chamadas ao LLM, tokens, custo, latência
/// e qualidade (taxa de respostas corretas pelo gabarito e comparação cega).
/// </summary>
public static class Experimento
{
    public sealed record Familia(string Nome, string Atributo, string Tipo, int Peso, string[] Frases);

    /// <summary>
    /// As famílias de mesma estrutura, com o peso de cada uma no conjunto (as perguntas em alta aparecem mais).
    /// Nas frases, {de} é "da França", {em} é "na França" e {o} é "a França".
    /// </summary>
    public static readonly Familia[] Familias =
    [
        new("capital", "capital", "pais", 34, ["Qual é a capital {de}?", "Qual a capital {de}?", "Me diga a capital {de}."]),
        new("moeda", "moeda", "pais", 17, ["Qual é a moeda {de}?", "Qual a moeda {de}?"]),
        new("idioma", "idioma", "pais", 10, ["Que língua se fala {em}?"]),
        new("continente", "continente", "pais", 10, ["Em que continente fica {o}?"]),
        new("simbolo", "simbolo", "elemento", 16, ["Qual o símbolo químico {de}?", "Qual é o símbolo químico {de}?"]),
        new("numero", "numero_atomico", "elemento", 13, ["Qual é o número atômico {de}?", "Qual o número atômico {de}?"]),
    ];

    public static string? FamiliaDe(string pergunta)
    {
        var n = Texto.Normalizar(pergunta);
        if (n.Contains("capital")) return "capital";
        if (n.Contains("moeda")) return "moeda";
        if (n.Contains("lingua") || n.Contains("idioma")) return "idioma";
        if (n.Contains("continente")) return "continente";
        if (n.Contains("simbolo")) return "simbolo";
        if (n.Contains("numero atomico")) return "numero";
        return null;
    }

    /// <summary>A frase da resposta (a do LLM simulado e a esperada no gabarito).</summary>
    public static string Frase(string familia, Entidade e, string valor) => familia switch
    {
        "capital" => $"A capital {e.Forma("de_entidade")} é {valor}.",
        "moeda" => $"A moeda {e.Forma("de_entidade")} é o {valor}.",
        "idioma" => $"{Entidade.Maiuscula(e.Forma("em_entidade")!)} fala-se {valor}.",
        "continente" => $"{Entidade.Maiuscula(e.Forma("o_entidade") ?? e.Nome)} fica na {valor}.",
        "simbolo" => $"O símbolo químico {e.Forma("de_entidade")} é {valor}.",
        _ => $"O número atômico {e.Forma("de_entidade")} é {valor}.",
    };

    private static string Pergunta(string frase, Entidade e) =>
        frase.Replace("{de}", e.Forma("de_entidade")).Replace("{em}", e.Forma("em_entidade")).Replace("{o}", e.Forma("o_entidade") ?? e.Nome);

    /// <summary>O conjunto de teste: <paramref name="total"/> perguntas sorteadas (com repetição) pela semente, nas proporções das famílias.</summary>
    public static List<PerguntaTeste> GerarConjunto(IFonteDeFatos fonte, int semente = 2026, int total = 150)
    {
        var rnd = new Random(semente);
        var somaPesos = Familias.Sum(f => f.Peso);
        var lista = new List<(Familia F, Entidade E, string Frase)>();
        foreach (var f in Familias)
        {
            var quantas = (int)Math.Round(total * (double)f.Peso / somaPesos);
            var entidades = fonte.Entidades.Where(e => e.Tipo == f.Tipo && e.Atributos.ContainsKey(f.Atributo)).ToList();
            for (var i = 0; i < quantas; i++)
                lista.Add((f, entidades[rnd.Next(entidades.Count)], f.Frases[rnd.Next(f.Frases.Length)]));
        }
        while (lista.Count > total) lista.RemoveAt(lista.Count - 1);
        var embaralhada = lista.OrderBy(_ => rnd.Next()).ToList();
        return embaralhada.Select((x, i) => new PerguntaTeste(i + 1, Pergunta(x.Frase, x.E), x.F.Nome, x.E.Nome, x.F.Atributo, x.E.Atributos[x.F.Atributo])).ToList();
    }

    /// <summary>A resposta traz o gabarito (sem diferenciar maiúsculas e acentos)?</summary>
    public static bool Correta(string resposta, string gabarito)
    {
        var r = Texto.Normalizar(resposta);
        var g = Texto.Normalizar(gabarito);
        return System.Text.RegularExpressions.Regex.IsMatch(r, $@"(^|\s){System.Text.RegularExpressions.Regex.Escape(g)}($|\s)");
    }

    /// <summary>
    /// Roda o experimento. <paramref name="llmPrototipo"/> e <paramref name="llmLinhaDeBase"/> são instâncias separadas
    /// (cada uma conta as próprias chamadas). O feedback do usuário é simulado: bom quando a resposta está certa.
    /// </summary>
    public static async Task<ResultadoExperimento> Rodar(
        IFonteDeFatos fonte, List<PerguntaTeste> conjunto, ILlm llmPrototipo, ILlm llmLinhaDeBase, IJuiz juiz,
        string modo, int semente, double taxaErroLlm, double latenciaLlmMs, ConfiguracaoOrquestrador? cfg = null, IParser? parser = null, CancellationToken ct = default)
    {
        var orq = new Orquestrador(parser ?? new ParserHeuristico(fonte), new TemplateStoreMemoria(), fonte, cfg);
        var proto = new List<RespostaTeste>();
        var chaves = new Dictionary<string, HashSet<string>>();
        foreach (var p in conjunto)
        {
            var r = await orq.Responder(p.Pergunta, llmPrototipo, ct);
            var correta = Correta(r.Texto, p.Gabarito);
            if (!chaves.TryGetValue(p.Familia, out var set)) chaves[p.Familia] = set = [];
            set.Add(r.Chave);
            // Feedback simulado do usuário (como o TCC prevê nos testes automatizados).
            if (r.Template is not null || r.Caminho == Caminho.Template) orq.Feedback(r.Chave, correta);
            proto.Add(new RespostaTeste(p.N, r.Texto, correta, r.Caminho, r.ChamadasGeracao, r.ChamadasParser, r.TokensEntrada + r.TokensSaida, r.Custo,
                r.LatenciaMs + (r.ChamadasGeracao + r.ChamadasParser) * latenciaLlmMs));
        }

        var baseLista = new List<RespostaTeste>();
        foreach (var p in conjunto)
        {
            var relogio = Stopwatch.StartNew();
            var r = await llmLinhaDeBase.Completar(Orquestrador.SystemGeracao, p.Pergunta, ct);
            baseLista.Add(new RespostaTeste(p.N, r.Texto.Trim(), Correta(r.Texto, p.Gabarito), Caminho.Llm, 1, 0, r.Entrada + r.Saida, r.Custo,
                relogio.Elapsed.TotalMilliseconds + latenciaLlmMs));
        }

        // Comparação cega: a ordem (A/B) é sorteada a cada pergunta, e o juiz não sabe de onde veio cada resposta.
        var rnd = new Random(semente + 1);
        int vp = 0, vb = 0, emp = 0;
        foreach (var p in conjunto)
        {
            var (rp, rb) = (proto[p.N - 1].Texto, baseLista[p.N - 1].Texto);
            var protoEhA = rnd.Next(2) == 0;
            var veredito = await juiz.Comparar(p, protoEhA ? rp : rb, protoEhA ? rb : rp, ct);
            if (veredito == "empate") emp++;
            else if ((veredito == "A") == protoEhA) vp++;
            else vb++;
        }

        static Metricas Medir(string nome, List<RespostaTeste> rs) => new()
        {
            Estrategia = nome, Perguntas = rs.Count, ChamadasGeracao = rs.Sum(r => r.ChamadasGeracao), ChamadasParser = rs.Sum(r => r.ChamadasParser),
            AcertosTemplate = rs.Count(r => r.Caminho == Caminho.Template), Tokens = rs.Sum(r => r.Tokens), Custo = rs.Sum(r => r.Custo),
            LatenciaMediaMs = rs.Count == 0 ? 0 : rs.Average(r => r.LatenciaMs), Corretas = rs.Count(r => r.Correta),
        };

        return new ResultadoExperimento
        {
            Modo = modo, Semente = semente, TaxaErroLlm = taxaErroLlm, LatenciaLlmMs = latenciaLlmMs,
            Prototipo = Medir("Protótipo (templates)", proto), LinhaDeBase = Medir("Linha de base (LLM puro)", baseLista),
            Comparacao = new ComparacaoCega(vp, vb, emp),
            ChavesPorFamilia = chaves.ToDictionary(k => k.Key, k => k.Value.Count),
            PorFamilia = conjunto.GroupBy(p => p.Familia).ToDictionary(g => g.Key, g => (g.Count(), g.Count(p => proto[p.N - 1].Caminho == Caminho.Template))),
            TemplatesSalvos = orq.Estatisticas.TemplatesSalvos,
            Conjunto = conjunto, RespostasPrototipo = proto, RespostasLinhaDeBase = baseLista,
        };
    }

    // ---- relatório -------------------------------------------------------------------

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static string Pct(double x) => (x * 100).ToString("0.0", PtBr) + "%";

    private static string Usd(double x) => "US$ " + x.ToString(x < 0.01 ? "0.0000" : "0.00", PtBr);

    private static string Ms(double x) => x.ToString("0", PtBr) + " ms";

    private static string Reducao(double proto, double baseValor) => baseValor == 0 ? "—" : Pct(1 - proto / baseValor);

    /// <summary>O relatório em Markdown (vai para docs/experimento/resultados.md).</summary>
    public static string Relatorio(ResultadoExperimento r)
    {
        var (p, b, c) = (r.Prototipo, r.LinhaDeBase, r.Comparacao);
        var sb = new StringBuilder();
        sb.AppendLine("# Resultados do experimento");
        sb.AppendLine();
        sb.AppendLine($"Gerado por `dotnet run --project src/Orquestracao.Experimento -- --llm {r.Modo}` · semente {r.Semente} · {p.Perguntas} perguntas em {r.PorFamilia.Count} famílias de mesma estrutura.");
        sb.AppendLine();
        if (r.Modo == "simulado")
        {
            sb.AppendLine($"> **LLM simulado.** O LLM de geração responde pela base de fatos e erra {Pct(r.TaxaErroLlm)} das vezes (troca o dado pelo de outra entidade, simulando alucinação). " +
                          $"Os tokens são estimados pelo tamanho do texto (≈ 4 caracteres por token), o custo usa o preço do Claude Sonnet 5.5 (US$ 2 / US$ 10 por milhão de tokens) e a latência soma {Ms(r.LatenciaLlmMs)} por chamada ao LLM ao tempo medido do código. " +
                          "A comparação cega usa um juiz pelo gabarito. Para os números com o modelo real, rode com `--llm api` (precisa de credencial da Anthropic).");
            sb.AppendLine();
        }
        sb.AppendLine("## Protótipo × linha de base");
        sb.AppendLine();
        sb.AppendLine("| Métrica | Protótipo (templates) | Linha de base (LLM puro) | Redução |");
        sb.AppendLine("|---|---:|---:|---:|");
        sb.AppendLine($"| Chamadas ao LLM de geração | {p.ChamadasGeracao} | {b.ChamadasGeracao} | {Reducao(p.ChamadasGeracao, b.ChamadasGeracao)} |");
        sb.AppendLine($"| Chamadas ao LLM do parser | {p.ChamadasParser} | — | |");
        sb.AppendLine($"| Taxa de acerto de template | {Pct(p.TaxaAcertoTemplate)} ({p.AcertosTemplate}) | — | |");
        sb.AppendLine($"| Tokens | {p.Tokens.ToString("N0", PtBr)} | {b.Tokens.ToString("N0", PtBr)} | {Reducao(p.Tokens, b.Tokens)} |");
        sb.AppendLine($"| Custo estimado | {Usd(p.Custo)} | {Usd(b.Custo)} | {Reducao(p.Custo, b.Custo)} |");
        sb.AppendLine($"| Latência média estimada | {Ms(p.LatenciaMediaMs)} | {Ms(b.LatenciaMediaMs)} | {Reducao(p.LatenciaMediaMs, b.LatenciaMediaMs)} |");
        sb.AppendLine($"| Respostas corretas (gabarito) | {Pct(p.TaxaCorretas)} ({p.Corretas}) | {Pct(b.TaxaCorretas)} ({b.Corretas}) | |");
        sb.AppendLine();
        sb.AppendLine($"**Comparação cega (T06):** protótipo melhor em {c.VitoriasPrototipo}, linha de base melhor em {c.VitoriasLinhaDeBase}, empate em {c.Empates} de {p.Perguntas}.");
        sb.AppendLine();
        sb.AppendLine("## Por família");
        sb.AppendLine();
        sb.AppendLine("| Família | Perguntas | Chaves distintas (T01) | Respondidas pelo template |");
        sb.AppendLine("|---|---:|---:|---:|");
        foreach (var f in Familias)
            if (r.PorFamilia.TryGetValue(f.Nome, out var x))
                sb.AppendLine($"| {f.Nome} | {x.Perguntas} | {r.ChavesPorFamilia.GetValueOrDefault(f.Nome)} | {x.Template} ({Pct((double)x.Template / x.Perguntas)}) |");
        sb.AppendLine();
        sb.AppendLine($"Templates salvos na base chave-valor: {r.TemplatesSalvos}.");
        sb.AppendLine();
        sb.AppendLine("## Testes do Quadro 2");
        sb.AppendLine();
        sb.AppendLine("| Teste | Resultado obtido | Situação |");
        sb.AppendLine("|---|---|---|");
        foreach (var (t, obtido, ok) in Quadro2(r)) sb.AppendLine($"| {t} | {obtido} | {(ok ? "Aprovado" : "Reprovado")} |");
        sb.AppendLine();
        sb.AppendLine("## As primeiras perguntas");
        sb.AppendLine();
        sb.AppendLine("| # | Pergunta | Protótipo | Caminho | Linha de base |");
        sb.AppendLine("|---:|---|---|---|---|");
        foreach (var q in r.Conjunto.Take(20))
        {
            var (rp, rb) = (r.RespostasPrototipo[q.N - 1], r.RespostasLinhaDeBase[q.N - 1]);
            sb.AppendLine($"| {q.N} | {q.Pergunta} | {rp.Texto}{(rp.Correta ? "" : " ❌")} | {rp.Caminho} | {rb.Texto}{(rb.Correta ? "" : " ❌")} |");
        }
        return sb.ToString();
    }

    /// <summary>Os resultados de T01–T06 (o Quadro 2 do TCC) a partir de um experimento rodado.</summary>
    public static List<(string Teste, string Obtido, bool Ok)> Quadro2(ResultadoExperimento r)
    {
        var (p, b, c) = (r.Prototipo, r.LinhaDeBase, r.Comparacao);
        var umaChave = r.ChavesPorFamilia.Values.All(n => n == 1);
        var primeirasViaLlm = r.PorFamilia.Count;
        return
        [
            ("T01", $"{r.PorFamilia.Count} famílias, {(umaChave ? "uma chave por família" : "mais de uma chave em alguma família")} ({string.Join(", ", r.ChavesPorFamilia.Select(k => $"{k.Key}: {k.Value}"))})", umaChave),
            ("T02", $"A primeira pergunta de cada família foi ao LLM e gerou template com peso 0,5 ({r.TemplatesSalvos} templates salvos para {primeirasViaLlm} famílias)", r.TemplatesSalvos >= primeirasViaLlm),
            ("T03", $"{p.AcertosTemplate} de {p.Perguntas} perguntas ({Pct(p.TaxaAcertoTemplate)}) montadas pelo template, sem o LLM de geração", p.AcertosTemplate > 0),
            ("T04", "Coberto pelos testes automatizados (OrquestradorTests.T04): o peso cai 0,50 → 0,35 → 0,245 e o template é descartado no 3º feedback ruim (0,17 < 0,20); a pergunta seguinte volta ao LLM", true),
            ("T05", $"{p.ChamadasGeracao} chamadas ao LLM no protótipo contra {b.ChamadasGeracao} na linha de base (redução de {Reducao(p.ChamadasGeracao, b.ChamadasGeracao)})", p.ChamadasGeracao < b.ChamadasGeracao),
            ("T06", $"Comparação cega: protótipo melhor em {c.VitoriasPrototipo}, linha de base em {c.VitoriasLinhaDeBase}, empate em {c.Empates}; corretas: {Pct(p.TaxaCorretas)} × {Pct(b.TaxaCorretas)}", c.VitoriasPrototipo >= c.VitoriasLinhaDeBase && p.TaxaCorretas >= b.TaxaCorretas),
        ];
    }
}
