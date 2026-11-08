using Orquestracao.Templates;
using static Orquestracao.Templates.Tests.Base;

namespace Orquestracao.Templates.Tests;

/// <summary>R01 (e o teste T01 do TCC): perguntas da mesma família geram a mesma chave.</summary>
public class ParserTests
{
    private readonly ParserHeuristico _p = new(Fonte);

    [Theory]
    [InlineData("Qual é a capital da França?")]
    [InlineData("Qual a capital do Japão?")]
    [InlineData("Me diga a capital de Portugal")]
    [InlineData("qual é a capital dos Estados Unidos?")]
    [InlineData("Você sabe qual é a capital da Coreia do Sul?")]
    [InlineData("QUAL É A CAPITAL DO BRASIL")]
    public void T01_MesmaFamilia_MesmaChave(string pergunta)
    {
        var e = _p.ExtrairSemLlm(pergunta);
        Assert.Equal("capital {pais}", e.Chave);
        Assert.True(e.TemSlot);
    }

    [Fact]
    public void Entidade_PeloNomeMaisLongo_EPeloApelido()
    {
        Assert.Equal("Coreia do Sul", _p.ExtrairSemLlm("Qual a moeda da Coreia do Sul?").Entidade!.Nome);
        Assert.Equal("Estados Unidos", _p.ExtrairSemLlm("Qual a capital dos EUA?").Entidade!.Nome);
        Assert.Equal("Brasil", _p.ExtrairSemLlm("capital do brasil").Entidade!.Nome); // sem acento e minúsculas
    }

    [Fact]
    public void FamiliasDiferentes_ChavesDiferentes()
    {
        string[] perguntas =
        [
            "Qual é a capital da França?", "Qual é a moeda da França?", "Que língua se fala na França?",
            "Em que continente fica a França?", "Qual o símbolo químico do ferro?", "Qual é o número atômico do ferro?",
        ];
        var chaves = perguntas.Select(p => _p.ExtrairSemLlm(p).Chave).ToList();
        Assert.Equal(chaves.Count, chaves.Distinct().Count());
        Assert.Equal("simbolo quimico {elemento}", chaves[4]);
        Assert.Equal("numero atomico {elemento}", _p.ExtrairSemLlm("Qual o número atômico da prata?").Chave);
    }

    [Fact]
    public void SemEntidadeConhecida_NaoTemSlot()
    {
        var e = _p.ExtrairSemLlm("Qual é a capital da Atlântida?");
        Assert.False(e.TemSlot);
        Assert.Equal("capital da atlantida", e.Chave);
    }

    [Fact]
    public async Task ParserLlm_UsaAEstruturaDoModelo_EConfereAEntidadeNaFonte()
    {
        var llm = new LlmSimulado((system, _) =>
        {
            Assert.Contains("estrutura", system);
            return "Claro! {\"estrutura\": \"qual é a capital de {entidade}\", \"entidade\": \"Japão\"}";
        });
        var (e, uso) = await new ParserLlm(llm, Fonte).Extrair("Me conta: capital japonesa?");
        Assert.Equal(("qual e a capital de {pais}", "Japão"), (e.Chave, e.Entidade!.Nome));
        Assert.NotNull(uso);

        var lixo = new LlmSimulado((_, _) => "não entendi");
        var (e2, _) = await new ParserLlm(lixo, Fonte).Extrair("Qual a capital?");
        Assert.Equal(("qual a capital", false), (e2.Chave, e2.TemSlot));
    }

    [Fact]
    public async Task ParserHibrido_SoChamaOLlmQuandoAHeuristicaNaoAcha()
    {
        var llm = new LlmSimulado((_, _) => "{\"estrutura\": \"capital de {entidade}\", \"entidade\": null}");
        var p = new ParserHibrido(_p, new ParserLlm(llm, Fonte));
        Assert.Null((await p.Extrair("Qual a capital do Peru?")).Uso);
        Assert.Equal(0, llm.Chamadas);
        await p.Extrair("Qual a capital de Wakanda?");
        Assert.Equal(1, llm.Chamadas);
    }
}
