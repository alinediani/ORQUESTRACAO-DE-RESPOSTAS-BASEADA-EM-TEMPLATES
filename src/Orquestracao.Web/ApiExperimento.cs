using System.Text.Json;
using Orquestracao.Core.Conversas;
using Orquestracao.Core.Diagramas;
using Orquestracao.Motores;
using Orquestracao.Templates;
using static Orquestracao.Web.Http;

namespace Orquestracao.Web;

/// <summary>
/// O experimento rodando dentro do servidor (onde vive o motor MCP), para o LLM de geração ser o Claude Code
/// conectado ao /mcp: cada chamada ao LLM vira um turno pendente (diagrama "orquestrador"), que o cliente MCP
/// busca com <c>proxima_vez</c> e responde com <c>responder</c>.
/// <code>
/// POST /api/experimento   {llm: "mcp" | "api", perguntas?: 150, semente?: 2026, cenario?: "gsm8k"}   começa (202)
/// GET  /api/experimento   status, turnos MCP pendentes e, no fim, o resumo e onde os arquivos foram gravados
/// </code>
/// O protótipo roda primeiro (uma pergunta por vez: os templates se formam na ordem); a linha de base manda todas
/// as perguntas juntas. Os arquivos vão para docs/experimento/: resultados-{llm}.md/.json, avaliacao-humana*.csv.
/// </summary>
public static class ApiExperimento
{
    private sealed class Estado
    {
        public string Status { get; set; } = "parado";
        public string? Llm { get; set; }
        public string? Mensagem { get; set; }
        public object? Resumo { get; set; }
        public List<string> Arquivos { get; } = [];
    }

    private static readonly Estado Atual = new();
    private static readonly Lock Trava = new();

