using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orquestracao.Core.Conversas;
using Orquestracao.Core.Diagramas;

namespace Orquestracao.Web;

/// <summary>
/// As personas e as conversas de cada diagrama, ligadas ao <see cref="Workspace"/>: o .md de cada
/// persona fica em <c>diagrams/&lt;slug&gt;/personas/</c> e cada conversa em <c>diagrams/&lt;slug&gt;/conversas/</c>.
/// </summary>
public sealed class EspacoIa
{
    public Workspace Ws { get; }
    public RegistroDeMotores Motores { get; }
    public string PastaTemplates { get; }

    private readonly ConcurrentDictionary<string, Conversas> _conversas = new();

    private readonly Orquestracao.Templates.MotorTemplate? _template;

    public EspacoIa(Workspace ws, RegistroDeMotores motores, string pastaTemplates, Orquestracao.Templates.MotorTemplate? template = null)
    {
        _template = template;
        Ws = ws;
        Motores = motores;
        PastaTemplates = pastaTemplates;
        // Removeu o retângulo: a persona dele vai para a lixeira. Fechou o diagrama: as conversas param.
        ws.AoRemoverNo = (slug, id) => PersonasDe(slug).Remover(id);
        ws.AoFechar = slug =>
        {
            if (_conversas.TryRemove(slug, out var c)) c.Fechar();
        };
    }

    /// <summary>Títulos dos motores na paleta e no topo do chat.</summary>
    public static readonly Dictionary<string, (string Titulo, string Descricao)> TituloMotor = new()
    {
        ["mcp"] = ("MCP · Claude Code", "O Claude Code conectado ao /mcp dá voz à persona (não precisa de chave)."),
        ["ollama"] = ("Ollama · local", "Um modelo local no Ollama (grátis, na CPU: modelos de 3 a 4B)."),
        ["api"] = ("API · Claude", "A API da Anthropic (precisa de ANTHROPIC_API_KEY)."),
        ["template"] = ("Template · orquestrador", "O orquestrador do TCC: reaproveita templates de resposta e só chama o LLM para estruturas novas."),
    };

    public PersonaStore PersonasDe(string slug) => new(Path.Combine(Ws.PastaDe(slug), "personas"));

    public List<TemplatePapel> Templates() => TemplatesDePapel.Carregar(PastaTemplates);

    public List<Persona> PersonasDoDiagrama(string slug)
    {
        var p = PersonasDe(slug);
        return Ws.Diagrama(slug).Diagrama.Nodes.Select(n => p.Ler(n.Id, n.Text)).ToList();
    }

    /// <summary>As conversas do diagrama (criadas na primeira vez).</summary>
    public Conversas ConversasDe(string slug)
    {
        var a = Ws.Diagrama(slug);
        return _conversas.GetOrAdd(slug, _ =>
        {
            var personas = PersonasDe(slug);
            var pasta = Ws.PastaDe(slug);
            return new Conversas(new OpcoesConversas
            {
                Dir = Path.Combine(pasta, "conversas"),
                Diagrama = slug,
                Ler = () => a.Diagrama,
                Persona = (id, nome) => personas.Ler(id, nome),
                Motores = Motores,
                Emitir = ev =>
                {
                    ev["source"] = "server";
                    a.Transmitir(ev);
                },
                Template = personas.LerTemplate,
                // Aprovar / Pedir mudança na vez humana é o feedback do orquestrador por template (R05).
                AoFeedbackHumano = (conversa, persona, bom) => _template?.RegistrarFeedback(conversa, persona, bom),
                GravarSaida = (conversa, arquivo, texto) =>
                {
                    var dir = Path.Combine(pasta, "saidas", conversa);
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, arquivo), texto + "\n");
                },
            });
        });
    }

    /// <summary>Avisa as abas que a persona mudou (o canvas e o chat se atualizam).</summary>
    public void AvisarPersona(string slug, Persona p, string source) =>
        Ws.Diagrama(slug).Transmitir(new JsonObject
        {
            ["type"] = "persona.atualizada",
            ["id"] = p.Id,
            ["persona"] = JsonSerializer.SerializeToNode(p, Json.Opcoes),
            ["source"] = source,
        });

    /// <summary>A pasta templates/ do repositório: a configurada ou a primeira encontrada subindo de <paramref name="inicio"/>.</summary>
    public static string AcharTemplates(string? configurada, string inicio)
    {
        if (!string.IsNullOrEmpty(configurada)) return Path.GetFullPath(configurada);
        for (var d = new DirectoryInfo(inicio); d is not null; d = d.Parent)
        {
            var t = Path.Combine(d.FullName, "templates");
            if (Directory.Exists(t) && Directory.EnumerateFiles(t, "*.md").Any()) return t;
        }
        return Path.Combine(inicio, "templates");
    }
}
