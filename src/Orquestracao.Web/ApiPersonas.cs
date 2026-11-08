using System.Text.Json;
using System.Text.RegularExpressions;
using Orquestracao.Core.Conversas;
using Orquestracao.Core.Diagramas;
using Orquestracao.Motores;
using static Orquestracao.Web.Http;

namespace Orquestracao.Web;

/// <summary>
/// As rotas das personas e das conversas (equivalem às rotas de IA de legado/server/diagrams.ts).
/// <code>
/// GET  /api/templates   /api/motores
/// POST /api/d/{slug}/templates/{papel}                 cria o retângulo e a persona {motor, x?, y?, text?}
/// GET  /api/d/{slug}/personas      GET|PUT /personas/{id}      GET|PUT /personas/{id}/saida
/// GET|POST /api/d/{slug}/conversas  GET /conversas/{id}
/// POST /conversas/{id}/mensagens|sintetizar|parar|humano     GET /conversas/{id}/saidas/{arquivo}
/// GET|PUT|DELETE /api/d/{slug}/memoria
/// </code>
/// </summary>
public static class ApiPersonas
{
    private static readonly JsonSerializerOptions SemNulos = new(Conversa.JsonOpcoes) { WriteIndented = true };

    /// <summary>JSON sem campos nulos (conversas, templates).</summary>
    private static async Task EnviarSemNulos(HttpContext ctx, int status, object corpo)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(corpo, corpo.GetType(), SemNulos) + "\n");
    }

    private static string? Texto(JsonElement j, string campo) =>
        j.ValueKind == JsonValueKind.Object && j.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static void MapApiPersonas(this WebApplication app)
    {
        var ia = app.Services.GetRequiredService<EspacoIa>();
        var ws = ia.Ws;

        app.MapGet("/api/templates", (HttpContext ctx) =>
        {
            var todos = ia.Templates();
            return EnviarSemNulos(ctx, 200, new
            {
                motores = ia.Motores.Todos.Select(m => m.Nome).Where(EspacoIa.TituloMotor.ContainsKey)
                    .Select(m => new { nome = m, titulo = EspacoIa.TituloMotor[m].Titulo, descricao = EspacoIa.TituloMotor[m].Descricao }),
                papeis = todos.Where(t => t.Especial is null),
                especiais = todos.Where(t => t.Especial is not null),
            });
        });

        app.MapGet("/api/motores", async (HttpContext ctx) =>
        {
            var lista = new List<object>();
            foreach (var m in ia.Motores.Todos.Where(m => EspacoIa.TituloMotor.ContainsKey(m.Nome)))
            {
                var d = await m.Disponivel(ctx.RequestAborted);
                var modelos = m is MotorOllama o ? await o.ModelosDisponiveis(ctx.RequestAborted) : MotorApi.Modelos.ToList();
                lista.Add(new { nome = m.Nome, titulo = EspacoIa.TituloMotor[m.Nome].Titulo, descricao = EspacoIa.TituloMotor[m.Nome].Descricao, ok = d.Ok, detalhe = d.Detalhe, modelos });
            }
            await Enviar(ctx, 200, lista);
        });

        var g = app.MapGroup("/api/d/{slug}");
        static string Slug(HttpContext ctx) => (string)ctx.Request.RouteValues["slug"]!;

        g.MapPost("/templates/{papel}", async (HttpContext ctx, string papel) =>
        {
            var slug = Slug(ctx);
            var t = ia.Templates().Find(x => x.Nome == papel) ?? throw new OpError(404, $"Template \"{papel}\" não existe.");
            var corpo = await LerJson(ctx);
            var motor = Texto(corpo, "motor") ?? "mcp";
            if (!ia.Motores.Tem(motor) || !EspacoIa.TituloMotor.ContainsKey(motor))
                throw new OpError(400, $"\"motor\" deve ser {string.Join(", ", ia.Motores.Todos.Select(m => m.Nome))}.");
            double? Num(string k) => corpo.ValueKind == JsonValueKind.Object && corpo.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
            var texto = Texto(corpo, "text");
            var novo = new NewNode { Text = string.IsNullOrWhiteSpace(texto) ? t.Titulo : texto, X = Num("x"), Y = Num("y") };
            var (ev, _) = ws.Diagrama(slug).Commit(Source(ctx), d => Ops.AddNode(d, novo));
            var no = ((NodeAdded)ev).Node;
            var persona = TemplatesDePapel.PersonaDo(t, no.Id, motor, no.Text);
            var store = ia.PersonasDe(slug);
            store.Gravar(persona);
            if (t.TemplateSaida is not null) store.GravarTemplate(persona.Id, t.TemplateSaida);
            ia.AvisarPersona(slug, persona, Source(ctx));
            await Enviar(ctx, 201, new { node = no, persona });
        });

        // ---- personas ---------------------------------------------------------
        DiagramNode No(string slug, string id) =>
            ws.Diagrama(slug).Diagrama.Nodes.Find(n => n.Id == id) ?? throw new OpError(404, $"Retângulo \"{id}\" não encontrado.");

        // O diagrama da Figura 1 é a visualização do orquestrador: os blocos são etapas, não personas.
        static bool EhAFigura(HttpContext ctx) => Slug(ctx) == OrquestradorAoVivo.Slug;
        g.MapGet("/personas", (HttpContext ctx) => Enviar(ctx, 200, EhAFigura(ctx) ? [] : ia.PersonasDoDiagrama(Slug(ctx))));
        g.MapGet("/personas/{id}", (HttpContext ctx, string id) =>
        {
            var n = No(Slug(ctx), id);
            var store = ia.PersonasDe(Slug(ctx));
            return Enviar(ctx, 200, new { markdown = store.LerMd(n.Id, n.Text), persona = store.Ler(n.Id, n.Text) });
        });
        g.MapPut("/personas/{id}", async (HttpContext ctx, string id) =>
        {
            var n = No(Slug(ctx), id);
            var store = ia.PersonasDe(Slug(ctx));
            store.GravarMd(n.Id, await LerTexto(ctx));
            var persona = store.Ler(n.Id, n.Text);
            ia.AvisarPersona(Slug(ctx), persona, Source(ctx));
            await Enviar(ctx, 200, new { persona });
        });
        // O template da persona de saída: lido e gravado à parte, sem tocar no .md da persona.
        g.MapGet("/personas/{id}/saida", (HttpContext ctx, string id) =>
        {
            var n = No(Slug(ctx), id);
            var store = ia.PersonasDe(Slug(ctx));
            return Enviar(ctx, 200, new { template = store.LerTemplate(n.Id), arquivo = store.Ler(n.Id, n.Text).Arquivo, padrao = !store.TemTemplate(n.Id) });
        });
        g.MapPut("/personas/{id}/saida", async (HttpContext ctx, string id) =>
        {
            var n = No(Slug(ctx), id);
            ia.PersonasDe(Slug(ctx)).GravarTemplate(n.Id, await LerTexto(ctx));
            await Enviar(ctx, 200, new { ok = true });
        });

        // ---- conversas --------------------------------------------------------
        g.MapGet("/conversas", (HttpContext ctx) => EnviarSemNulos(ctx, 200, ia.ConversasDe(Slug(ctx)).Listar()));
        g.MapPost("/conversas", async (HttpContext ctx) =>
        {
            if (EhAFigura(ctx))
                throw new OpError(400, "Este diagrama é a visualização do orquestrador (Figura 1): pergunte pelo painel (orquestrador.html) ou por uma persona com motor Template.");
            var corpo = await LerJson(ctx);
            int? rodadas = null;
            if (corpo.ValueKind == JsonValueKind.Object && corpo.TryGetProperty("rodadas", out var r))
            {
                if (r.ValueKind != JsonValueKind.Number || !r.TryGetInt32(out var n)) throw new OpError(400, "\"rodadas\" deve ser de 1 a 10.");
                rodadas = n;
            }
            var c = ia.ConversasDe(Slug(ctx)).Iniciar(Texto(corpo, "assunto"), Texto(corpo, "modo"), rodadas);
            await EnviarSemNulos(ctx, 201, c);
        });
        g.MapGet("/conversas/{id}", (HttpContext ctx, string id) => EnviarSemNulos(ctx, 200, ia.ConversasDe(Slug(ctx)).Obter(id)));
        g.MapPost("/conversas/{id}/mensagens", async (HttpContext ctx, string id) =>
            await EnviarSemNulos(ctx, 202, ia.ConversasDe(Slug(ctx)).Intervir(id, Texto(await LerJson(ctx), "texto"))));
        g.MapPost("/conversas/{id}/sintetizar", (HttpContext ctx, string id) =>
        {
            ia.ConversasDe(Slug(ctx)).Sintetizar(id);
            return Enviar(ctx, 202, new { ok = true });
        });
        g.MapPost("/conversas/{id}/parar", (HttpContext ctx, string id) =>
        {
            ia.ConversasDe(Slug(ctx)).Parar(id);
            return Enviar(ctx, 202, new { ok = true });
        });
        g.MapPost("/conversas/{id}/humano", async (HttpContext ctx, string id) =>
        {
            var corpo = await LerJson(ctx);
            ia.ConversasDe(Slug(ctx)).ResponderHumano(id, new RespostaHumana(Texto(corpo, "acao") ?? "", Texto(corpo, "texto")));
            await Enviar(ctx, 202, new { ok = true });
        });
        g.MapGet("/conversas/{id}/saidas/{arquivo}", async (HttpContext ctx, string id, string arquivo) =>
        {
            var slug = Slug(ctx);
            ia.ConversasDe(slug).Obter(id);
            var caminho = Path.Combine(ws.PastaDe(slug), "saidas", id, arquivo);
            if (!Personas.ArquivoRe().IsMatch(arquivo) || arquivo.Contains("..") || !File.Exists(caminho))
                throw new OpError(404, $"A conversa não gerou \"{arquivo}\".");
            ctx.Response.ContentType = "text/markdown; charset=utf-8";
            ctx.Response.Headers.ContentDisposition = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(arquivo)}";
            await ctx.Response.SendFileAsync(caminho);
        });

        // ---- memória (só o arquivo: a persona de memória não age nesta versão) -----
        static int ContarItens(string md) => md.Split('\n').Count(l => Regex.IsMatch(l, @"^\s*[-*] \S"));
        g.MapGet("/memoria", (HttpContext ctx) =>
        {
            var arq = Path.Combine(ws.PastaDe(Slug(ctx)), "memoria.md");
            var md = File.Exists(arq) ? File.ReadAllText(arq) : "";
            return Enviar(ctx, 200, new { md = md.Length > 0 ? md : Personas.MemoriaPadrao, existe = md.Length > 0, itens = ContarItens(md) });
        });
        g.MapPut("/memoria", async (HttpContext ctx) =>
        {
            var md = await LerTexto(ctx);
            if (string.IsNullOrWhiteSpace(md)) throw new OpError(400, "A memória não pode ficar vazia (para apagar, use Zerar).");
            File.WriteAllText(Path.Combine(ws.PastaDe(Slug(ctx)), "memoria.md"), md.Trim() + "\n");
            await Enviar(ctx, 200, new { ok = true, itens = ContarItens(md) });
        });
        g.MapDelete("/memoria", (HttpContext ctx) =>
        {
            var pasta = ws.PastaDe(Slug(ctx));
            var arq = Path.Combine(pasta, "memoria.md");
            if (File.Exists(arq))
            {
                Directory.CreateDirectory(Path.Combine(pasta, ".removidos"));
                File.Move(arq, Path.Combine(pasta, ".removidos", $"memoria-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.md"));
            }
            return Enviar(ctx, 200, new { ok = true });
        });
    }
}
