// O experimento do TCC: o mesmo conjunto de perguntas pelo protótipo (orquestrador por template) e pela linha
// de base (o LLM responde todas). Grava o relatório em docs/experimento/.
//
//   dotnet run --project src/Orquestracao.Experimento                      # LLM simulado (sem custo, reprodutível)
//   dotnet run --project src/Orquestracao.Experimento -- --erro 0.1        # o LLM simulado erra 10% das vezes
//   dotnet run --project src/Orquestracao.Experimento -- --llm api         # Claude Sonnet 5.5 na geração e Claude Opus 5.5 como juiz
//   dotnet run --project src/Orquestracao.Experimento -- --llm api --parser hibrido   # e o Claude Haiku 5.5 no parser
//   dotnet run --project src/Orquestracao.Experimento -- --llm gravado --respostas docs/experimento/resultados-mcp.json --banco docs/experimento/experimento-mcp.db
//       reproduz um experimento a partir das respostas gravadas do LLM (ex.: o do Claude via MCP) e grava a base SQLite
//   dotnet run --project src/Orquestracao.Experimento -- --cenario gsm8k --analisar --preparar --perguntas 30
//       análise estrutural da base inteira (sem LLM) e a amostra que vai ao experimento (cenarios/gsm8k/conjunto.json)
using System.Globalization;
using System.Text.Json;
using Orquestracao.Core.Conversas;
using Orquestracao.Motores;
using Orquestracao.Templates;

var opcoes = Opcoes(args);
var modo = opcoes.GetValueOrDefault("llm", "simulado");
var semente = int.Parse(opcoes.GetValueOrDefault("semente", "2026"), CultureInfo.InvariantCulture);
var erro = double.Parse(opcoes.GetValueOrDefault("erro", "0.05"), CultureInfo.InvariantCulture);
var latencia = double.Parse(opcoes.GetValueOrDefault("latencia", modo == "simulado" ? "1200" : "0"), CultureInfo.InvariantCulture);
var total = int.Parse(opcoes.GetValueOrDefault("perguntas", "150"), CultureInfo.InvariantCulture);
var saida = opcoes.GetValueOrDefault("saida", Path.Combine(Raiz(), "docs", "experimento"));

var fonte = FonteDeFatosJson.Ler(FonteDeFatosJson.Achar(opcoes.GetValueOrDefault("fatos"), AppContext.BaseDirectory));

