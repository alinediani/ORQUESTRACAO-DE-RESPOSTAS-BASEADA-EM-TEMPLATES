using static Orquestracao.Templates.Tests.Base;

namespace Orquestracao.Templates.Tests;

/// <summary>Os cenários com bases públicas: leitura, amostra, análise estrutural e a correção de respostas numéricas.</summary>
public class CenariosTests
{
    [Fact]
    public void Gsm8k_OGabaritoEONumeroDepoisDoQuatroCerquilhas()
    {
        var arq = Path.GetTempFileName();
        File.WriteAllText(arq, """
            {"question":"Ana tem 2 maçãs e ganha 3. Quantas tem?","answer":"2+3=<<2+3=5>>5\n#### 5"}
            {"question":"Um carro custa 1,200 reais.","answer":"...\n#### 1,200"}

            """);
        var l = Cenarios.LerGsm8k(arq);
        Assert.Equal([("5", "gsm8k", 1), ("1200", "gsm8k", 2)], l.Select(p => (p.Gabarito, p.Familia, p.N)));
        File.Delete(arq);
    }

    [Fact]
    public void Amostra_ReprodutivelERenumerada()
    {
        var todas = Enumerable.Range(1, 50).Select(i => new PerguntaTeste(i, $"p{i}", "f", "", "a", $"{i}")).ToList();
        var a = Cenarios.Amostra(todas, 10, 7);
        Assert.Equal(Enumerable.Range(1, 10), a.Select(p => p.N));
        Assert.Equal(a.Select(p => p.Pergunta), Cenarios.Amostra(todas, 10, 7).Select(p => p.Pergunta));
        Assert.Equal(10, a.Select(p => p.Pergunta).Distinct().Count());
    }

    [Fact]
    public void Analise_ContaChavesETetoDeReaproveitamento()
    {
        var perguntas = new[]
        {
            "Qual é a capital da França?", "Qual a capital do Japão?", "Me diga a capital do Peru.", // 1 chave, 2 repetições
            "Qual é a moeda do Japão?",                                                           // 1 chave nova
            "Ana tem 2 maçãs e ganha 3.", "Ana tem 7 maçãs e ganha 1.",                           // sem entidade; mesma estrutura abstrata
        };
        var a = Cenarios.Analisar("teste", perguntas, new ParserHeuristico(Fonte));
        Assert.Equal((6, 4, 2), (a.Perguntas, a.ComEntidade, a.ChavesComEntidade));
        Assert.Equal(2, a.TetoReaproveitamento);
        Assert.Equal(4, a.ChavesDistintas);
        Assert.Equal(3, a.ChavesAbstratasDistintas);
        Assert.Equal(("capital {pais}", 3), a.MaisFrequentes[0]);
        Assert.Contains("Teto de reaproveitamento", Cenarios.RelatorioAnalise(a, "de teste"));
    }

    [Theory]
    [InlineData("James corre 540 metros por semana.", "540", true)]
    [InlineData("O total é de R$ 1.200,00.", "1200", true)]
    [InlineData("Ele tinha 32 hectares.", "32", true)]
    [InlineData("Ele tinha 12,8 hectares.", "32", false)]
    [InlineData("São 5400 metros.", "540", false)]
    [InlineData("O preço é 2,5 reais.", "2.5", true)]
    public void Correta_GabaritoNumericoValePeloValor(string resposta, string gabarito, bool esperado) =>
        Assert.Equal(esperado, Experimento.Correta(resposta, gabarito));
}
