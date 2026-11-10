# Os cenários do experimento: onde o orquestrador por templates encaixa

O experimento principal (`docs/experimento/`) usou perguntas geradas sobre uma base de fatos controlada: o cenário
ideal para o protótipo. Para saber como ele se sai fora desse cenário, quatro bases públicas, uma por tipo de pergunta,
passaram pelo mesmo protótipo e pela mesma linha de base. O LLM de geração foi o Claude (Opus 5.5), conectado ao `/mcp`.

| Cenário | Base (licença) | Perguntas analisadas | Teto de reaproveitamento¹ | Amostra | Chamadas ao LLM (protótipo × base) | Pelo template | Qualidade do template |
|---|---|---:|---:|---:|---:|---:|---|
| Factual controlado | base própria (`dados/fatos.json`) | 150 | 96% | 150 | 6 × 150 | 144 (96%) | Dado 100% correto; 33 de 144 com erro de forma (artigo herdado) |
| [Matemática](gsm8k/README.md) | GSM8K-pt (MIT) | 1.318 | 0% | 30 | 30 × 30 | 0 | — |
| [Factual real](mkqa/README.md) | MKQA (CC BY-SA 3.0) | 10.000 | 0,2% | 30 | 30 × 30 | 0 | — |
| [Saúde](saude/README.md) | SaudeBR-QA (CC BY 4.0) | 17.747 | 0,1% | 22² | **6 × 22** | **16 (73%)** | **9 de 16 erradas ou absurdas** |
| [Textos](squad/README.md) | SQuAD-pt (CC BY-SA 4.0) | 9.808 | 0,01% | 30 | 30 × 30 | 0 | — |
| [Quiz, tradução fiel](quiz/README.md) | SimpleQuestions/Wikidata (CC BY 3.0) | 300 | 30,0% | 30 | 26 × 30 | 4 | Dado certo nas 4; corretas 17 × 15 |
| [Quiz, simplificada](quiz/README.md) | idem, com normalizador | 300 | 79,3% | 30 | **14 × 30** (+30 do normalizador) | **16** | Dado certo nas 16; corretas **21 × 15**; erros de forma |

¹ Perguntas com entidade da fonte cuja chave já tinha aparecido: o máximo que o template poderia responder.
² O melhor caso: todas as perguntas do corpus cuja chave se repete.

## O que os cenários mostram

O protótipo só funciona quando **três condições** valem ao mesmo tempo:

1. **A estrutura se repete.** A mesma pergunta volta com outra entidade. Em perguntas espontâneas (MKQA, saúde, SQuAD) e
   em problemas de matemática, a estrutura quase nunca volta: o teto de reaproveitamento fica entre 0% e 0,2%. A
   intenção se repete ("quem canta", "tem cura", "em que ano"), mas a frase e o assunto mudam.
2. **O dado é buscável numa fonte chave-valor.** Na matemática ele é calculado; no SQuAD, está num texto livre; no MKQA,
   está num mundo aberto que nenhuma fonte pequena cobre.
3. **Tudo o que varia na resposta vem da fonte.** Esta condição só apareceu no cenário de saúde, o único em que o
   template foi usado. "Tem cura na maioria dos casos" e "é um erro de refração" são conteúdo, não forma, e o template os
   congelou: o protótipo cortou 73% das chamadas e entregou informação clínica errada em 9 de 16 respostas.

O quiz (SimpleQuestions, com a fonte tirada do Wikidata) é o primeiro cenário real que atende às três condições, mas só
quando as perguntas são padronizadas antes do parser. Com a tradução fiel, as pessoas escrevem a mesma pergunta de muitos
jeitos, e só 4 de 30 saíram do template. Com a simplificada, foram 16 de 30, com 53% menos chamadas de geração e mais
acertos que a linha de base (21 contra 15), porque o dado do Wikidata corrigiu erros do LLM. A simplificação, porém, é uma
chamada a mais por pergunta: com o mesmo modelo da geração, ela custa mais do que economiza; com um modelo pequeno (Haiku),
preserva cerca de 95% da economia.

O cenário factual controlado atende às três, e por isso obtém 96% de reaproveitamento com o dado sempre correto. Na
prática, esse perfil é o de **atendimento com dados de sistema**: perguntas frequentes e repetitivas cuja resposta
inteira é um dado de um cadastro ("Qual o horário da unidade X?", "Qual o status do pedido Y?", "Qual especialista trata
Z?"). Fora desse perfil, o ganho é nulo (matemática, busca aberta, textos) ou perigoso (saúde).

## Segundo fluxo: normalizador antes do parser (cenários "b")

No segundo fluxo, cada pergunta passa por um LLM normalizador (`Orquestrador.SystemNormalizacao`) antes do parser. A
normalização só decide a rota: a geração continua recebendo a pergunta original. O normalizador foi o **Claude Haiku**,
por meio de subagentes do Claude Code com o modelo Haiku, um por cenário. As saídas estão gravadas em
`normalizadas-haiku.tsv`, e o custo usa o preço do Haiku 5.5. Os resultados ficam em `resultados-mcp-haiku.md/.json` e
`experimento-mcp-haiku.db`, na pasta de cada cenário.

| Cenário | Geração (protótipo × base) | Normalizador | Pelo template | Corretas (protótipo × base) | Custo total do protótipo × base |
|---|---:|---:|---:|---:|---:|
| 1b GSM8K | 30 × 30 | 30 (US$ 0,0011) | 0 | 30 × 30 | +6% |
| 2b MKQA | 30 × 30 | 30 (US$ 0,0005) | 0 | 20 × 20 | +3% |
| 3b Saúde | 6 × 22 | 22 (US$ 0,0003) | 16 (os mesmos de antes) | — | −71% (com os mesmos 9 erros) |
| 4b SQuAD | 30 × 30 | 30 (US$ 0,0011) | 0 | 29 × 29 | +3% |
| 6c Quiz (fiel + Haiku) | 24 × 30 | 30 (US$ 0,0005) | 6 | 18 × 15 | **−17%** |

Onde a estrutura não se repete, o normalizador só acrescenta custo. Na saúde, as chaves do melhor caso já coincidiam, e
o resultado não mudou. No quiz, o Haiku com um prompt genérico padronizou bem menos que as formas canônicas por relação
(6 contra 16 pelo template), mas ainda reduziu o custo em 17% e melhorou o acerto. **Um normalizador genérico ajuda pouco:
o ganho vem de formas canônicas definidas para o domínio.**

> Limitação: o subagente normalizou as 30 perguntas de cada cenário num único pedido, e não uma chamada por pergunta.

```bash
curl -X POST localhost:5190/api/experimento -d '{"llm":"mcp","cenario":"quiz-fiel","normalizador":"haiku"}'
```

## Reprodução

Cada pasta tem o arquivo da base, `analise.md` (análise estrutural sem LLM), `conjunto.json` (a amostra),
`resultados-mcp.md/.json`, a base SQLite `experimento-mcp.db` e a planilha cega `avaliacao-humana.csv`. Os comandos estão
no README de cada cenário.
