using Orquestracao.Templates;
using static Orquestracao.Templates.Tests.Base;

namespace Orquestracao.Templates.Tests;

/// <summary>F7: o experimento do TCC (T01, T05 e T06 sobre o conjunto de 150 perguntas), com o LLM simulado.</summary>
public class ExperimentoTests
{
    private static async Task<ResultadoExperimento> Rodar(double taxaErro = 0.05, int semente = 2026)
    {
        var conjunto = Experimento.GerarConjunto(Fonte, semente);
        return await Experimento.Rodar(Fonte, conjunto, new LlmDeFatos(Fonte, taxaErro, 7), new LlmDeFatos(Fonte, taxaErro, 7), new JuizPorGabarito(),
            "simulado", semente, taxaErro, latenciaLlmMs: 1200);
    }

    [Fact]
    public void Conjunto_150Perguntas_EmSeisFamilias_ComGabarito_EReprodutivel()
    {
        var c = Experimento.GerarConjunto(Fonte);
        Assert.Equal(150, c.Count);
        Assert.Equal(6, c.Select(p => p.Familia).Distinct().Count());
        Assert.True(c.Count(p => p.Familia == "capital") > c.Count(p => p.Familia == "idioma")); // as perguntas em alta aparecem mais
        Assert.All(c, p => Assert.Equal(p.Gabarito, E(p.Entidade).Atributos[p.Atributo]));
        Assert.Equal(c.Select(p => p.Pergunta), Experimento.GerarConjunto(Fonte).Select(p => p.Pergunta)); // mesma semente, mesmo conjunto
        Assert.Contains(c, p => p.Pergunta.Contains(" do ") || p.Pergunta.Contains(" da "));
    }

    [Fact]
    public async Task T01_CadaFamiliaGeraUmaChaveSo_NoConjuntoInteiro()
    {
        var r = await Rodar();
        Assert.All(r.ChavesPorFamilia.Values, n => Assert.Equal(1, n));
    }

    [Fact]
    public async Task T05_OPrototipoChamaOLlmMenosQueALinhaDeBase()
    {
        var r = await Rodar(taxaErro: 0);
        Assert.Equal(150, r.LinhaDeBase.ChamadasGeracao);
        Assert.Equal(6, r.Prototipo.ChamadasGeracao); // uma por família: a primeira pergunta de cada estrutura
        Assert.Equal(144, r.Prototipo.AcertosTemplate);
        Assert.True(r.Prototipo.Tokens < r.LinhaDeBase.Tokens && r.Prototipo.Custo < r.LinhaDeBase.Custo);
        Assert.True(r.Prototipo.LatenciaMediaMs < r.LinhaDeBase.LatenciaMediaMs);
    }

    [Fact]
    public async Task T06_AQualidadeNaoPiora_ENaoHaRespostaInventadaVindaDoTemplate()
    {
        var r = await Rodar(taxaErro: 0.05);
        Assert.True(r.Prototipo.TaxaCorretas >= r.LinhaDeBase.TaxaCorretas);
        Assert.True(r.Comparacao.VitoriasPrototipo >= r.Comparacao.VitoriasLinhaDeBase);
        // O que vem do template vem da fonte: nenhuma resposta do caminho do template está errada.
        Assert.All(r.RespostasPrototipo.Where(x => x.Caminho == Caminho.Template), x => Assert.True(x.Correta, x.Texto));
        Assert.All(Experimento.Quadro2(r), t => Assert.True(t.Ok, $"{t.Teste}: {t.Obtido}"));
    }

    [Fact]
    public async Task RespostaErradaDoLlm_NaoViraTemplate()
    {
        // Com erro em toda chamada, nada vira template (o dado errado não é da entidade perguntada).
        var r = await Rodar(taxaErro: 1);
        Assert.Equal(0, r.TemplatesSalvos);
        Assert.Equal(150, r.Prototipo.ChamadasGeracao);
    }

    [Fact]
    public async Task Relatorio_TrazAsMetricasEOQuadro2()
    {
        var md = Experimento.Relatorio(await Rodar());
        Assert.Contains("| Chamadas ao LLM de geração |", md);
        Assert.Contains("Comparação cega (T06)", md);
        Assert.Contains("| T05 |", md);
        Assert.DoesNotContain("Reprovado", md);
    }

    [Fact]
    public async Task JuizLlm_LeAResposta_EAOrdemESorteada()
    {
        var p = new PerguntaTeste(1, "Qual a capital do Peru?", "capital", "Peru", "capital", "Lima");
        Assert.Equal("A", await new JuizLlm(new LlmSimulado((_, _) => "A")).Comparar(p, "Lima", "Cusco"));
        Assert.Equal("empate", await new JuizLlm(new LlmSimulado((_, _) => "EMPATE.")).Comparar(p, "Lima", "Lima"));
        Assert.Equal("B", await new JuizPorGabarito().Comparar(p, "A capital do Peru é Cusco.", "A capital do Peru é Lima."));
        Assert.True(Experimento.Correta("A capital do Peru é LIMA.", "Lima"));
        Assert.False(Experimento.Correta("A capital é Limasol.", "Lima"));
    }
}
