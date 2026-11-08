using Orquestracao.Core.Conversas;
using Orquestracao.Core.Diagramas;

namespace Orquestracao.Core.Tests.Conversas;

/// <summary>Portado de legado/server/turnos.test.ts: as setas dizem quem responde a quem e quem delega para quem.</summary>
public class TurnosTests
{
    internal static Diagram D(string nos, params (string De, string Para)[] setas)
    {
        var d = new Diagram();
        foreach (var (id, i) in nos.Split(',').Select((n, i) => (n.Trim(), i)))
            d.Nodes.Add(new DiagramNode { Id = id, Text = id.ToUpperInvariant(), X = i * 200, Y = 100 });
        foreach (var (de, para) in setas) Ops.AddEdge(d, new NewEdge { From = de, To = para });
        return d;
    }

    [Fact]
    public void Ordem_PelasSetas_EmLargura()
    {
        var d = D("a,b,c,d", ("a", "b"), ("a", "c"), ("b", "d"));
        Assert.Equal(["a"], Turnos.Raizes(d));
        Assert.Equal(["a", "b", "c", "d"], Turnos.OrdemDaRodada(d));
    }

    [Fact]
    public void Ciclo_ComecaPeloPrimeiroOuPeloBlocoDeInicio()
    {
        var d = D("a,b,c", ("a", "b"), ("b", "c"), ("c", "a"));
        Assert.Equal(["a"], Turnos.Raizes(d));
        Assert.Equal(["b", "c", "a"], Turnos.OrdemDaRodada(d, ["b"]));
    }

    [Fact]
    public void QuemNaoFoiAlcancado_EntraDepois_ESetaDeCicloSeIgnora()
    {
        var d = D("a,b,solto", ("a", "b"), ("b", "b"));
        // "solto" não tem seta chegando: também abre a rodada (raiz), logo depois de "a".
        Assert.Equal(["a", "solto"], Turnos.Raizes(d));
        Assert.Equal(["a", "solto", "b"], Turnos.OrdemDaRodada(d));
        Assert.Equal(["a"], Turnos.Predecessores(d, "b"));
        Assert.Equal(["b"], Turnos.Delegaveis(d, "a"));
        Assert.Empty(Turnos.Delegaveis(d, "b"));
    }

    [Fact]
    public void SemNos_TiraORetanguloESuasSetas()
    {
        var d = Turnos.SemNos(D("a,b,c", ("a", "b"), ("b", "c")), ["b"]);
        Assert.Equal(["a", "c"], d.Nodes.Select(n => n.Id));
        Assert.Empty(d.Edges);
        Assert.Equal(["e1"], Turnos.Arestas(D("a,b", ("a", "b")), ["a"], "b"));
    }

    [Fact]
    public void AcharPessoa_SemAcentoNemMaiusculas()
    {
        Pessoa[] p = [new("n1", "Cética"), new("n2", "Moderadora Geral")];
        Assert.Equal("n1", Turnos.AcharPessoa("@CETICA", p)?.Id);
        Assert.Equal("n2", Turnos.AcharPessoa("n2", p)?.Id);
        Assert.Null(Turnos.AcharPessoa("Otimista", p));
    }

    [Fact]
    public void Mencionada_PegaONomeMaisLongo_EPalavraInteira()
    {
        Pessoa[] p = [new("n1", "Moderadora"), new("n2", "Moderadora Geral")];
        Assert.Equal("n2", Turnos.Mencionada("e aí, @moderadora geral?", p));
        Assert.Equal("n1", Turnos.Mencionada("@Moderadora, sua vez", p));
        Assert.Null(Turnos.Mencionada("@moderadoras", p));
        Assert.Null(Turnos.Mencionada("sem menção", p));
    }

    [Fact]
    public void LerDelegacoes_SoAsPermitidas_ERestoSemAsLinhas()
    {
        Pessoa[] p = [new("n2", "Pesquisadora"), new("n3", "Redatora")];
        var (dl, recusadas, resto) = Turnos.LerDelegacoes("Plano:\nDELEGAR Pesquisadora: levantar dados\ndelegar @redatora : escrever\nDELEGAR Revisora: revisar\nFim.", p);
        Assert.Equal([new Delegacao("n2", "levantar dados"), new Delegacao("n3", "escrever")], dl);
        Assert.Equal(["Revisora"], recusadas);
        Assert.Equal("Plano:\nFim.", resto);
    }
}
