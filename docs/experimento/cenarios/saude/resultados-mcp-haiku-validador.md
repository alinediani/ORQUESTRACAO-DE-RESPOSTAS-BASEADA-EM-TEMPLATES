# Resultados do experimento — cenário saude

Gerado por `POST /api/experimento {"llm": "mcp", "cenario": "saude"}` · 22 perguntas da amostra `conjunto.json` (semente 2026; ver `analise.md` e `README.md`).

> **Com a trava (validador).** Cada template novo passou por um validador (`Orquestrador.SystemValidacao`) antes de ser salvo: os vereditos são os do Claude Haiku 5.5, gravados em `validacao-haiku.tsv`. Template recusado não é salvo, e as perguntas da chave vão ao LLM. O custo do validador usa o preço do Haiku 5.5 e entra no custo do protótipo.

> **Segundo fluxo, com normalizador.** Antes do parser, cada pergunta passou por um normalizador (`Orquestrador.SystemNormalizacao`): as saídas são as do Claude Haiku 5.5, gravadas em `normalizadas-haiku.tsv` (geradas por subagentes do Claude Code com o modelo Haiku, um por cenário, com as 30 perguntas no mesmo pedido). A normalização só decide a rota; o LLM de geração recebe a pergunta original. O custo do normalizador usa o preço do Haiku 5.5 (US$ 0,10 / 0,50 por milhão de tokens) e entra no custo do protótipo.

> **LLM real via MCP.** O LLM de geração foi o Claude (Opus 5.5) conectado ao /mcp do protótipo; cada chamada virou um turno pendente respondido pela ferramenta `responder`, sem consultar o gabarito. O protótipo e a linha de base usam o mesmo system prompt de geração (resposta em uma frase). Tokens estimados pelo tamanho do texto (≈ 4 caracteres por token), custo pelo preço do Claude Opus 5.5 (US$ 4 / US$ 20 por milhão). A correção automática procura o gabarito da base na resposta (palavra exata; gabarito numérico, pelo valor).

## Protótipo × linha de base

| Métrica | Protótipo (templates) | Linha de base (LLM puro) | Redução |
|---|---:|---:|---:|
| Chamadas ao LLM de geração | 22 | 22 | 0,0% |
| Chamadas ao LLM do parser | 0 | — | |
| Chamadas ao normalizador (Claude Haiku 5.5) | 22 (2.580 tokens, US$ 0,0003) | — | |
| Chamadas ao validador (Claude Haiku 5.5) | 6 (6 recusados; 1.750 tokens, US$ 0,0002) | — | |
| Taxa de acerto de template | 0,0% (0) | — | |
| Tokens | 6.461 | 2.131 | -203,2% |
| Custo estimado | US$ 0,02 | US$ 0,02 | -2,5% |
| Respostas corretas (gabarito) | não se aplica | não se aplica | |

**Sem gabarito curto:** as respostas são orientações livres. A qualidade é avaliada uma a uma (README do cenário) e na planilha cega `avaliacao-humana.csv`; o feedback simulado não foi aplicado.

## Por família

| Família | Perguntas | Chaves distintas (T01) | Respondidas pelo template |
|---|---:|---:|---:|
| cardiologia | 1 | 1 | 0 (0,0%) |
| coloproctologia | 1 | 1 | 0 (0,0%) |
| dermatologia | 4 | 4 | 0 (0,0%) |
| ginecologia e obstetricia | 2 | 2 | 0 (0,0%) |
| oftalmologia | 2 | 2 | 0 (0,0%) |
| otorrinolaringologia | 2 | 1 | 0 (0,0%) |
| psiquiatria | 4 | 2 | 0 (0,0%) |
| reumatologia | 4 | 2 | 0 (0,0%) |
| urologia | 2 | 1 | 0 (0,0%) |

Templates salvos na base chave-valor: 0.

## As respostas

