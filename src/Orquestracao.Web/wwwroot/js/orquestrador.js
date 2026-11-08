/**
 * Painel do orquestrador por template (F6 do TCC): pergunta, o caminho seguido (template ou LLM), a chave,
 * o template e o peso, o feedback (👍/👎), a Figura 1 com o caminho destacado, as estatísticas (R07), os
 * templates da base chave-valor e o registro ao vivo (SSE /api/orquestrador/events).
 */
const mermaid = window.mermaid;
const $ = (sel) => document.querySelector(sel);
const FIGURA = 'orquestrador-por-template';
const EXEMPLOS = [
  'Qual é a capital da França?',
  'Qual é a capital do Japão?',
  'Qual a moeda da Argentina?',
  'Qual a moeda do Chile?',
  'Em que continente fica o Egito?',
  'Qual o símbolo químico do ferro?',
  'Qual o símbolo químico do ouro?',
];

mermaid.initialize({
  startOnLoad: false,
  theme: matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'default',
  flowchart: { htmlLabels: true },
});

async function api(caminho, opcoes = {}) {
  const res = await fetch(caminho, {
    ...opcoes,
    headers: { 'Content-Type': 'application/json', ...(opcoes.headers ?? {}) },
    body: opcoes.body === undefined ? undefined : JSON.stringify(opcoes.body),
  });
  const texto = await res.text();
  const json = texto ? JSON.parse(texto) : null;
  if (!res.ok) throw new Error(json?.error ?? `HTTP ${res.status}`);
  return json;
}

const pct = (x) => `${Math.round(x * 100)}%`;
const custo = (d) => (d >= 0.01 || d === 0 ? `US$ ${d.toFixed(2)}` : `US$ ${d.toFixed(4)}`).replace('.', ',');
// Arredonda meio para cima (0,245 → 0,25), apesar do ponto flutuante.
const peso = (p) => (p === null || p === undefined ? '—' : (Math.round(p * 100 + 1e-9) / 100).toFixed(2).replace('.', ','));

// ---- a pergunta e o resultado ------------------------------------------------

let ultimo = null;
let esperando = false;

function mostrarResultado(r) {
  ultimo = r;
  $('#resultado').hidden = false;
  const caminho = $('#r-caminho');
  caminho.textContent = r.caminho === 'template' ? 'Template · sem LLM' : 'LLM padrão';
  caminho.className = `selo caminho-${r.caminho}`;
  $('#r-motivo').textContent = r.motivo ?? (r.caminho === 'template' ? 'chave existe' : '');
  $('#r-texto').textContent = r.texto;
  $('#r-chave').textContent = r.chave;
  $('#r-template').textContent = r.template ?? (r.caminho === 'llm' ? 'não reutilizável (a resposta não traz dados da fonte)' : '—');
  $('#r-peso').textContent = peso(r.peso);
  $('#r-chamadas').textContent = `${r.chamadasGeracao} chamada${r.chamadasGeracao === 1 ? '' : 's'} de geração · ${r.chamadasParser} do parser · ${custo(r.custo)}`;
  $('#r-latencia').textContent = `${Math.round(r.latenciaMs)} ms`;
  $('#r-feedback').textContent = '';
  const temTemplate = Boolean(r.template);
  $('#bom').disabled = !temTemplate;
  $('#ruim').disabled = !temTemplate;
  void desenharFluxo(r.etapas ?? []);
}

async function perguntar(pergunta) {
  if (!pergunta.trim()) return;
  esperando = true;
  if (motor.value === 'mcp') {
    $('#resultado').hidden = false;
    $('#r-texto').textContent = 'Se a estrutura for nova, esperando o Claude Code responder pelo /mcp…';
  }
  try {
    mostrarResultado(await api('/api/orquestrador/perguntar', { method: 'POST', body: { pergunta, motor: $('#motor').value } }));
  } catch (err) {
    $('#resultado').hidden = false;
    $('#r-texto').textContent = `Não deu: ${err.message}`;
  } finally {
    esperando = false;
  }
  await atualizarEstado();
}

