# A trava: validação do template antes de salvar

**O problema.** No cenário de saúde, o template de "Labirintite tem cura?" virou "{O_entidade} tem cura na maioria
dos casos…" e foi reaproveitado para glaucoma e vitiligo, que não têm cura. O extrator só transforma em slot o que vem da
fonte, e trata como forma todo o resto da frase. Nesse caso, o resto era conteúdo.

**A trava.** Quando nasce um template novo, um LLM pequeno, o validador, recebe a pergunta, a resposta e o template
(`Orquestrador.SystemValidacao`). Ele responde SIM só se o que a pergunta pede está num slot e se nenhum trecho fixo
depende da entidade. Template recusado não é salvo, e a chave fica marcada: as perguntas dela vão sempre ao LLM, sem
validar de novo. O custo é **uma chamada por chave**, e não por pergunta. A trava é uma opção do fluxo (`validador`), e os
resultados sem ela continuam valendo.

**O validador foi o Claude Haiku**, por meio de um subagente do Claude Code com o modelo Haiku. Ele avaliou os 27 templates
candidatos dos experimentos. Os vereditos estão em `validacao-haiku.tsv` (aqui e nas pastas dos cenários); o custo usa o
preço do Haiku 5.5.

```bash
curl -X POST localhost:5190/api/experimento -d '{"llm":"mcp","validador":"haiku"}'                       # o controlado
curl -X POST localhost:5190/api/experimento -d '{"llm":"mcp","cenario":"saude","validador":"haiku"}'
```

## O que o validador recusou

| Experimento | Aprovados | Recusados | Exemplos de recusa |
|---|---:|---:|---|
| Controlado | 5 | 1 | "A moeda {de_entidade} é **a** {moeda}.": o artigo só vale para moeda feminina |
| Saúde | 0 | 6 | "{O_entidade} **tem cura na maioria dos casos**…"; "{O_entidade} **é um erro de refração**…" |
| Quiz, tradução fiel | 1 | 10 | "{entidade} **(El rapto)** é um filme do {p495}."; "{entidade} é **dos** {p27}." |
| Quiz, simplificada | 1 | 3 | "{entidade} é **um** {p136}." |

O validador barrou os dois tipos de problema: o **conteúdo** congelado (saúde, "El rapto") e a **forma** que depende do
valor (o artigo da moeda, "dos", "um"). Ele aprovou "A capital {de_entidade} é {capital}." e "{Em_entidade} fala-se
{idioma}.", que ainda produzem 13 respostas com diferenças de forma ("A capital do Egito é Cairo", sem o artigo). A trava
não pega tudo.

## Resultados com a trava

| Experimento | Chamadas de geração (protótipo × base) | Pelo template | Corretas (protótipo × base) | Respostas de template com diferença de forma | Custo do protótipo × base |
|---|---:|---:|---:|---:|---:|
| Controlado, sem trava | 6 × 150 | 144 | 150 × 150 | 33 | −96% |
| **Controlado, com trava** | **31 × 150** | **119** | 150 × 150 | **13** | **−78%** |
| Saúde, sem trava | 6 × 22 | 16 | — | 9 erradas ou absurdas | −71% |
| **Saúde, com trava** | 22 × 22 | **0** | — | **nenhuma** | +1% |
| Quiz fiel, sem trava | 26 × 30 | 4 | 17 × 15 | 2 | −14% |
| **Quiz fiel, com trava** | 28 × 30 | 2 | 17 × 15 | 0 | −4% |
| Quiz simplificado, sem trava | 14 × 30 | 16 | 21 × 15 | quase todas | −54% |
| **Quiz simplificado, com trava** | 25 × 30 | 5 | 19 × 15 | **0** | −15% |

## Conclusões

1. **A trava troca economia por segurança, e a troca é a certa nos cenários perigosos.** Na saúde, o protótipo passa a
   se comportar como a linha de base, sem nenhuma resposta clínica errada. No controlado, ainda corta 78% do custo, e os
   erros de forma caem de 33 para 13.
2. **Onde a forma depende do valor (artigos, gênero), a trava recusa o template**, e o ganho cai. No quiz simplificado,
   só "{entidade} nasceu em {p19}" sobreviveu. Para recuperar esse ganho sem perder a segurança, falta guardar na fonte o
   gênero e o artigo dos valores, para que o slot inclua a forma certa ("do Canadá", "dos Estados Unidos").
3. **O custo da trava é desprezível:** uma chamada ao Haiku por chave nova, menos de US$ 0,0004 por experimento.

> **Limitação:** o subagente avaliou os 27 templates num único pedido, e não uma chamada por template. A trava também
> depende do julgamento do validador: ele aprovou templates que ainda erram o artigo em alguns valores (capital).
