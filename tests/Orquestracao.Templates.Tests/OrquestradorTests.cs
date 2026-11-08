using Orquestracao.Templates;
using static Orquestracao.Templates.Tests.Base;

namespace Orquestracao.Templates.Tests;

/// <summary>R02–R05 e R07, e os testes T02–T04 do TCC (Quadro 2).</summary>
public class OrquestradorTests
{
    [Fact]
    public async Task T02_EstruturaNova_ChamaOLlmUmaVez_ESalvaOTemplateComPesoInicial()
    {
        var orq = Orquestrador();
        var llm = Llm();
        var r = await orq.Responder("Qual é a capital da França?", llm);
        Assert.Equal((Caminho.Llm, "chave nova", 1), (r.Caminho, r.Motivo, llm.Chamadas));
        Assert.Equal("A capital da França é Paris.", r.Texto);
        Assert.True(r.TemplateSalvo);
        var t = orq.Store.Obter("capital {pais}")!;
        Assert.Equal(("A capital {de_entidade} é {capital}.", 0.5), (t.Template, t.Peso));
    }

    [Fact]
    public async Task T03_MesmaFamiliaDepois_RespondeSemChamarOLlm_ComODadoDaFonte()
    {
        var orq = Orquestrador();
        var llm = Llm();
        await orq.Responder("Qual é a capital da França?", llm);
        var r = await orq.Responder("Me diga a capital do Japão", llm);
        Assert.Equal((Caminho.Template, "A capital do Japão é Tóquio."), (r.Caminho, r.Texto));
        Assert.Equal((1, 0), (llm.Chamadas, r.ChamadasGeracao));
        Assert.Equal(1, orq.Store.Obter("capital {pais}")!.Usos);
        Assert.Equal(0, r.Custo);
    }

    [Fact]
    public async Task T04_FeedbackRuimRepetido_PesoCai_DescartaAbaixoDoLimiar_EVoltaAoLlm()
    {
        var orq = Orquestrador();
        var llm = Llm();
        await orq.Responder("Qual é a capital da França?", llm);
        Assert.Equal(0.35, orq.Feedback("capital {pais}", bom: false)!.Peso, 6);
        Assert.Equal(0.245, orq.Feedback("capital {pais}", bom: false)!.Peso, 6);
        Assert.Null(orq.Feedback("capital {pais}", bom: false)); // 0,1715 < 0,2: descartado
        Assert.Null(orq.Store.Obter("capital {pais}"));

        var r = await orq.Responder("Qual a capital do Peru?", llm);
        Assert.Equal((Caminho.Llm, "chave nova", 2), (r.Caminho, r.Motivo, llm.Chamadas));
        Assert.Equal(0.5, orq.Store.Obter("capital {pais}")!.Peso); // nasce de novo
        Assert.Equal((3, 1), (orq.Estatisticas.FeedbacksRuins, orq.Estatisticas.TemplatesDescartados));
    }

    [Fact]
    public async Task FeedbackBom_SobeOPeso_PorMediaMovel()
    {
        var orq = Orquestrador();
        await orq.Responder("Qual a moeda do Japão?", Llm());
        Assert.Equal(0.65, orq.Feedback("moeda {pais}", true)!.Peso, 6);
        Assert.Equal(0.755, orq.Feedback("moeda {pais}", true)!.Peso, 6);
        Assert.Equal(2, orq.Store.Obter("moeda {pais}")!.FeedbacksBons);
        Assert.Null(orq.Feedback("chave que nao existe", true));
    }

    [Fact]
    public async Task ConfiguracaoCalibravel_AlfaELimiar()
    {
        var orq = Orquestrador(cfg: new ConfiguracaoOrquestrador { PesoInicial = 0.8, Alfa = 0.5, Limiar = 0.5 });
        await orq.Responder("Qual a capital do Chile?", Llm());
        Assert.Equal(0.8, orq.Store.Obter("capital {pais}")!.Peso);
        Assert.Null(orq.Feedback("capital {pais}", false)); // 0,5·0 + 0,5·0,8 = 0,4 < 0,5: descartado no primeiro feedback ruim
        Assert.Null(orq.Store.Obter("capital {pais}"));
    }

