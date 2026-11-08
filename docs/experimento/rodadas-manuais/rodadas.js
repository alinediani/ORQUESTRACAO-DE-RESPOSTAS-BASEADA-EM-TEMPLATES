// Rodadas "manuais" no painel do orquestrador, pelo navegador (Playwright): digita cada pergunta, espera a
// resposta (com o motor MCP, as estruturas novas esperam o Claude Code responder) e dá 👍/👎.
// Critério do avaliador: 👍 se o dado confere com a base de fatos e o artigo da moeda está certo; senão 👎.
// Uso: node rodadas.js <repo> <rodadas> <perguntas_por_rodada> <saida.jsonl>
const [repo, nRodadas = '4', nPorRodada = '15', saida = 'rodadas.jsonl'] = process.argv.slice(2);
const { chromium } = require(`${repo}/tests/Orquestracao.E2E.Tests/bin/Debug/net10.0/.playwright/package`);
const fs = require('fs');
const fatos = JSON.parse(fs.readFileSync(`${repo}/dados/fatos.json`, 'utf8')).entidades;

let semente = 20261108;
const rnd = () => ((semente = (semente * 1103515245 + 12345) % 2147483648) / 2147483648);
const pick = (a) => a[Math.floor(rnd() * a.length)];
const forma = (e, tipo) => {
  const de = { a: 'da', o: 'do', os: 'dos', as: 'das' }[e.artigo] ?? 'de';
  const em = { a: 'na', o: 'no', os: 'nos', as: 'nas' }[e.artigo] ?? 'em';
  const o = e.artigo ? `${e.artigo} ${e.nome}` : e.nome;
  return { de: `${de} ${e.nome}`, em: `${em} ${e.nome}`, o }[tipo];
};
const familias = [
  { nome: 'capital', tipo: 'pais', attr: 'capital', frases: ['Qual é a capital {de}?', 'Me diga a capital {de}.'] },
  { nome: 'moeda', tipo: 'pais', attr: 'moeda', frases: ['Qual a moeda {de}?', 'Qual é a moeda {de}?'] },
  { nome: 'idioma', tipo: 'pais', attr: 'idioma', frases: ['Que língua se fala {em}?'] },
  { nome: 'continente', tipo: 'pais', attr: 'continente', frases: ['Em que continente fica {o}?'] },
  { nome: 'simbolo', tipo: 'elemento', attr: 'simbolo', frases: ['Qual o símbolo químico {de}?'] },
  { nome: 'numero', tipo: 'elemento', attr: 'numero_atomico', frases: ['Qual é o número atômico {de}?'] },
];
const foraDaBase = ['Qual é a capital do Acre?', 'Qual a moeda de Wakanda?', 'Quem pintou a Mona Lisa?'];
const femininas = ['coroa', 'libra', 'naira', 'rupia'];
const sem = (s) => s.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();

function avaliar(q, texto) {
  if (!q.gabarito) return null; // fora da base: o avaliador não julga
  const temDado = sem(texto).includes(sem(q.gabarito));
  let artigoOk = true;
  if (q.familia === 'moeda') {
    const art = femininas.some((f) => q.gabarito.startsWith(f)) ? 'a' : 'o';
    artigoOk = new RegExp(`\\b${art} ${q.gabarito.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`, 'i').test(texto);
  }
  return temDado && artigoOk;
}

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1400, height: 1000 } });
  await page.goto('http://localhost:5190/orquestrador.html');
  await page.waitForSelector('body[data-pronto=true]');
  await page.selectOption('#motor', 'mcp');
  const out = fs.createWriteStream(saida, { flags: 'a' });
  for (let r = 1; r <= +nRodadas; r++) {
    for (let i = 0; i < +nPorRodada; i++) {
      let q;
      if (i === +nPorRodada - 1) q = { pergunta: foraDaBase[(r - 1) % foraDaBase.length], familia: 'fora', gabarito: null };
      else {
        const f = pick(familias);
        const e = pick(fatos.filter((x) => x.tipo === f.tipo));
        q = { pergunta: pick(f.frases).replace(/\{(de|em|o)\}/, (_, t) => forma(e, t)), familia: f.nome, gabarito: e.atributos[f.attr] };
      }
      await page.fill('#pergunta', q.pergunta);
      await page.click('#form-pergunta button[type=submit]');
      // A resposta chegou quando o texto não é mais o aviso de espera e corresponde a esta pergunta.
      await page.waitForFunction(
        () => { const t = document.querySelector('#r-texto')?.textContent ?? ''; return t && !t.includes('esperando o Claude Code') && document.querySelector('#r-chave')?.textContent; },
        null, { timeout: 30 * 60 * 1000, polling: 300 });
      await page.waitForTimeout(150);
      const lido = await page.evaluate(() => ({
        caminho: document.querySelector('#r-caminho').textContent, texto: document.querySelector('#r-texto').textContent,
        chave: document.querySelector('#r-chave').textContent, template: document.querySelector('#r-template').textContent,
        peso: document.querySelector('#r-peso').textContent,
      }));
      const nota = avaliar(q, lido.texto);
      let feedback = null;
      if (nota !== null && !(await page.isDisabled('#bom'))) {
        await page.click(nota ? '#bom' : '#ruim');
        await page.waitForFunction(() => document.querySelector('#r-feedback').textContent.length > 0);
        feedback = await page.textContent('#r-feedback');
      }
      const linha = { rodada: r, n: i + 1, ...q, ...lido, avaliacao: nota === null ? 'sem avaliação' : nota ? 'boa' : 'ruim', feedback };
      out.write(JSON.stringify(linha) + '\n');
      console.log(`[${r}.${i + 1}] ${lido.caminho.padEnd(18)} ${lido.texto}  → ${linha.avaliacao}${feedback ? ` (${feedback})` : ''}`);
      // Limpa o texto para a próxima espera não confundir com a resposta anterior.
      await page.evaluate(() => { document.querySelector('#r-texto').textContent = ''; document.querySelector('#r-chave').textContent = ''; });
    }
    await page.screenshot({ path: saida.replace('.jsonl', `-rodada${r}.png`), fullPage: true });
  }
  out.end();
  await browser.close();
  console.log('FIM');
})().catch((e) => { console.error('ERRO', e); process.exit(1); });
