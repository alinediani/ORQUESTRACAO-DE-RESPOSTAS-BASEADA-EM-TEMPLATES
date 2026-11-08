using Orquestracao.Core.Conversas;
using Orquestracao.Core.Diagramas;

namespace Orquestracao.Core.Tests.Conversas;

/// <summary>Portado de legado/server/personas.test.ts: o .md da persona, o yaml, a validação e os templates.</summary>
public class PersonasTests
{
    private static int Status(Action a)
    {
        try
        {
            a();
            return 0;
        }
        catch (OpError e)
        {
            return e.Status;
        }
    }

    private static string Md(string yaml, string prompt = "Você é a Cética.") => $"# Cética\n\n{prompt}\n\n```yaml\n{yaml}\n```\n";

    /// <summary>A pasta templates/ do repositório (subindo a partir da pasta do teste).</summary>
    internal static string PastaTemplates()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "templates")) && File.Exists(Path.Combine(d.FullName, "OrquestracaoTemplates.slnx")))
                return Path.Combine(d.FullName, "templates");
        throw new DirectoryNotFoundException("templates/ do repositório");
    }

    [Fact]
    public void SemYaml_UsaOsPadroes()
    {
        var p = Personas.Ler("n1", "# X\n\nSó o prompt.", "Cética");
        Assert.Equal(("Só o prompt.", "mcp", "claude-opus-5-5", "medium", "debatedora"), (p.Prompt, p.Motor, p.Modelo, p.Esforco, p.Papel));
        Assert.Matches("^#[0-9a-f]{6}$", p.Cor);
        Assert.Equal(p.Cor, Personas.Ler("n1", "x", "y").Cor); // cor estável pelo id
        Assert.Null(p.Escuta); // "todas"
    }

    [Fact]
    public void LeOYaml_EOModeloPadraoDeCadaMotor()
    {
        var p = Personas.Ler("n2", Md("motor: ollama\npapel: moderadora\ncor: \"#e11d48\"\ninicio: true\nhumana: true\nprazo: 30\naoVencer: encerrar"), "Cética");
        Assert.Equal(("ollama", "qwen2.5:3b", "moderadora", "#e11d48"), (p.Motor, p.Modelo, p.Papel, p.Cor));
        Assert.True(p.Inicio && p.Humana);
        Assert.Equal((30.0, "encerrar"), (p.Prazo, p.AoVencer));
        Assert.Equal("Você é a Cética.", p.Prompt);
    }

    [Fact]
    public void Valida_TiposEValores()
    {
        Assert.Equal(400, Status(() => Personas.Ler("n1", Md("motor: gpt"), "x")));
        Assert.Equal(400, Status(() => Personas.Ler("n1", Md("cor: azul"), "x")));
        Assert.Equal(400, Status(() => Personas.Ler("n1", Md("inicio: \"true\""), "x"))); // texto, não booleano
        Assert.Equal(400, Status(() => Personas.Ler("n1", Md("prazo: 0"), "x")));
        Assert.Equal(400, Status(() => Personas.Ler("n1", Md("ferramentas: [bash]"), "x")));
        Assert.Equal(400, Status(() => Personas.Ler("n1", Md("arquivo: ../fora.md"), "x")));
        Assert.Equal(400, Status(() => Personas.Ler("n1", Md("modelo: \"\""), "x")));
        Assert.Equal(400, Status(() => Personas.Ler("n1", Md("motor: [quebrado"), "x")));
        Assert.Equal(400, Status(() => Personas.Ler("n1", Md("escuta: [\"com espaço\"]"), "x")));
        Assert.Equal(0, Status(() => Personas.Ler("n1", Md("ferramentas: [web_search]\nescuta: [n1, n3]"), "x")));
    }

    [Fact]
    public void Escrever_EIdaEVolta()
    {
        var p = Personas.Ler("n1", Md("motor: api\nmodelo: claude-sonnet-5-5\nesforco: high\nsaida: true\narquivo: README.md\nferramentas: [web_search]"), "Cética");
        var md = Personas.Escrever(p);
        Assert.StartsWith("# Cética\n\nVocê é a Cética.\n\n```yaml\n", md);
        Assert.Contains("cor: \"#", md); // # no começo exige aspas no yaml
        Assert.DoesNotContain("inicio", md); // só o que difere do padrão
        Assert.Equal(Json.Serializar(p), Json.Serializar(Personas.Ler("n1", md, "Cética")));
    }

    [Fact]
    public void MotorTemplate_TemFallbackPadraoNaApi()
    {
        var p = Personas.Ler("n1", Md("motor: template"), "Orquestrador");
        Assert.Equal(("template", "api", "claude-sonnet-5-5"), (p.Motor, p.Fallback, p.Modelo));
        Assert.Equal("ollama", Personas.Ler("n1", Md("motor: template\nfallback: ollama"), "x").Fallback);
        Assert.Contains("fallback: api", Personas.Escrever(p));
    }

    [Fact]
    public void Store_GravaLeEMandaParaALixeira()
    {
        var dir = Directory.CreateTempSubdirectory("personas-").FullName;
        try
        {
            var s = new PersonaStore(dir);
            Assert.Contains("Você é Meio.", s.LerMd("n1", "Meio")); // sem arquivo: o padrão
            Assert.Equal(400, Status(() => s.GravarMd("n1", Md("motor: x"))));
            s.GravarMd("n1", Md("motor: api"));
            Assert.Equal("api", s.Ler("n1", "Cética").Motor);
            Assert.Equal(Personas.TemplateSaidaPadrao, s.LerTemplate("n1"));
            s.GravarTemplate("n1", "# Meu template");
            Assert.True(s.TemTemplate("n1"));
            Assert.Equal(["n1"], s.Ids());
            Assert.Equal(["n1"], s.IdsComTemplate());
            s.Remover("n1");
            Assert.False(s.Existe("n1"));
            Assert.Equal(2, Directory.GetFiles(Path.Combine(dir, ".removidos")).Length);
            Assert.Equal(400, Status(() => s.Ler("../x", "x")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Templates_DoRepositorio_ViramPersonasEmQualquerMotor()
    {
        var todos = TemplatesDePapel.Carregar(PastaTemplates());
        var cetica = todos.Single(t => t.Nome == "cetica");
        Assert.Equal(("Cética", "debatedora", "#e11d48"), (cetica.Titulo, cetica.Papel, cetica.Cor));
        Assert.Equal("qwen2.5:3b", cetica.Modelos["ollama"]);
        var p = TemplatesDePapel.PersonaDo(cetica, "n7", "ollama");
        Assert.Equal(("n7", "Cética", "ollama", "qwen2.5:3b"), (p.Id, p.Nome, p.Motor, p.Modelo));

        var voce = todos.Single(t => t.Especial == "humana");
        Assert.True(TemplatesDePapel.PersonaDo(voce, "n8", "mcp").Humana);
        var saida = todos.Single(t => t.Nome == "saida-readme");
        Assert.Equal("saida", saida.Especial);
        Assert.NotNull(saida.TemplateSaida);
        Assert.True(TemplatesDePapel.PersonaDo(saida, "n9", "api").Saida);
    }
}