if (opcoes.GetValueOrDefault("cenario") is { } cenario)
{
    // Os cenários com bases públicas: docs/experimento/cenarios/{cenario}/. A fonte de fatos do cenário (fatos.json
    // na pasta) substitui a padrão quando existe.
    var pasta = Path.Combine(Raiz(), "docs", "experimento", "cenarios", cenario);
    var fatosDoCenario = Path.Combine(pasta, "fatos.json");
    if (cenario == "saude" && opcoes.ContainsKey("fonte"))
    {
        // A fonte do cenário de saúde sai do corpus (condicoes.txt + a especialidade mais frequente de cada condição).
        var ents = Cenarios.FonteSaude(Cenarios.LerSaude(Path.Combine(pasta, "saudebr-qa.csv")), Path.Combine(pasta, "condicoes.txt"));
        var j = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        File.WriteAllText(fatosDoCenario, JsonSerializer.Serialize(new
        {
            descricao = "Fonte do cenário de saúde, gerada do SaudeBR-QA por `--cenario saude --fonte`: condições de condicoes.txt citadas em 20+ perguntas, com o especialista da especialidade mais frequente entre elas.",
            entidades = ents,
        }, j) + "\n");
        Console.WriteLine($"saude: fonte com {ents.Count} condições em {fatosDoCenario}");
    }
    var fonteCenario = File.Exists(fatosDoCenario) ? FonteDeFatosJson.Ler(fatosDoCenario) : fonte;
    var descricao = File.Exists(fatosDoCenario) ? "do cenário (`fatos.json` na pasta)" : $"padrão do protótipo (`dados/fatos.json`, {fonte.Entidades.Count} entidades)";
    // conferiveis: só as perguntas com gabarito curto (a amostra); a análise estrutural usa a base inteira.
    List<PerguntaTeste> Ler(bool conferiveis) => cenario switch
    {
        "gsm8k" => Cenarios.LerGsm8k(Path.Combine(pasta, "gsm8k-pt-test.jsonl")),
        "mkqa" => Cenarios.LerMkqa(Path.Combine(pasta, "mkqa-pt.jsonl"), conferiveis),
        "saude" => Cenarios.LerSaude(Path.Combine(pasta, "saudebr-qa.csv")),
        "squad" => Cenarios.LerSquad(Path.Combine(pasta, "squad-pt-validation.jsonl"), comTexto: conferiveis),
        _ => throw new ArgumentException($"cenário desconhecido: {cenario}"),
    };
    var jsonOpcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    if (opcoes.ContainsKey("analisar"))
    {
        var a = Cenarios.Analisar(cenario, Ler(false).Select(p => p.Pergunta).ToList(), new ParserHeuristico(fonteCenario));
        File.WriteAllText(Path.Combine(pasta, "analise.md"), Cenarios.RelatorioAnalise(a, descricao));
        Console.WriteLine($"{cenario}: {a.Perguntas} perguntas, {a.ComEntidade} com entidade, {a.ChavesDistintas} chaves, teto de reaproveitamento {a.TetoReaproveitamento} ({a.TetoReaproveitamentoPct:P1}), estruturas abstratas repetidas {a.RepeticoesAbstratas}");
    }
    if (opcoes.ContainsKey("preparar"))
    {
        // Saúde: o melhor caso (as chaves que mais se repetem), para exercitar o caminho do template.
        var amostra = cenario == "saude"
            ? Cenarios.AmostraMelhorCaso(Ler(true), new ParserHeuristico(fonteCenario), total, semente)
            : Cenarios.Amostra(Ler(true), total, semente);
        File.WriteAllText(Path.Combine(pasta, "conjunto.json"), JsonSerializer.Serialize(amostra, jsonOpcoes) + "\n");
        Console.WriteLine($"{cenario}: amostra de {amostra.Count} perguntas (semente {semente}) em {Path.Combine(pasta, "conjunto.json")}");
    }
    return 0;
}

var conjunto = Experimento.GerarConjunto(fonte, semente, total);

ILlm llmProto, llmBase;
IJuiz juiz;
IParser? parser = null;
if (modo == "simulado")
{
    llmProto = new LlmDeFatos(fonte, erro, 7);
    llmBase = new LlmDeFatos(fonte, erro, 7);
    juiz = new JuizPorGabarito();
}
else if (modo == "api")
{
    if (!MotorApi.TemCredencial())
    {
        Console.Error.WriteLine("Sem credencial da Anthropic: defina ANTHROPIC_API_KEY (ou rode `ant auth login`).");
        return 1;
    }
    var api = new MotorApi();
    Persona P(string modelo, string esforco) => Personas.Padrao("experimento", "Experimento") with { Motor = "api", Modelo = modelo, Esforco = esforco };
    // O TCC: Sonnet na geração, Haiku no parser; o juiz da comparação cega é o Opus.
    llmProto = new LlmPorMotor(api, P("claude-sonnet-5-5", "low"));
    llmBase = new LlmPorMotor(api, P("claude-sonnet-5-5", "low"));
    juiz = new JuizLlm(new LlmPorMotor(api, P("claude-opus-5-5", "medium")));
    var tipoParser = opcoes.GetValueOrDefault("parser", "heuristico");
    if (tipoParser != "heuristico")
    {
        var porLlm = new ParserLlm(new LlmPorMotor(api, P("claude-haiku-5-5", "low")), fonte);
        parser = tipoParser == "llm" ? porLlm : new ParserHibrido(new ParserHeuristico(fonte), porLlm);
    }
    erro = 0;
}
else if (modo == "gravado")
{
    // As respostas que o LLM deu, gravadas no resultados-*.json: o protótipo reaproveita as do caminho "llm", e a
    // linha de base, as dela. O fluxo é determinístico, então a reprodução tem de bater com o experimento original.
    var arquivo = opcoes.GetValueOrDefault("respostas") ?? throw new ArgumentException("--respostas <resultados-*.json>");
    using var doc = JsonDocument.Parse(File.ReadAllText(arquivo));
    var proto = new Dictionary<string, string>();
    var baseR = new Dictionary<string, string>();
    foreach (var x in doc.RootElement.GetProperty("respostas").EnumerateArray())
    {
        var pergunta = x.GetProperty("pergunta").GetString()!;
        var rp = x.GetProperty("prototipo");
        if (rp.GetProperty("caminho").GetString() == "llm") proto.TryAdd(pergunta, rp.GetProperty("texto").GetString()!);
        baseR.TryAdd(pergunta, x.GetProperty("linhaDeBase").GetProperty("texto").GetString()!);
    }
    ILlm Gravado(Dictionary<string, string> d) => new LlmComEstimativa(new LlmSimulado((_, pergunta) =>
        d.TryGetValue(pergunta, out var t) ? t : throw new InvalidOperationException($"sem resposta gravada para: {pergunta}"), 0, 0), 4, 20);
    llmProto = Gravado(proto);
    llmBase = Gravado(baseR);
    juiz = new JuizPorGabarito();
    erro = 0;
}
else
{
    Console.Error.WriteLine($"--llm deve ser simulado, api ou gravado (veio {modo}).");
    return 1;
}

