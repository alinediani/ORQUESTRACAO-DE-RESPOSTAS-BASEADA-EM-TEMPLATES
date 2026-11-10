# Cenário 8 — Atendimento com banco de dados (MultiWOZ)

**Hipótese:** é o nicho que os outros cenários apontaram. Num atendimento, as perguntas pedem dados de um cadastro
(telefone, endereço, CEP, região, faixa de preço) e se repetem. A fonte do protótipo é o próprio banco do sistema, e não
um texto escrito por quem responde.

## Base

- **MultiWOZ 2.2** (Budzianowski et al., 2018; Zang et al., 2020), [budzianowski/multiwoz](https://github.com/budzianowski/multiwoz).
  São diálogos reais entre turistas e atendentes em Cambridge, com um banco de restaurantes (110), hotéis (33) e atrações
  (79) e a anotação de quais dados o usuário pede em cada fala.
- **Licença:** os dados são CC BY 4.0 (University of Cambridge); o repositório é MIT.
- **Arquivos:**
  - `restaurant_db.json`, `hotel_db.json` e `attraction_db.json`: o banco, baixado em 10/11/2026;
  - `pedidos.json`: as 477 falas do teste (1.000 diálogos) em que o usuário pede um ou mais atributos de uma entidade do
    banco, com o nome da entidade em discussão (o estado da conversa), os atributos pedidos e a tradução fiel para o
    português, feita por um subagente do Claude Code com o modelo Sonnet.
- **A fonte** é o banco, com os valores categóricos em português (região "centre" → "centro", faixa "cheap" → "barata").
  Ficam de fora os atributos desconhecidos ("?"), e restam 466 pedidos.
- **Gabarito:** os valores pedidos e o nome da entidade, porque o dado certo com o nome de outro estabelecimento é erro.
- **Linha de base justa:** o LLM recebe no prompt o registro do banco da entidade em discussão, como um atendimento que
  consulta o sistema. O protótipo recebe o mesmo registro quando chama o LLM.

## Variantes

- **Fiel:** a tradução fiel, só com o parser. A entidade precisa estar na frase.
- **Fiel + contexto:** a opção nova do fluxo, `entidadeDoContexto`. Quando a frase não traz a entidade ("Qual é o
  endereço?"), vale a que está em discussão na conversa.
- **Canônica:** a pergunta na forma canônica do atributo ("Qual é o telefone de X?"), como faria um sistema de atendimento
  que conhece as próprias intenções.
- **Canônica + trava:** com o validador Haiku (`../multiwoz-canonica/validacao-haiku.tsv`).

```bash
dotnet run --project src/Orquestracao.Experimento -- --cenario multiwoz-fiel --analisar --preparar --perguntas 30
curl -X POST localhost:5190/api/experimento -d '{"llm":"mcp","cenario":"multiwoz-fiel","contexto":"sim"}'
curl -X POST localhost:5190/api/experimento -d '{"llm":"mcp","cenario":"multiwoz-canonica","validador":"haiku"}'
```

## Resultados

**Análise estrutural** dos 466 pedidos (`../multiwoz-*/analise.md`):

| Variante | Só o parser | Com a entidade do contexto |
|---|---:|---:|
| Inglês original | 0,0% | 1,3% |
| Tradução fiel | 0,2% | 3,4% |
| **Formas canônicas** (78 chaves) | — | **83,3%** |

Só 69 dos 477 pedidos (14%) dizem o nome da entidade. O resto fala "disso", "deles" ou nada ("Qual é o endereço?"). As
mesmas perguntas são escritas de muitos jeitos, e várias pedem mais de um dado ("o endereço e o CEP").

**Experimento real**, com 30 pedidos sorteados e o Claude (Opus 5.5) como LLM via MCP, com o registro do banco no prompt:

| Variante | Chamadas de geração (protótipo × base) | Pelo template | Corretas (dado + nome) | Custo do protótipo |
|---|---:|---:|---:|---:|
| Fiel | 30 × 30 | 0 | 30 × 30 | igual |
| Fiel + contexto | 30 × 30 | 0 | 30 × 30 | igual |
| **Canônica** | **19 × 30** | **11** | **30 × 30** | **37% menor** |
| Canônica + trava | 19 × 30 | 11 | 30 × 30 | 35% menor |

Na amostra, as 30 perguntas caem em 19 chaves, e cada chave paga uma chamada. Com volume, a fração de chaves repetidas
tende aos 83% da análise.

**Dois bugs do extrator, achados e corrigidos neste cenário:**

1. **Nome com outra caixa.** Na resposta estava "La Mimosa"; na fonte, "la mimosa". O nome não virou slot e o template
   respondeu "O endereço do **La Mimosa** é 100 Mill Road…" para o Bedouin. O gabarito não pegava o erro, porque o
   endereço estava certo; por isso o nome passou a fazer parte do gabarito.
2. **Valor dentro do nome.** O tipo "hotel" foi trocado por slot dentro de "ashley hotel" ("O endereço do ashley {type}…").
   O validador recusou esse template e revelou o bug.

Os dois têm testes em `CenariosTests`.

**O que ainda falha é a forma:** 4 das 11 respostas de template herdam o artigo da primeira ("O telefone **da** gonville
hotel", vindo de "da all saints church"). O validador, desta vez, não reprovou artigos ligados ao nome, um critério
diferente do usado nos outros cenários.

## Conclusões

1. **É o nicho, e ele funciona com duas condições.** A entidade tem de vir do estado da conversa, e a pergunta tem de
   estar na forma canônica. Sem isso, nem o atendimento real se repete: as pessoas não dizem o nome e escrevem a mesma
   pergunta de muitos jeitos. Com as duas, o protótipo cortou 37% do custo na amostra, com a mesma correção da linha de
   base, e o teto na base inteira é de 83%.
2. **A forma canônica, aqui, não é um normalizador genérico, e sim a intenção que o sistema já conhece.** Num atendimento
   com cadastro, o sistema sabe qual dado está sendo pedido (é o que o MultiWOZ anota). O que falta para o protótipo é
   ligar essa intenção à chave.
3. **Num cadastro, o dado do template é sempre o do banco.** A resposta do template nunca alucina o dado, mas pode herdar
   o artigo ou, antes da correção, o nome da primeira entidade. A trava e o gênero dos valores na fonte são o caminho.

> **Viés:** o Claude respondeu com o registro do banco no prompt, e o Sonnet traduziu as falas. A fonte e o gabarito vêm do
> banco do MultiWOZ.
