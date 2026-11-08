using Orquestracao.Core.Diagramas;

namespace Orquestracao.Core.Tests.Diagramas;

/// <summary>Portado de legado/src/ops.test.ts (retângulos, setas, eventos e Mermaid).</summary>
public class OpsTests
{
    internal static Diagram Sample()
    {
        var d = new Diagram();
        Ops.AddNode(d, new NewNode { Text = "Início", X = 100, Y = 100 });
        Ops.AddNode(d, new NewNode { Text = "Meio", X = 300, Y = 100 });
        Ops.AddNode(d, new NewNode { Text = "Fim", X = 500, Y = 100 });
        Ops.AddEdge(d, new NewEdge { From = "n1", To = "n2", Text = "processa" });
        Ops.AddEdge(d, new NewEdge { From = "n2", To = "n3" });
        return d;
    }

    /// <summary>O status do OpError lançado, ou 0 quando a operação passa.</summary>
    internal static int StatusOf(Action acao)
    {
        try
        {
            acao();
            return 0;
        }
        catch (OpError err)
        {
            return err.Status;
        }
    }

    private static System.Text.Json.JsonElement J(string json) => Entrada.Ler(json);

    private static string Js(object o) => Json.Serializar(o);

    // ---- retângulos ----------------------------------------------------------

    [Fact]
    public void Cria_ComIdSequencialOuComOIdPedido()
    {
        var d = Sample();
        Assert.Equal(["n1", "n2", "n3"], d.Nodes.Select(n => n.Id));
        var ev = Assert.IsType<NodeAdded>(Ops.AddNode(d, new NewNode { Id = "meu-id", Text = "X" }));
        Assert.Equal(("meu-id", "X"), (ev.Node.Id, ev.Node.Text));
    }

    [Fact]
    public void SemPosicao_VaiParaUmaVagaLivre()
    {
        var d = Sample();
        var ev = Assert.IsType<NodeAdded>(Ops.AddNode(d, new NewNode { Text = "Solto" }));
        foreach (var n in d.Nodes.Take(3))
            Assert.True(Math.Abs(n.X - ev.Node.X) >= 180 || Math.Abs(n.Y - ev.Node.Y) >= 70);
    }

    [Fact]
    public void ValidaAEntrada()
    {
        var d = Sample();
        Assert.Equal(409, StatusOf(() => Ops.AddNode(d, new NewNode { Id = "n1", Text = "dup" })));
        Assert.Equal(400, StatusOf(() => Ops.AddNode(d, new NewNode { Id = "com espaço" })));
        Assert.Equal(400, StatusOf(() => Ops.AddNode(d, new NewNode { Text = "  " })));
        Assert.Equal(400, StatusOf(() => Entrada.NovoNo(J("""{"text":"a","cor":"azul"}"""))));
        Assert.Equal(400, StatusOf(() => Entrada.PatchNo(J("""{"x":"longe"}"""))));
        Assert.Equal(404, StatusOf(() => Ops.UpdateNode(d, "n9", new NodePatch { Text = "a" })));
    }

    [Fact]
    public void Patch_AlteraSoOsCamposEnviados()
    {
        var d = Sample();
        var before = d.Clone();
        Ops.UpdateNode(d, "n2", new NodePatch { Active = true });
        Assert.Equal(Js(before.Nodes[1]).Replace("}", ",\"active\":true}"), Js(d.Nodes[1]));
        Assert.Equal(Js(before.Nodes[0]), Js(d.Nodes[0]));
        Assert.Equal(Js(before.Edges), Js(d.Edges));
        Ops.UpdateNode(d, "n2", new NodePatch { Active = false });
        Assert.Equal(Js(before.Nodes[1]), Js(d.Nodes[1]));
    }

    [Fact]
    public void Responsavel_NormalizaRemoveComVazioOuNullEValida()
    {
        var d = Sample();
        Ops.UpdateNode(d, "n1", new NodePatch { Owner = "  Maria \n Souza " });
        Assert.Equal("Maria Souza", d.Nodes[0].Owner);
        Ops.UpdateNode(d, "n1", Entrada.PatchNo(J("""{"owner":null}""")));
        Assert.DoesNotContain("owner", Js(d.Nodes[0]));
        Ops.UpdateNode(d, "n1", new NodePatch { Owner = "João" });
        Ops.UpdateNode(d, "n1", new NodePatch { Owner = "   " });
        Assert.DoesNotContain("owner", Js(d.Nodes[0]));
        Assert.Equal(400, StatusOf(() => Entrada.PatchNo(J("""{"owner":7}"""))));
        Assert.Equal(400, StatusOf(() => Ops.UpdateNode(d, "n1", new NodePatch { Owner = new string('x', 61) })));
        var ev = Assert.IsType<NodeAdded>(Ops.AddNode(d, new NewNode { Text = "Com dono", Owner = "Ana" }));
        Assert.Equal("Ana", ev.Node.Owner);
    }