async function avaliar(bom) {
  if (!ultimo?.chave) return;
  const r = await api('/api/orquestrador/feedback', { method: 'POST', body: { chave: ultimo.chave, bom } });
  $('#r-feedback').textContent = r.descartado
    ? 'Template descartado: a próxima pergunta desta estrutura volta ao LLM.'
    : `Peso agora: ${peso(r.template.peso)}`;
  if (r.template) $('#r-peso').textContent = peso(r.template.peso);
  await atualizarEstado();
}

// ---- a Figura 1 com o caminho destacado ----------------------------------------

let figura = null;

async function carregarFigura() {
  try {
    const [codigo, arestas] = await Promise.all([
      fetch(`/api/d/${FIGURA}/diagram`).then((r) => (r.ok && r.status !== 204 ? r.text() : null)),
      fetch(`/api/d/${FIGURA}/edges`).then((r) => (r.ok ? r.json() : [])),
    ]);
    figura = codigo ? { codigo: codigo.replace(/^flowchart LR/, 'flowchart TD'), arestas } : null;
  } catch {
    figura = null;
  }
}

let desenho = 0;
async function desenharFluxo(etapas) {
  const alvo = $('#fluxo');
  if (!figura) {
    alvo.textContent = 'O diagrama da Figura 1 não existe neste servidor.';
    return;
  }
  const verde = etapas.some((e) => e.de === 'motor' && e.para === 'busca');
  const nos = new Set(etapas.flatMap((e) => [e.de, e.para]));
  const indices = figura.arestas.map((a, i) => (etapas.some((e) => e.de === a.from && e.para === a.to) ? i : -1)).filter((i) => i >= 0);
  const cor = verde ? '#1f9d55' : '#7c3aed';
  const linhas = [figura.codigo];
  if (nos.size) {
    linhas.push(`    classDef caminho stroke:${cor},stroke-width:3px`);
    linhas.push(`    class ${[...nos].join(',')} caminho`);
  }
  if (indices.length) linhas.push(`    linkStyle ${indices.join(',')} stroke:${cor},stroke-width:3px`);
  const seq = ++desenho;
  try {
    const { svg } = await mermaid.render(`fluxo-${seq}`, linhas.join('\n'));
    if (seq === desenho) {
      alvo.innerHTML = svg;
      alvo.dataset.caminho = etapas.length ? (verde ? 'template' : 'llm') : '';
    }
  } catch (err) {
    alvo.textContent = `Erro ao desenhar: ${err.message}`;
  }
}

// ---- estatísticas e templates -----------------------------------------------------

function numero(valor, rotulo, id) {
  const d = Object.assign(document.createElement('div'), { className: 'numero' });
  d.dataset.id = id;
  d.append(Object.assign(document.createElement('b'), { textContent: valor }), Object.assign(document.createElement('span'), { textContent: rotulo }));
  return d;
}

