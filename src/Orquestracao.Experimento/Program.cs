// O experimento do TCC: o mesmo conjunto de perguntas pelo protótipo (orquestrador por template) e pela linha
// de base (o LLM responde todas). Grava o relatório em docs/experimento/.
//
//   dotnet run --project src/Orquestracao.Experimento                      # LLM simulado (sem custo, reprodutível)
//   dotnet run --project src/Orquestracao.Experimento -- --erro 0.1        # o LLM simulado erra 10% das vezes
//   dotnet run --project src/Orquestracao.Experimento -- --llm api         # Claude Sonnet 5.5 na geração e Claude Opus 5.5 como juiz
//   dotnet run --project src/Orquestracao.Experimento -- --llm api --parser hibrido   # e o Claude Haiku 5.5 no parser
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
else
{
    Console.Error.WriteLine($"--llm deve ser simulado ou api (veio {modo}).");
    return 1;
}

Console.WriteLine($"Experimento: {conjunto.Count} perguntas, LLM {modo}{(modo == "simulado" ? $" (erro {erro:P0})" : "")}, semente {semente}…");
var r = await Experimento.Rodar(fonte, conjunto, llmProto, llmBase, juiz, modo, semente, erro, latencia, parser: parser);

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