    [Fact]
    public void Remover_LevaJuntoAsSetasLigadas()
    {
        var d = Sample();
        var ev = Assert.IsType<NodeRemoved>(Ops.RemoveNode(d, "n2"));
        Assert.Equal("n2", ev.Id);
        Assert.Equal(["e1", "e2"], ev.Edges);
        Assert.Empty(d.Edges);
        Assert.Equal("""{"type":"node.removed","id":"n2","edges":["e1","e2"]}""", Json.Serializar<DiagramEvent>(ev));
    }

    [Fact]
    public void EncontraPeloTexto_SemDiferenciarMaiusculas()
    {
        var d = Sample();
        Assert.Equal("n2", Ops.ResolveNode(d, "meio").Id);
        Ops.AddNode(d, new NewNode { Text = "Meio" });
        Assert.Equal(409, StatusOf(() => Ops.ResolveNode(d, "Meio")));
        Assert.Equal(404, StatusOf(() => Ops.ResolveNode(d, "nada")));
    }

    // ---- setas ---------------------------------------------------------------

    [Fact]
    public void Seta_AceitaCicloERecusaDuplicadaOuComPontaInexistente()
    {
        var d = Sample();
        var ev = Assert.IsType<EdgeAdded>(Ops.AddEdge(d, new NewEdge { From = "n2", To = "n2", Text = "de novo" }));
        Assert.Equal(("e3", "n2", "n2"), (ev.Edge.Id, ev.Edge.From, ev.Edge.To));
        Assert.Equal(409, StatusOf(() => Ops.AddEdge(d, new NewEdge { From = "n1", To = "n2" })));
        Assert.Equal(400, StatusOf(() => Ops.AddEdge(d, new NewEdge { From = "n1", To = "n9" })));
    }

    [Fact]
    public void Seta_PatchMudaTextoEPontasValidandoDuplicadas()
    {
        var d = Sample();
        Ops.UpdateEdge(d, "e2", new EdgePatch { Text = "finaliza", To = "n1" });
        Assert.Equal("""{"id":"e2","from":"n2","to":"n1","text":"finaliza"}""", Js(d.Edges[1]));
        Assert.Equal(409, StatusOf(() => Ops.UpdateEdge(d, "e2", new EdgePatch { From = "n1", To = "n2" })));
    }

    // ---- eventos -------------------------------------------------------------

    [Fact]
    public void AplicarOsEventosNumaCopia_ReproduzAsOperacoes()
    {
        var a = Sample();
        var b = a.Clone();
        DiagramEvent[] events =
        [
            Ops.UpdateNode(a, "n1", new NodePatch { Text = "Começo", X = 120 }),
            Ops.AddNode(a, new NewNode { Text = "Novo", X = 10, Y = 10 }),
            Ops.AddEdge(a, new NewEdge { From = "n4", To = "n4" }),
            Ops.UpdateEdge(a, "e1", new EdgePatch { Text = "vai" }),
            Ops.RemoveEdge(a, "e2"),
            Ops.RemoveNode(a, "n3"),
        ];
        // Os eventos viajam como JSON (como pelo SSE) e são aplicados na outra cópia.
        var copy = b;
        foreach (var ev in events)
            copy = Ops.ApplyEvent(copy, Json.Ler<DiagramEvent>(Json.Serializar(ev))!);
        Assert.Equal(Js(a), Js(copy));
    }

    [Fact]
    public void Diagrama_ValidaUmDiagramaInteiro()
    {
        Assert.Equal(Js(Sample()), Js(Entrada.Diagrama(J(Js(Sample())))));
        Assert.Equal(400, StatusOf(() => Entrada.Diagrama(J("""{"nodes":[],"edges":[{"from":"a","to":"b"}]}"""))));
        Assert.Equal(400, StatusOf(() => Entrada.Diagrama(J("\"x\""))));
    }
}
