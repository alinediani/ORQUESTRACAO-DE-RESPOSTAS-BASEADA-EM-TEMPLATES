# Avaliação humana (comparação cega, T06)

**Objetivo:** saber se as respostas do protótipo (montadas por template, com o dado da base de fatos) têm qualidade equivalente às do LLM puro.

## Os arquivos

| Arquivo | Para quem | O que tem |
|---|---|---|
| `avaliacao-humana.csv` | os avaliadores | as 150 perguntas, cada uma com duas respostas (A e B) em ordem sorteada, sem dizer de onde veio cada uma |
| `avaliacao-humana-chave.csv` | só quem organiza | qual resposta é do protótipo e qual é da linha de base, a família e o gabarito. **Não entregue aos avaliadores.** |

Os dois arquivos usam `;` como separador e abrem direto no Excel ou no Google Sheets.

## Como avaliar

Para cada linha, preencha:

- **melhor:** `A`, `B` ou `empate`. Julgue correção, clareza e se a frase está bem escrita (concordância, pontuação). Se as duas forem equivalentes, marque `empate`.
- **a_correta** e **b_correta:** `s` se a informação está certa, `n` se está errada.
- **comentario:** opcional (por exemplo, "artigo errado", "mais completa").

## Sugestões

- **Avaliadores:** pelo menos 3, cada um sozinho, sem conversar sobre as respostas.
- **Amostra:** se 150 linhas for muito, sorteie uma amostra, por exemplo 50 linhas. Inclua os pares diferentes, porque os idênticos tendem ao empate.
- **Desembaralhar:** junte as planilhas pela coluna `id` com `avaliacao-humana-chave.csv` e conte as vitórias de cada estratégia. Calcule também a concordância entre avaliadores, por exemplo pelo kappa de Fleiss.
- **Registro:** o resultado vai para o Quadro 2 (T06) e para a seção 5.1 do TCC.