    [Fact]
    public async Task ForcarLlm_DepoisDeFeedbackRuim_MesmoComTemplate()
    {
        var orq = Orquestrador();
        var llm = Llm();
        await orq.Responder("Qual é a capital da França?", llm);
        orq.Feedback("capital {pais}", false);
        var r = await orq.Responder("Qual é a capital da Itália?", llm, forcarLlm: true);
        Assert.Equal((Caminho.Llm, 2), (r.Caminho, llm.Chamadas));
        Assert.Equal(0.35, orq.Store.Obter("capital {pais}")!.Peso, 6); // o mesmo template: o feedback continua valendo
    }

    [Fact]
    public async Task FonteSemODado_VaiAoLlm()
    {
        var orq = Orquestrador();
        orq.Store.Salvar(new TemplateResposta { Chave = "capital {pais}", Template = "A capital {de_entidade} é {populacao}." });
        var r = await orq.Responder("Qual a capital da Grécia?", Llm());
        Assert.Equal((Caminho.Llm, "a fonte não tem o dado para preencher o template"), (r.Caminho, r.Motivo));
        Assert.Equal("A capital {de_entidade} é {capital}.", orq.Store.Obter("capital {pais}")!.Template); // reformulado
    }

    [Fact]
    public async Task PesoAbaixoDoLimiar_NaoEUsado()
    {
        var orq = Orquestrador();
        orq.Store.Salvar(new TemplateResposta { Chave = "capital {pais}", Template = "A capital {de_entidade} é {capital}.", Peso = 0.1 });
        Assert.Equal("peso abaixo do limiar", (await orq.Responder("Qual a capital da Suécia?", Llm())).Motivo);
    }

    [Fact]
    public async Task PerguntaSemEntidade_OuRespostaNaoReutilizavel_NaoViraTemplate()
    {
        var orq = Orquestrador();
        var r1 = await orq.Responder("Qual é o sentido da vida?", Llm());
        Assert.Equal(("sem entidade conhecida na pergunta", false), (r1.Motivo, r1.TemplateSalvo));
        var r2 = await orq.Responder("Fale sobre a França", Llm()); // o LLM simulado não traz nenhum dado da fonte
        Assert.Equal((Caminho.Llm, false, (string?)null), (r2.Caminho, r2.TemplateSalvo, r2.Template));
        Assert.Empty(orq.Store.Listar());
    }

    [Fact]
    public async Task R07_ContaChamadasTokensCustoELatencia()
    {
        var orq = Orquestrador();
        var llm = Llm(custo: 0.01);
        foreach (var p in new[] { "Qual a capital da França?", "Qual a capital do Japão?", "Qual a capital do Peru?", "Qual a moeda do Peru?" })
            await orq.Responder(p, llm);
        var s = orq.Estatisticas;
        Assert.Equal((4, 2, 2, 0), (s.Perguntas, s.AcertosTemplate, s.ChamadasGeracao, s.ChamadasParser));
        Assert.Equal(llm.Chamadas, s.ChamadasGeracao);
        Assert.Equal((120L, 30L, 0.02), (s.TokensEntrada, s.TokensSaida, Math.Round(s.Custo, 6)));
        Assert.Equal(0.5, s.TaxaAcertoTemplate);
        Assert.True(s.LatenciaMediaMs >= 0);
        Assert.Equal(2, s.TemplatesSalvos);
    }

    [Fact]
    public async Task Etapas_OCaminhoVerdeEORoxoDaFigura1()
    {
        var orq = Orquestrador();
        var vistas = new List<string>();
        orq.Etapa += e => vistas.Add($"{e.De}>{e.Para}");
        await orq.Responder("Qual a capital da França?", Llm());
        Assert.Equal(["ui>parser", "parser>motor", "motor>llm", "llm>extrai", "extrai>salva", "salva>entrega"], vistas);
        vistas.Clear();
        await orq.Responder("Qual a capital do Japão?", Llm());
        Assert.Equal(["ui>parser", "parser>motor", "motor>busca", "busca>fonte", "fonte>adapta", "adapta>entrega"], vistas);
        vistas.Clear();
        orq.Feedback("capital {pais}", true);
        Assert.Equal(["entrega>feedback", "feedback>fim"], vistas);
    }
}