async function atualizarEstado() {
  const e = await api('/api/orquestrador');
  const s = e.estatisticas;
  $('#estatisticas').replaceChildren(
    numero(String(s.perguntas), 'perguntas', 'perguntas'),
    numero(`${pct(s.taxaAcertoTemplate)}`, `acertos de template (${s.acertosTemplate})`, 'acertos'),
    numero(String(s.chamadasGeracao), 'chamadas ao LLM de geração', 'geracao'),
    numero(String(s.chamadasParser), 'chamadas ao parser (LLM)', 'parser'),
    numero(`${s.tokensEntrada + s.tokensSaida}`, 'tokens', 'tokens'),
    numero(custo(s.custo), 'custo estimado', 'custo'),
    numero(`${Math.round(s.latenciaMediaMs)} ms`, 'latência média', 'latencia'),
    numero(`${s.templatesSalvos} / ${s.templatesDescartados}`, 'templates salvos / descartados', 'templates'),
  );
  const c = e.configuracao;
  $('#configuracao').textContent = `Peso inicial ${peso(c.pesoInicial)} · média móvel α ${peso(c.alfa)} · descarte abaixo de ${peso(c.limiar)} · até ${c.limiteTentativas} reenvios ao LLM · fonte com ${e.fonte.entidades} entidades.`;

  $('#templates').replaceChildren(
    ...e.templates.map((t) => {
      const tr = document.createElement('tr');
      tr.dataset.chave = t.chave;
      const td = (conteudo) => {
        const c = document.createElement('td');
        if (typeof conteudo === 'string') c.textContent = conteudo;
        else c.append(conteudo);
        return c;
      };
      const codigo = (txt) => Object.assign(document.createElement('code'), { textContent: txt });
      const barra = document.createElement('span');
      barra.innerHTML = `<span class="barra"><i style="width:${Math.round(t.peso * 100)}%"></i></span>${peso(t.peso)}`;
      const apagar = Object.assign(document.createElement('button'), { type: 'button', textContent: 'Apagar', className: 'danger' });
      apagar.addEventListener('click', async () => {
        await fetch(`/api/orquestrador/templates/${encodeURIComponent(t.chave)}`, { method: 'DELETE' });
        await atualizarEstado();
      });
      tr.append(td(codigo(t.chave)), td(codigo(t.template)), td(barra), td(String(t.usos)), td(String(t.feedbacksBons)), td(String(t.feedbacksRuins)), td(apagar));
      return tr;
    }),
  );
  $('#sem-templates').hidden = e.templates.length > 0;
}

// ---- ao vivo -----------------------------------------------------------------------

function registrar(texto) {
  const li = Object.assign(document.createElement('li'), { textContent: `${new Date().toLocaleTimeString('pt-BR')} ${texto}` });
  $('#log').prepend(li);
  while ($('#log').children.length > 80) $('#log').lastChild.remove();
}

function ouvir() {
  const eventos = new EventSource('/api/orquestrador/events');
  eventos.onmessage = (ev) => {
    const m = JSON.parse(ev.data);
    if (m.type === 'etapa') registrar(`${m.de} → ${m.para}${m.rotulo ? ` (${m.rotulo})` : ''}`);
    else if (m.type === 'resultado') {
      registrar(`resposta pelo ${m.resultado.caminho === 'template' ? 'template' : 'LLM'}: ${m.resultado.texto}`);
      // Perguntas de outras origens (uma persona numa conversa, o MCP) também aparecem aqui.
      if (!esperando) {
        mostrarResultado(m.resultado);
        void atualizarEstado();
      }
    } else if (m.type === 'feedback') {
      registrar(`feedback ${m.bom ? 'bom' : 'ruim'} em "${m.chave}"`);
      void atualizarEstado();
    }
  };
}

// ---- início ------------------------------------------------------------------------

$('#form-pergunta').addEventListener('submit', (e) => {
  e.preventDefault();
  void perguntar($('#pergunta').value);
});
// O LLM das chaves novas: com MCP, a resposta espera o Claude Code atender pelo /mcp.
const motor = $('#motor');
try {
  motor.value = localStorage.getItem('orquestrador:motor') ?? motor.value;
} catch {}
const avisoMcp = () => ($('#aviso-mcp').hidden = motor.value !== 'mcp');
motor.addEventListener('change', () => {
  avisoMcp();
  try {
    localStorage.setItem('orquestrador:motor', motor.value);
  } catch {}
});
avisoMcp();
$('#bom').addEventListener('click', () => void avaliar(true));
$('#ruim').addEventListener('click', () => void avaliar(false));
$('#limpar').addEventListener('click', async () => {
  if (!confirm('Apagar todos os templates? As próximas perguntas voltam ao LLM.')) return;
  await fetch('/api/orquestrador/templates', { method: 'DELETE' });
  await atualizarEstado();
});
$('#exemplos').replaceChildren(
  ...EXEMPLOS.map((p) => {
    const b = Object.assign(document.createElement('button'), { type: 'button', textContent: p });
    b.addEventListener('click', () => {
      $('#pergunta').value = p;
      void perguntar(p);
    });
    return b;
  }),
);

await carregarFigura();
await desenharFluxo([]);
await atualizarEstado();
ouvir();
document.body.dataset.pronto = 'true';
