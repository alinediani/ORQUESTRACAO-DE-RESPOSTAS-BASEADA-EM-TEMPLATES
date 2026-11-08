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
/// POST /api/experimento   {llm: "mcp" | "api", perguntas?: 150, semente?: 2026}   começa (202)
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
                    var conjunto = Experimento.GerarConjunto(fonte, semente, total);
                    var r = await Experimento.Rodar(fonte, conjunto, Novo(), Novo(), new JuizPorGabarito(), llm, semente, 0, 0, concorrenciaLinhaDeBase: conjunto.Count);
                    Directory.CreateDirectory(saida);
                    var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                    {
                        WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, IncludeFields = true,
                    };
                    var (planilha, chave) = AvaliacaoHumana.Gerar(r, semente);
                    var arquivos = new Dictionary<string, string>
                    {
                        [$"resultados-{llm}.md"] = Experimento.Relatorio(r),
                        [$"resultados-{llm}.json"] = JsonSerializer.Serialize(new
                        {
                            r.Modo, r.Semente, r.Prototipo, r.LinhaDeBase, r.Comparacao, r.ChavesPorFamilia, r.TemplatesSalvos,
                            quadro2 = Experimento.Quadro2(r).Select(t => new { teste = t.Teste, obtido = t.Obtido, situacao = t.Ok ? "Aprovado" : "Reprovado" }),
                            respostas = r.Conjunto.Select(q => new
                            {
                                q.N, q.Pergunta, q.Familia, q.Gabarito,
                                prototipo = r.RespostasPrototipo[q.N - 1], linhaDeBase = r.RespostasLinhaDeBase[q.N - 1],
                            }),
                        }, opcoes) + "\n",
                        ["avaliacao-humana.csv"] = planilha,
                        ["avaliacao-humana-chave.csv"] = chave,
                    };
                    foreach (var (nome, conteudo) in arquivos) File.WriteAllText(Path.Combine(saida, nome), conteudo);
                    lock (Trava)
                    {
                        Atual.Status = "concluido";
                        Atual.Resumo = new { prototipo = r.Prototipo, linhaDeBase = r.LinhaDeBase, comparacao = r.Comparacao, r.ChavesPorFamilia, r.TemplatesSalvos };
                        Atual.Arquivos.AddRange(arquivos.Keys.Select(k => Path.Combine(saida, k)));
                    }
                }
                catch (Exception e)
                {
                    lock (Trava) (Atual.Status, Atual.Mensagem) = ("erro", e.Message);
                }
            });
            await Enviar(ctx, 202, new { status = "rodando", llm, perguntas = total, semente, saida });
        });
    }
}
