# Cenário 3 — Atendimento em saúde (SaudeBR-QA)

**Hipótese:** neste cenário, o reaproveitamento da *forma* da resposta é arriscado. O conteúdo clínico depende da
condição e do paciente, e a fonte só fornece parte dele.

## Base

- **SaudeBR-QA** (Carlos Santos, 2025, *Corpus de Perguntas e Respostas em Saúde (Português - Brasil)*,
  [CarlHenry670/SaudeBR-QA](https://github.com/CarlHenry670/SaudeBR-QA)). São 23.382 pares de perguntas reais de
  pacientes e respostas de profissionais, em 72 especialidades. Cada pergunta tem uma linha por resposta, o que dá
  17.747 perguntas distintas.
- **Licença:** CC BY 4.0. O arquivo `saudebr-qa.csv` é o `corpus_perguntas_respostas.csv` original, baixado em 09/11/2026.
- **Gabarito:** não há. As respostas são orientações livres, então a correção automática não se aplica e a qualidade
  foi avaliada uma a uma (abaixo).

## A fonte do cenário

A fonte padrão do protótipo (países e elementos) não tem nada de saúde. Por isso a fonte do cenário (`fatos.json`)
foi gerada do próprio corpus com `--cenario saude --fonte`:

- a lista `condicoes.txt` traz 54 condições comuns, com artigo e apelidos ("queda de cabelo" é apelido de alopecia);
- entram as 45 condições citadas em 20 ou mais perguntas;
- o atributo `especialista` de cada condição é o profissional da especialidade mais frequente entre as perguntas que a
  citam (artrose leva a reumatologista; varizes, a cirurgião vascular). É o "dado de sistema" que a fonte fornece.

## Como reproduzir

```bash
dotnet run --project src/Orquestracao.Experimento -- --cenario saude --fonte --analisar --preparar --perguntas 30
curl -X POST localhost:5190/api/experimento -d '{"llm":"mcp","cenario":"saude"}'
```

## Resultados

**Análise estrutural** ([analise.md](analise.md)), sobre as 17.747 perguntas, com a fonte de 45 condições:

| Medida | Valor |
|---|---:|
| Perguntas com uma condição da fonte | 2.769 (15,6%) |
| Chaves distintas | 17.723 |
| **Teto de reaproveitamento** | **16 perguntas (0,1%)** |
| A chave que mais se repete | `{condicao} tem cura`, 12 vezes |

**Experimento real, no melhor caso** ([resultados-mcp.md](resultados-mcp.md)). A amostra junta **todas** as 22
perguntas do corpus cuja chave se repete (7 chaves); são as únicas que podem passar pelo caminho do template. O LLM foi o
Claude (Opus 5.5) via MCP. Não houve feedback simulado: sem gabarito, não há como saber se a resposta estava boa, e os
templates ficaram como um usuário que não avalia os deixaria.

| Métrica | Protótipo | Linha de base |
|---|---:|---:|
| Chamadas ao LLM de geração | **6** | 22 |
| Respostas pelo template | **16 (72,7%)** | — |
| Templates salvos | 6 | — |

**A qualidade das 16 respostas do template**, conferidas uma a uma:

| Situação | Perguntas | Exemplo |
|---|---|---|
| Adequada (a mesma pergunta, ou condições em que a frase vale) | 18, 21 | "O reumatologista trata de artrose?" (a mesma pergunta da 1); "Fissura anal tem cura?" |
| Aceitável, mas genérica | 9, 10, 11, 14, 17 | "A depressão tem cura na maioria dos casos…": a resposta original, que fala de remissão com tratamento, era mais cuidadosa |
| **Errada: diz que tem cura o que não tem** | 6, 15, 19, 20, 22 | "O glaucoma tem cura na maioria dos casos…"; o mesmo vale para artrose, fibromialgia, vitiligo e zumbido |
| **Absurda: o conteúdo de outra condição** | 7, 8, 12, 16 | "O TDAH é um erro de refração em que se enxerga mal de longe…"; "Com queda de cabelo… procure uma emergência se ela estiver muito alta" |

São **9 respostas erradas ou absurdas em 16** (56%). A linha de base, com uma resposta do LLM por pergunta, não errou
nenhuma dessas.

## Conclusões

1. **A economia aparece, mas a qualidade desaba.** No único recorte em que o protótipo reaproveita (o melhor caso do
   corpus inteiro), ele cortou 73% das chamadas. O custo foi entregar informação clínica errada em mais da metade das
   respostas de template.
2. **A fonte fornece só parte da resposta.** O template troca a condição e o especialista, que vêm da fonte. O resto da
   frase, "tem cura na maioria dos casos" ou "é um erro de refração", é **conteúdo**, não forma, e varia de uma condição
   para outra. O protótipo supõe que tudo o que não vem da fonte é forma. Em saúde isso é falso, e o template congela o
   conteúdo da primeira resposta.
3. **A chave não captura o que a pergunta pede.** `que {condicao}` ("O que é…?") juntou miopia e TDAH na mesma chave,
   sem nenhum atributo de definição na fonte. Faltam dois controles: o template deveria exigir que todo o conteúdo variável
   venha da fonte, e a chave deveria levar o atributo pedido.
4. **Os apelidos não viram slot (limitação do extrator).** "Queda de cabelo" é apelido de alopecia, mas o extrator só troca
   o nome e as formas com artigo. A expressão ficou dentro do template e voltou em outras perguntas, como na 8
   ("O tratamento da queda de cabelo… com a avaliação de um ginecologista", para a endometriose).
5. **O feedback não protege a primeira vítima.** Mesmo com o feedback ligado, o template só cairia depois de 3 avaliações
   ruins seguidas (peso 0,5 → 0,35 → 0,245 → 0,17). Em saúde, cada uma dessas avaliações é um paciente que recebeu
   informação errada. Para este domínio, o template precisaria de validação humana **antes** do uso, e não depois.
6. **Ainda há lugar para o protótipo em saúde:** em perguntas cuja resposta inteira é dado de sistema ("Qual especialista
   trata X?", "Qual o horário da UBS?", "Meu exame ficou pronto?"). Esse tipo de pergunta quase não aparece no corpus, que é
   de dúvidas clínicas; ele é típico de um atendimento com sistema de agendamento.

> **Viés:** o Claude respondeu às perguntas e também avaliou as respostas de template. A avaliação cega humana está em
> `avaliacao-humana.csv` (a chave em `avaliacao-humana-chave.csv`). A lista de condições foi escolhida pelo Claude; a
> especialidade de cada uma veio do corpus.