    public static string PastaDeSaida(IConfiguration cfg, string inicio)
    {
        if (cfg["Experimento:Saida"] is { Length: > 0 } c) return Path.GetFullPath(c);
        for (var d = new DirectoryInfo(inicio); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "OrquestracaoTemplates.slnx"))) return Path.Combine(d.FullName, "docs", "experimento");
        return Path.Combine(inicio, "experimento");
    }

    public static void MapApiExperimento(this WebApplication app)
    {
        var fonte = app.Services.GetRequiredService<IFonteDeFatos>();
        var motores = app.Services.GetRequiredService<RegistroDeMotores>();
        var mcp = app.Services.GetRequiredService<MotorMcp>();
        var saida = PastaDeSaida(app.Configuration, app.Environment.ContentRootPath);

        app.MapGet("/api/experimento", (HttpContext ctx) =>
        {
            lock (Trava)
                return Enviar(ctx, 200, new
                {
                    status = Atual.Status, llm = Atual.Llm, mensagem = Atual.Mensagem, resumo = Atual.Resumo, arquivos = Atual.Arquivos,
                    pendentesMcp = mcp.Pendentes("orquestrador").Count,
                });
        });

        app.MapPost("/api/experimento", async (HttpContext ctx) =>
        {
            var corpo = await LerJson(ctx);
            string Texto(string k, string padrao) => corpo.ValueKind == JsonValueKind.Object && corpo.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : padrao;
            int Numero(string k, int padrao) => corpo.ValueKind == JsonValueKind.Object && corpo.TryGetProperty(k, out var v) && v.TryGetInt32(out var n) ? n : padrao;
            var llm = Texto("llm", "mcp");
            if (llm is not ("mcp" or "api") || !motores.Tem(llm)) throw new OpError(400, "\"llm\" deve ser mcp ou api.");
            var (total, semente) = (Numero("perguntas", 150), Numero("semente", 2026));
            // Um cenário com base pública: o conjunto (e, se houver, a fonte) vem de docs/experimento/cenarios/{cenario}/.
            var cenario = Texto("cenario", "");
            // O segundo fluxo: "haiku" usa as normalizações do Claude Haiku gravadas em normalizadas-haiku.tsv (pasta do cenário).
            var comNormalizador = Texto("normalizador", "") == "haiku";
            // A trava: "haiku" usa os vereditos do Claude Haiku gravados em validacao-haiku.tsv (template<TAB>SIM|NÃO<TAB>motivo).
            var comValidador = Texto("validador", "") == "haiku";
            // O estilizador: "haiku" usa as reescritas do Claude Haiku gravadas em estilizadas-haiku.tsv (estilo<TAB>resposta do núcleo<TAB>texto).
            var comEstilizador = Texto("estilizador", "") == "haiku";
            string? pastaCenario = null;
            if (cenario.Length > 0)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(cenario, "^[a-z0-9-]+$")) throw new OpError(400, "\"cenario\" inválido.");
                pastaCenario = Path.Combine(saida, "cenarios", cenario);
                if (!File.Exists(Path.Combine(pastaCenario, "conjunto.json"))) throw new OpError(404, $"Sem conjunto.json em {pastaCenario}.");
                if (comNormalizador && !File.Exists(Path.Combine(pastaCenario, "normalizadas-haiku.tsv"))) throw new OpError(404, $"Sem normalizadas-haiku.tsv em {pastaCenario}.");
            }
            lock (Trava)
            {
                if (Atual.Status == "rodando") throw new OpError(409, "Já há um experimento rodando.");
                (Atual.Status, Atual.Llm, Atual.Mensagem, Atual.Resumo) = ("rodando", llm, null, null);
                Atual.Arquivos.Clear();
            }
            var motor = motores[llm];
            _ = Task.Run(async () =>
            {
                try
                {
                    var persona = Personas.Padrao("experimento", "Experimento") with { Motor = llm, Modelo = "claude-opus-5-5", Esforco = "low" };
                    ILlm Novo() => new LlmComEstimativa(new LlmPorMotor(motor, persona), 4, 20);
                    var fonteUsada = fonte;
                    List<PerguntaTeste> conjunto;
                    var pasta = saida;
                    if (pastaCenario is null) conjunto = Experimento.GerarConjunto(fonte, semente, total);
                    else
                    {
                        pasta = pastaCenario;
                        conjunto = JsonSerializer.Deserialize<List<PerguntaTeste>>(File.ReadAllText(Path.Combine(pastaCenario, "conjunto.json")),
                            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                        if (File.Exists(Path.Combine(pastaCenario, "fatos.json"))) fonteUsada = FonteDeFatosJson.Ler(Path.Combine(pastaCenario, "fatos.json"));
                    }
                    ILlm? normalizador = null;
                    var sufixo = "";
                    if (comNormalizador)
                    {
                        var porN = File.ReadLines(Path.Combine(pasta, "normalizadas-haiku.tsv")).Where(l => l.Contains('\t'))
                            .Select(l => l.Split('\t', 2)).ToDictionary(c => int.Parse(c[0], System.Globalization.CultureInfo.InvariantCulture), c => c[1].Trim());
                        var porPergunta = conjunto.GroupBy(q => q.Pergunta).ToDictionary(g => g.Key, g => porN[g.First().N]); // o conjunto pode repetir perguntas
                        // Preço do Claude Haiku 5.5 (US$ 0,10 / 0,50 por milhão), tokens estimados pelo tamanho do texto.
                        normalizador = new LlmComEstimativa(new LlmSimulado((_, q) => porPergunta[q], 0, 0), 0.1, 0.5);
                        sufixo = "-haiku";
                    }
                    ILlm? validador = null;
                    if (comValidador)
                    {
                        var arquivo = Path.Combine(pasta, "validacao-haiku.tsv");
                        var vereditos = File.ReadLines(arquivo).Where(l => l.Contains('\t')).Select(l => l.Split('\t'))
                            .GroupBy(c => c[0]).ToDictionary(g => g.Key, g => $"{g.First()[1]} {g.First().ElementAtOrDefault(2)}");
                        validador = new LlmComEstimativa(new LlmSimulado((_, u) =>
                        {
                            var t = u[(u.LastIndexOf("Template: ", StringComparison.Ordinal) + 10)..];
                            return vereditos.TryGetValue(t, out var v) ? v : throw new InvalidOperationException($"sem veredito gravado para o template: {t}");
                        }, 0, 0), 0.1, 0.5);
                        sufixo += "-validador";
                    }
                    ILlm? estilizador = null;
                    if (comEstilizador)
                    {
                        var reescritas = File.ReadLines(Path.Combine(pasta, "estilizadas-haiku.tsv")).Where(l => !l.StartsWith('#') && l.Contains('\t'))
                            .Select(l => l.Split('\t', 3)).GroupBy(c => (c[0].Trim(), c[1].Trim())).ToDictionary(g => g.Key, g => g.First()[2].Trim());
                        estilizador = new LlmComEstimativa(new LlmSimulado((_, u) =>
                        {
                            var linhas = u.Split('\n', 2);
                            var chave = (linhas[0]["Estilo pedido: ".Length..].Trim(), linhas[1]["Resposta: ".Length..].Trim());
                            return reescritas.TryGetValue(chave, out var t) ? t : throw new InvalidOperationException($"sem reescrita gravada para {chave}");
                        }, 0, 0), 0.1, 0.5);
                        sufixo += "-estilizador";
                    }
                    // A base do experimento (templates, perguntas e feedbacks do protótipo) fica gravada ao lado do relatório.
                    Directory.CreateDirectory(pasta);
                    var banco = Path.Combine(pasta, $"experimento-{llm}{sufixo}.db");
                    if (File.Exists(banco)) File.Delete(banco);
                    var r = await Experimento.Rodar(fonteUsada, conjunto, Novo(), Novo(), new JuizPorGabarito(), llm, semente, 0, 0,
                        concorrenciaLinhaDeBase: conjunto.Count, store: new TemplateStoreSqlite(banco), normalizador: normalizador, validador: validador, estilizador: estilizador);
                    if (pastaCenario is not null) r = r with { Cenario = cenario };
                    var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                    {
                        WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, IncludeFields = true,
                    };
                    var (planilha, chave) = AvaliacaoHumana.Gerar(r, semente);
                    var arquivos = new Dictionary<string, string>
                    {
                        [$"resultados-{llm}{sufixo}.md"] = Experimento.Relatorio(r),
                        [$"resultados-{llm}{sufixo}.json"] = JsonSerializer.Serialize(new
                        {
                            r.Modo, r.Cenario, r.Semente, r.Prototipo, r.LinhaDeBase, r.Comparacao, r.ChavesPorFamilia, r.TemplatesSalvos,
                            quadro2 = Experimento.Quadro2(r).Select(t => new { teste = t.Teste, obtido = t.Obtido, situacao = t.Ok ? "Aprovado" : "Reprovado" }),
                            respostas = r.Conjunto.Select(q => new
                            {
                                q.N, q.Pergunta, q.Familia, q.Gabarito,
                                prototipo = r.RespostasPrototipo[q.N - 1], linhaDeBase = r.RespostasLinhaDeBase[q.N - 1],
                            }),
                        }, opcoes) + "\n",
                        [$"avaliacao-humana{sufixo}.csv"] = planilha,
                        [$"avaliacao-humana{sufixo}-chave.csv"] = chave,
                    };
                    if (pastaCenario is not null)
                    {
                        // Num cenário, o Quadro 2 (do experimento factual) não se aplica.
                        var json = arquivos[$"resultados-{llm}{sufixo}.json"];
                        arquivos[$"resultados-{llm}{sufixo}.json"] = JsonSerializer.Serialize(JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!
                            .Where(k => k.Key != "quadro2").ToDictionary(), opcoes) + "\n";
                    }
                    foreach (var (nome, conteudo) in arquivos) File.WriteAllText(Path.Combine(pasta, nome), conteudo);
                    lock (Trava)
                    {
                        Atual.Status = "concluido";
                        Atual.Resumo = new { prototipo = r.Prototipo, linhaDeBase = r.LinhaDeBase, comparacao = r.Comparacao, r.ChavesPorFamilia, r.TemplatesSalvos };
                        Atual.Arquivos.AddRange(arquivos.Keys.Select(k => Path.Combine(pasta, k)));
                        Atual.Arquivos.Add(banco);
                    }
                }
                catch (Exception e)
                {
                    lock (Trava) (Atual.Status, Atual.Mensagem) = ("erro", e.Message);
                }
            });
            await Enviar(ctx, 202, new { status = "rodando", llm, cenario = cenario.Length > 0 ? cenario : null, perguntas = cenario.Length > 0 ? (int?)null : total, semente, saida = pastaCenario ?? saida });
        });
    }
}
