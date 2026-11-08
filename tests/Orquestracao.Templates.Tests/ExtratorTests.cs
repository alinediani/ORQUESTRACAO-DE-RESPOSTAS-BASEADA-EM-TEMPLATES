using Orquestracao.Templates;
using static Orquestracao.Templates.Tests.Base;

namespace Orquestracao.Templates.Tests;

/// <summary>R03/R04: extrair o template da resposta do LLM e preenchê-lo com o dado da fonte.</summary>
public class ExtratorTests
{
    [Fact]
    public void ExtraiOTemplate_EPreencheParaOutraEntidade_ComAPreposicaoCerta()
    {
        var t = Extrator.ExtrairTemplate("A capital da França é Paris.", E("França"));
        Assert.Equal("A capital {de_entidade} é {capital}.", t);
        Assert.Equal("A capital do Japão é Tóquio.", Extrator.Preencher(t!, E("Japão"), Fonte));
        Assert.Equal("A capital de Portugal é Lisboa.", Extrator.Preencher(t!, E("Portugal"), Fonte));
        Assert.Equal("A capital dos Estados Unidos é Washington, D.C..", Extrator.Preencher(t!, E("Estados Unidos"), Fonte));
    }

    [Fact]
    public void ValorQueContemONome_ETrocadoAntes()
    {
        var t = Extrator.ExtrairTemplate("A capital do México é a Cidade do México.", E("México"));
        Assert.Equal("A capital {de_entidade} é a {capital}.", t);
        Assert.Equal("A capital da Itália é a Roma.", Extrator.Preencher(t!, E("Itália"), Fonte));
    }

    [Fact]
    public void MaiusculaNoComecoDaFrase()
    {
        var t = Extrator.ExtrairTemplate("Na França fala-se francês.", E("França"));
        Assert.Equal("{Em_entidade} fala-se {idioma}.", t);
        Assert.Equal("No Japão fala-se japonês.", Extrator.Preencher(t!, E("Japão"), Fonte));
        Assert.Equal("Em Portugal fala-se português.", Extrator.Preencher(t!, E("Portugal"), Fonte));
    }

    [Fact]
    public void ValorCurto_NaoConfundeComOArtigoDoComeco()
    {
        var t = Extrator.ExtrairTemplate("O símbolo químico do oxigênio é O.", E("oxigênio"));
        Assert.Equal("O símbolo químico {de_entidade} é {simbolo}.", t);
        Assert.Equal("O símbolo químico do ferro é Fe.", Extrator.Preencher(t!, E("ferro"), Fonte));
        Assert.Equal("O símbolo químico da prata é Ag.", Extrator.Preencher(t!, E("prata"), Fonte));
    }

    [Fact]
    public void RespostaSemDadoDaFonte_NaoEReutilizavel()
    {
        Assert.Null(Extrator.ExtrairTemplate("Depende do ponto de vista sobre a França.", E("França")));
        Assert.Equal(["capital"], Extrator.Atributos("A capital {de_entidade} é {capital}."));
    }

    [Fact]
    public void FonteSemOAtributo_NaoPreenche()
    {
        Assert.Null(Extrator.Preencher("O símbolo {de_entidade} é {simbolo}.", E("França"), Fonte)); // país não tem símbolo químico
    }
}