| # | Pergunta | Protótipo | Caminho | Linha de base |
|---:|---|---|---|---|
| 1 | Reumatologista trata de artrose? | Sim, o reumatologista trata artrose, assim como o ortopedista, conforme o caso. ❌ | llm | Sim, o reumatologista trata artrose, assim como o ortopedista, conforme o caso. ❌ |
| 2 | Labirintite tem cura? | A labirintite tem cura na maioria dos casos, com o tratamento da causa orientado por um otorrinolaringologista. ❌ | llm | A labirintite tem cura na maioria dos casos, com o tratamento da causa orientado por um otorrinolaringologista. ❌ |
| 3 | Qual o melhor tratamento para Mioma? | O melhor tratamento para mioma depende do tamanho, da localização e dos sintomas, e vai do acompanhamento a remédios ou cirurgia, definido com o ginecologista. ❌ | llm | O melhor tratamento para mioma depende do tamanho, da localização e dos sintomas, e vai do acompanhamento a remédios ou cirurgia, definido com o ginecologista. ❌ |
| 4 | Gostaria de saber sobre tratamento de queda de cabelo. | O tratamento da queda de cabelo depende da causa e pode incluir minoxidil, correção de deficiências nutricionais e outros remédios, sempre com a avaliação de um dermatologista. ❌ | llm | O tratamento da queda de cabelo depende da causa e pode incluir minoxidil, correção de deficiências nutricionais e outros remédios, sempre com a avaliação de um dermatologista. ❌ |
| 5 | Estou com a pressão alta. O que fazer? | Com a pressão alta, sente-se, repouse e meça de novo, e procure uma emergência se ela estiver muito alta ou houver dor no peito, falta de ar ou alteração na fala; para o controle contínuo, consulte um cardiologista. ❌ | llm | Com a pressão alta, sente-se, repouse e meça de novo, e procure uma emergência se ela estiver muito alta ou houver dor no peito, falta de ar ou alteração na fala; para o controle contínuo, consulte um cardiologista. ❌ |
| 6 | ARTROSE TEM CURA? | A artrose não tem cura, mas exercícios, controle do peso e remédios aliviam a dor e retardam a progressão, com acompanhamento de um reumatologista ou ortopedista. ❌ | llm | A artrose não tem cura, mas exercícios, controle do peso e remédios aliviam a dor e retardam a progressão, com acompanhamento de um reumatologista ou ortopedista. ❌ |
| 7 | Estou com queda de cabelo, o que fazer? | Com queda de cabelo, o ideal é procurar um dermatologista para investigar a causa (estresse, anemia, tireoide, hormônios) antes de começar o tratamento. ❌ | llm | Com queda de cabelo, o ideal é procurar um dermatologista para investigar a causa (estresse, anemia, tireoide, hormônios) antes de começar o tratamento. ❌ |
| 8 | Gostaria de saber sobre tratamento da endometriose. | O tratamento da endometriose pode ser hormonal ou cirúrgico, conforme os sintomas e o desejo de engravidar, e deve ser definido com um ginecologista. ❌ | llm | O tratamento da endometriose pode ser hormonal ou cirúrgico, conforme os sintomas e o desejo de engravidar, e deve ser definido com um ginecologista. ❌ |
| 9 | Depressão tem cura? | A depressão tem tratamento eficaz com psicoterapia e, quando indicado, antidepressivos, e muitas pessoas alcançam a remissão completa com o acompanhamento de um psiquiatra. ❌ | llm | A depressão tem tratamento eficaz com psicoterapia e, quando indicado, antidepressivos, e muitas pessoas alcançam a remissão completa com o acompanhamento de um psiquiatra. ❌ |
| 10 | Ejaculação precoce tem cura?? | A ejaculação precoce tem tratamento eficaz, com terapia comportamental e medicamentos, orientado por um urologista ou sexólogo. ❌ | llm | A ejaculação precoce tem tratamento eficaz, com terapia comportamental e medicamentos, orientado por um urologista ou sexólogo. ❌ |
| 11 | A ejaculação precoce tem cura? | Sim, a ejaculação precoce tem tratamento eficaz, com técnicas comportamentais e medicamentos orientados por um urologista. ❌ | llm | Sim, a ejaculação precoce tem tratamento eficaz, com técnicas comportamentais e medicamentos orientados por um urologista. ❌ |
| 12 | Qual o melhor tratamento para queda de cabelo? | O melhor tratamento para queda de cabelo depende da causa, e o dermatologista pode indicar minoxidil, finasterida (em homens) ou a correção de deficiências. ❌ | llm | O melhor tratamento para queda de cabelo depende da causa, e o dermatologista pode indicar minoxidil, finasterida (em homens) ou a correção de deficiências. ❌ |
| 13 | O que é miopia? | A miopia é um erro de refração em que se enxerga mal de longe, porque a imagem se forma antes da retina, e é corrigida com óculos, lentes ou cirurgia indicados pelo oftalmologista. ❌ | llm | A miopia é um erro de refração em que se enxerga mal de longe, porque a imagem se forma antes da retina, e é corrigida com óculos, lentes ou cirurgia indicados pelo oftalmologista. ❌ |
| 14 | A depressão tem cura? | A depressão tem tratamento e, na maioria dos casos, remissão com psicoterapia e medicamentos acompanhados por um psiquiatra. ❌ | llm | A depressão tem tratamento e, na maioria dos casos, remissão com psicoterapia e medicamentos acompanhados por um psiquiatra. ❌ |
| 15 | ZUMBIDO TEM CURA? | O zumbido nem sempre tem cura, mas costuma melhorar quando a causa é tratada, e o otorrinolaringologista pode indicar terapias que reduzem o incômodo. ❌ | llm | O zumbido nem sempre tem cura, mas costuma melhorar quando a causa é tratada, e o otorrinolaringologista pode indicar terapias que reduzem o incômodo. ❌ |
| 16 | O que é TDAH? | O TDAH (transtorno do déficit de atenção com hiperatividade) é um transtorno do neurodesenvolvimento marcado por desatenção, hiperatividade e impulsividade, diagnosticado e tratado por psiquiatra ou neurologista. ❌ | llm | O TDAH (transtorno do déficit de atenção com hiperatividade) é um transtorno do neurodesenvolvimento marcado por desatenção, hiperatividade e impulsividade, diagnosticado e tratado por psiquiatra ou neurologista. ❌ |
| 17 | Ansiedade tem cura? | A ansiedade tem tratamento eficaz com psicoterapia e, quando necessário, medicamentos, e muitas pessoas ficam sem sintomas com o acompanhamento de um psiquiatra ou psicólogo. ❌ | llm | A ansiedade tem tratamento eficaz com psicoterapia e, quando necessário, medicamentos, e muitas pessoas ficam sem sintomas com o acompanhamento de um psiquiatra ou psicólogo. ❌ |
| 18 | O REUMATOLOGISTA TRATA DE ARTROSE? | Sim, o reumatologista trata artrose, assim como o ortopedista. ❌ | llm | Sim, o reumatologista trata artrose, assim como o ortopedista. ❌ |
| 19 | Fibromialgia tem cura? | A fibromialgia não tem cura, mas exercícios, sono adequado, psicoterapia e medicamentos controlam bem os sintomas, com o acompanhamento de um reumatologista. ❌ | llm | A fibromialgia não tem cura, mas exercícios, sono adequado, psicoterapia e medicamentos controlam bem os sintomas, com o acompanhamento de um reumatologista. ❌ |
| 20 | Glaucoma tem cura? | O glaucoma não tem cura, mas colírios, laser ou cirurgia controlam a pressão do olho e evitam a perda de visão, com o acompanhamento de um oftalmologista. ❌ | llm | O glaucoma não tem cura, mas colírios, laser ou cirurgia controlam a pressão do olho e evitam a perda de visão, com o acompanhamento de um oftalmologista. ❌ |
| 21 | Fissura anal tem cura? | A fissura anal tem cura na maioria dos casos, com pomadas, fibras e banhos de assento ou, se for crônica, cirurgia indicada pelo proctologista. ❌ | llm | A fissura anal tem cura na maioria dos casos, com pomadas, fibras e banhos de assento ou, se for crônica, cirurgia indicada pelo proctologista. ❌ |
| 22 | Vitiligo tem cura? | O vitiligo não tem cura, mas cremes, fototerapia e outros remédios podem repigmentar a pele, com o acompanhamento de um dermatologista. ❌ | llm | O vitiligo não tem cura, mas cremes, fototerapia e outros remédios podem repigmentar a pele, com o acompanhamento de um dermatologista. ❌ |
