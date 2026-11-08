using Orquestracao.Core.Diagramas;
using static Orquestracao.Core.Tests.Diagramas.OpsTests;

namespace Orquestracao.Core.Tests.Diagramas;

/// <summary>Portado de legado/src/ops.test.ts (bloco "Mermaid").</summary>
public class MermaidTests
{
    [Fact]
    public void Responsavel_VaiEVoltaDentroDoTextoDoRetangulo()
    {
        var d = Sample();
        Ops.UpdateNode(d, "n2", new NodePatch { Owner = "Maria Souza", Active = true });
        var code = Mermaid.Gerar(d);
        Assert.Contains("n2(\"Meio<br/><small>👤 Maria Souza</small>\")", code);
        Assert.DoesNotContain("@owner", code);
        var (diagram, errors) = Mermaid.Ler(code);
        Assert.Empty(errors);
        Assert.Equal([null, "Maria Souza", null], diagram.Nodes.Select(n => n.Owner));
        Assert.True(diagram.Nodes[1].Active);
        Assert.Equal("Meio", diagram.Nodes[1].Text);
    }

    [Fact]
    public void AspasESinaisMenorMaior_SobrevivemAIdaEVolta()
    {
        var d = Sample();
        Ops.UpdateNode(d, "n1", new NodePatch { Text = "Aprovar \"A\" <b>já</b>", Owner = "Zé <ze@x.com> \"Chefe\"" });
        var code = Mermaid.Gerar(d);
        Assert.DoesNotContain("<b>", code);
        var (diagram, errors) = Mermaid.Ler(code);
        Assert.Empty(errors);
        Assert.Equal("Aprovar \"A\" <b>já</b>", diagram.Nodes[0].Text);
        Assert.Equal("Zé <ze@x.com> \"Chefe\"", diagram.Nodes[0].Owner);
    }

    [Fact]
    public void AindaLeOResponsavelNoFormatoAntigo()
    {
        var (diagram, _) = Mermaid.Ler("flowchart LR\n    n1(\"A\")\n    n2(\"B\")\n%% @owner n2 Ana Lima");
        Assert.Equal([null, "Ana Lima"], diagram.Nodes.Select(n => n.Owner));
        // o texto do retângulo tem prioridade sobre o comentário
        var (both, _) = Mermaid.Ler("flowchart LR\n    n1(\"A<br/><small>👤 Bia</small>\")\n%% @owner n1 Ana Lima");
        Assert.Equal(("A", "Bia"), (both.Nodes[0].Text, both.Nodes[0].Owner));
    }

    [Fact]
    public void SetaAnimada_VaiEVoltaComASintaxeDeAnimacao()
    {
        var d = Sample();
        Ops.UpdateEdge(d, "e1", new EdgePatch { Animated = true });
        var code = Mermaid.Gerar(d);
        Assert.Contains("n1 e1@-->|\"processa\"| n2", code);
        Assert.Contains("e1@{ animate: true }", code);
        Assert.Contains("    n2 --> n3", code);
        var (diagram, errors) = Mermaid.Ler(code);
        Assert.Empty(errors);
        Assert.Equal(Json.Serializar(d.Edges), Json.Serializar(diagram.Edges));
        Ops.UpdateEdge(d, "e1", new EdgePatch { Animated = false });
        Assert.DoesNotContain("animated", Json.Serializar(d.Edges[0]));
        Assert.Equal(400, StatusOf(() => Entrada.PatchSeta(Entrada.Ler("""{"animated":"sim"}"""))));
    }

    [Fact]
    public void IdEscritoNoCodigo_ValeParaASeta()
    {
        var (diagram, _) = Mermaid.Ler("flowchart LR\n    a(\"A\")\n    b(\"B\")\n    a meu-id@--> b\n    meu-id@{ animate: true }");
        Assert.Equal("""[{"id":"meu-id","from":"a","to":"b","text":"","animated":true}]""", Json.Serializar(diagram.Edges));
    }

    [Fact]
    public void LerOCodigo_MantemOsIdsDasSetasQueJaExistiam()
    {
        var d = Sample();
        d.Edges[0].Id = "eabc";
        var code = Mermaid.Gerar(d).Replace("n2 --> n3", "n2 --> n3\n    n3 --> n1");
        var (diagram, errors) = Mermaid.Ler(code, d);
        Assert.Empty(errors);
        Assert.Equal(["eabc", "e2", "e3"], diagram.Edges.Select(e => e.Id));
    }

    [Fact]
    public void LinhaNaoReconhecida_ViraErroComONumeroDaLinha()
    {
        var (_, errors) = Mermaid.Ler("flowchart LR\n    a(\"A\")\n    isto não é mermaid");
        Assert.Equal(["Linha 3: não reconhecida — \"isto não é mermaid\""], errors);
    }

    [Fact]
    public void Posicoes_ArredondamComoNoJavaScript()
    {
        var d = new Diagram();
        Ops.AddNode(d, new NewNode { Text = "A", X = 10.5, Y = -2.5 });
        Assert.EndsWith("%% @pos n1 11 -2", Mermaid.Gerar(d));
    }
}