Console.WriteLine($"Experimento: {conjunto.Count} perguntas, LLM {modo}{(modo == "simulado" ? $" (erro {erro:P0})" : "")}, semente {semente}…");
ITemplateStore? store = null;
if (opcoes.GetValueOrDefault("banco") is { } banco)
{
    if (File.Exists(banco)) File.Delete(banco);
    store = new TemplateStoreSqlite(banco);
}
var r = await Experimento.Rodar(fonte, conjunto, llmProto, llmBase, juiz, modo, semente, erro, latencia, parser: parser, store: store);
if (modo == "gravado")
{
    // Só a base: o relatório do experimento original já está gravado.
    Console.WriteLine($"Reprodução: protótipo {r.Prototipo.ChamadasGeracao} chamadas, {r.Prototipo.AcertosTemplate} pelo template, {r.Prototipo.Corretas} corretas · linha de base {r.LinhaDeBase.ChamadasGeracao}.");
    Console.WriteLine($"Base gravada: {opcoes["banco"]}");
    return 0;
}

Directory.CreateDirectory(saida);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, IncludeFields = true,
};
var sufixo = modo == "simulado" ? "" : $"-{modo}";
File.WriteAllText(Path.Combine(saida, $"resultados{sufixo}.md"), Experimento.Relatorio(r));
File.WriteAllText(Path.Combine(saida, $"resultados{sufixo}.json"), JsonSerializer.Serialize(new
{
    r.Modo, r.Semente, r.TaxaErroLlm, r.LatenciaLlmMs, r.Prototipo, r.LinhaDeBase, r.Comparacao, r.ChavesPorFamilia,
    porFamilia = r.PorFamilia.ToDictionary(k => k.Key, k => new { perguntas = k.Value.Perguntas, template = k.Value.Template }),
    r.TemplatesSalvos,
    quadro2 = Experimento.Quadro2(r).Select(t => new { teste = t.Teste, obtido = t.Obtido, situacao = t.Ok ? "Aprovado" : "Reprovado" }),
}, json) + "\n");
File.WriteAllText(Path.Combine(saida, "conjunto.json"), JsonSerializer.Serialize(conjunto, json) + "\n");

var (p, b) = (r.Prototipo, r.LinhaDeBase);
Console.WriteLine($"Chamadas ao LLM de geração: protótipo {p.ChamadasGeracao} × linha de base {b.ChamadasGeracao}");
Console.WriteLine($"Taxa de acerto de template: {p.TaxaAcertoTemplate:P1} · corretas: {p.TaxaCorretas:P1} × {b.TaxaCorretas:P1}");
Console.WriteLine($"Comparação cega: protótipo {r.Comparacao.VitoriasPrototipo} · linha de base {r.Comparacao.VitoriasLinhaDeBase} · empates {r.Comparacao.Empates}");
Console.WriteLine($"Relatório: {Path.Combine(saida, $"resultados{sufixo}.md")}");
return 0;

static Dictionary<string, string> Opcoes(string[] args)
{
    var d = new Dictionary<string, string>();
    for (var i = 0; i < args.Length; i++)
        if (args[i].StartsWith("--", StringComparison.Ordinal))
            d[args[i][2..]] = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : "true";
    return d;
}

static string Raiz()
{
    for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "OrquestracaoTemplates.slnx"))) return d.FullName;
    return Directory.GetCurrentDirectory();
}
