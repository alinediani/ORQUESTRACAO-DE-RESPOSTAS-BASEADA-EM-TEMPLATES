/**
 * Painel da persona do retângulo selecionado: motor, modelo, papel, esforço, cor e o prompt.
 * Grava sozinho (o .md em personas/<id>.md), um pouco depois da última alteração.
 */

const SALVAR_MS = 600;
const PADRAO = { mcp: 'claude-opus-5-5', ollama: 'qwen2.5:3b', api: 'claude-opus-5-5' };
const $ = (sel) => document.querySelector(sel);

/** O .md da persona, no mesmo formato que o servidor grava. */
export function personaMd(p) {
  const linhas = [`motor: ${p.motor}`, `modelo: ${p.modelo}`, `esforco: ${p.esforco}`, `papel: ${p.papel}`, `cor: "${p.cor}"`];
  if (p.ferramentas.length) linhas.push(`ferramentas: [${p.ferramentas.join(', ')}]`);
  if (p.inicio) linhas.push('inicio: true');
  if (p.saida) linhas.push('saida: true');
  if (p.arquivo && p.arquivo !== 'saida.md') linhas.push(`arquivo: ${JSON.stringify(p.arquivo)}`);
  if (p.humana) linhas.push('humana: true');
  if (p.prazo) linhas.push(`prazo: ${p.prazo}`);
  if (p.aoVencer === 'encerrar') linhas.push('aoVencer: encerrar');
  if (p.memoria) linhas.push('memoria: true');
  if (p.escuta !== 'todas') linhas.push(`escuta: [${p.escuta.join(', ')}]`);
  if (p.quando === 'fala') linhas.push('quando: fala');
  if (p.limite && p.limite !== 4000) linhas.push(`limite: ${p.limite}`);
  if (p.resumoPara !== 'todas') linhas.push(`resumoPara: [${p.resumoPara.join(', ')}]`);
  if (p.curadoria) linhas.push('curadoria: true');
  if (p.depois?.diagrama) {
    linhas.push('depois:', `  diagrama: ${p.depois.diagrama}`);
    if (p.depois.modo) linhas.push(`  modo: ${p.depois.modo}`);
    if (p.depois.rodadas) linhas.push(`  rodadas: ${p.depois.rodadas}`);
    if (p.depois.prefixo) linhas.push(`  prefixo: ${JSON.stringify(p.depois.prefixo)}`);
  }
  return `# ${p.nome}\n\n${p.prompt.trim()}\n\n\`\`\`yaml\n${linhas.join('\n')}\n\`\`\`\n`;
}

export class PersonaPanel {
  id = null;
  atual = null;
  timer;
  sujo = false;
  modelos = {};
  root = $('#persona-panel');
  campos = {
    motor: $('#pp-motor'),
    modelo: $('#pp-modelo'),
    papel: $('#pp-papel'),
    esforco: $('#pp-esforco'),
    cor: $('#pp-cor'),
    prompt: $('#pp-prompt'),
    inicio: $('#pp-inicio'),
    saida: $('#pp-saida'),
    arquivo: $('#pp-arquivo'),
    humana: $('#pp-humana'),
    prazo: $('#pp-prazo'),
    aoVencer: $('#pp-aovencer'),
    depois: $('#pp-depois'),
    depoisModo: $('#pp-depois-modo'),
    depoisRodadas: $('#pp-depois-rodadas'),
    memoria: $('#pp-memoria'),
    quando: $('#pp-quando'),
    limite: $('#pp-limite'),
    curadoria: $('#pp-curadoria'),
  };
  template = new TemplateEditor();

  constructor(
    api,
    store,
    /** Avisa o resto da tela (canvas, chat) que a persona mudou. */
    mudou,
    /** Abre o palácio da memória do diagrama. */
    abrirPalacio = () => {},
  ) {
    this.api = api;
    this.store = store;
    this.mudou = mudou;
    this.abrirPalacio = abrirPalacio;
    store.subscribe(() => {
      const sel = store.selection?.kind === 'node' ? store.selection.id : null;
      if (sel !== this.id) void this.mostrar(sel);
    });
    for (const el of Object.values(this.campos)) el.addEventListener('input', () => this.editar());
    this.campos.motor.addEventListener('change', () => {
      // Trocar de motor troca o modelo pelo padrão do novo motor, se o atual não for dele.
      const motor = this.campos.motor.value;
      const doClaude = this.campos.modelo.value.startsWith('claude');
      if ((motor === 'ollama') === doClaude) this.campos.modelo.value = this.modelos[motor]?.[0] ?? PADRAO[motor];
      this.preencherModelos();
      this.editar();
    });
    $('#pp-palacio').addEventListener('click', () => this.abrirPalacio());
    $('#pp-template').addEventListener(
      'click',
      () => this.atual && void this.template.abrir(`${this.api}/personas/${encodeURIComponent(this.atual.id)}/saida`, this.atual.arquivo),
    );
    document.addEventListener('motores', (e) => {
      for (const m of e.detail) {
        this.modelos[m.nome] = m.modelos;
        const opt = [...this.campos.motor.options].find((o) => o.value === m.nome);
        if (opt) opt.textContent = `${opt.textContent.replace(/ \(fora do ar\)$/, '')}${m.ok ? '' : ' (fora do ar)'}`;
      }
      this.preencherModelos();
    });
  }

  /** Tecla I: marca ou desmarca o bloco de início da persona selecionada (e grava já). */
  alternarInicio() {
    if (!this.atual) return;
    this.campos.inicio.checked = !this.campos.inicio.checked;
    this.editar();
    void this.salvar();
  }

  /** Outra aba (ou o MCP) mudou a persona: recarrega se não há edição pendente aqui. */
  atualizada(p) {
    if (p.id === this.id && !this.sujo) this.preencher(p);
  }

  async mostrar(id) {
    if (this.sujo) await this.salvar();
    this.id = id;
    this.root.hidden = !id;
    if (!id) return;
    const res = await fetch(`${this.api}/personas/${encodeURIComponent(id)}`);
    if (!res.ok || this.id !== id) return;
    this.preencher((await res.json()).persona);
  }

  preencher(p) {
    this.atual = p;
    $('#pp-titulo').textContent = `Persona · ${p.nome}`;
    const c = this.campos;
    c.motor.value = p.motor;
    c.modelo.value = p.modelo;
    c.papel.value = p.papel;
    c.esforco.value = p.esforco;
    c.cor.value = p.cor;
    c.inicio.checked = p.inicio;
    c.saida.checked = p.saida;
    if (document.activeElement !== c.arquivo) c.arquivo.value = p.arquivo;
    $('#pp-saida-box').hidden = !p.saida;
    c.humana.checked = p.humana;
    if (document.activeElement !== c.prazo) c.prazo.value = p.prazo ? String(p.prazo) : '';
    c.aoVencer.value = p.aoVencer;
    void this.preencherDiagramas(p.depois?.diagrama ?? '');
    c.depoisModo.value = p.depois?.modo ?? 'debate';
    c.depoisRodadas.value = String(p.depois?.rodadas ?? 2);
    $('#pp-depois-box').hidden = !p.saida;
    c.memoria.checked = p.memoria;
    c.curadoria.checked = p.curadoria;
    c.quando.value = p.quando;
    if (document.activeElement !== c.limite) c.limite.value = String(p.limite);
    $('#pp-memoria-box').hidden = !p.memoria;
    this.preencherLista('#pp-escuta', p.id, p.escuta);
    this.preencherLista('#pp-resumo', p.id, p.resumoPara);
    this.mostrarHumana(p.humana);
    if (document.activeElement !== c.prompt) c.prompt.value = p.prompt;
    this.preencherModelos();
    this.status('');
  }

  /** Uma lista de personas ("Escuta:", "Resumo no início para:"): "todas" e cada uma (menos a própria memória). */
  preencherLista(sel, proprio, valor) {
    const box = $(sel);
    const caixa = (valor, rotulo, marcada) => {
      const l = Object.assign(document.createElement('label'), { className: 'pp-inicio' });
      const i = Object.assign(document.createElement('input'), { type: 'checkbox', value: valor, checked: marcada });
      i.addEventListener('change', () => {
        if (valor === 'todas' && i.checked) for (const o of box.querySelectorAll('input')) o.checked = o === i;
        if (valor !== 'todas' && i.checked) box.querySelector('input[value="todas"]').checked = false;
        this.editar();
      });
      l.append(i, ` ${rotulo}`);
      return l;
    };
    const todas = valor === 'todas';
    box.replaceChildren(
      caixa('todas', 'todas', todas),
      ...this.store.diagram.nodes.filter((n) => n.id !== proprio).map((n) => caixa(n.id, n.text, !todas && valor.includes(n.id))),
    );
  }

  /** "todas", os ids marcados, ou undefined quando nada está marcado (cada lista decide o padrão). */
  marcadas(sel) {
    const marcadas = [...document.querySelectorAll(`${sel} input:checked`)].map((i) => i.value);
    if (marcadas.includes('todas')) return 'todas';
    return marcadas.length ? marcadas : undefined;
  }

  /** Os diagramas para o pós-fluxo (o atual também vale: a cadeia tem limite). */
  async preencherDiagramas(escolhido) {
    const lista = await fetch('/api/diagrams')
      .then((r) => r.json())
      .catch(() => []);
    const sel = this.campos.depois;
    sel.replaceChildren(new Option('— nada —', ''), ...lista.map((d) => new Option(d.name, d.slug)));
    if (escolhido && !lista.some((d) => d.slug === escolhido)) sel.append(new Option(`${escolhido} (não existe)`, escolhido));
    sel.value = escolhido;
  }

  /** A persona humana não usa motor: o prazo aparece e os campos do motor ficam apagados. */
  mostrarHumana(humana) {
    $('#pp-humana-box').hidden = !humana;
    this.root.classList.toggle('pp-e-humana', humana);
  }

  preencherModelos() {
    const lista = this.modelos[this.campos.motor.value] ?? [];
    $('#pp-modelos').replaceChildren(...lista.map((m) => new Option(m, m)));
    this.campos.esforco.disabled = this.campos.motor.value !== 'api';
    this.campos.esforco.title = this.campos.esforco.disabled ? 'O esforço só vale no motor API.' : '';
  }

  editar() {
    if (!this.atual) return;
    const c = this.campos;
    this.atual = {
      ...this.atual,
      motor: c.motor.value,
      modelo: c.modelo.value.trim(),
      papel: c.papel.value,
      esforco: c.esforco.value,
      cor: c.cor.value,
      prompt: c.prompt.value,
      inicio: c.inicio.checked,
      saida: c.saida.checked,
      arquivo: c.arquivo.value.trim() || 'saida.md',
      humana: c.humana.checked,
      prazo: Number(c.prazo.value) > 0 ? Number(c.prazo.value) : undefined,
      aoVencer: c.aoVencer.value,
      depois: c.depois.value
        ? { ...this.atual.depois, diagrama: c.depois.value, modo: c.depoisModo.value, rodadas: Number(c.depoisRodadas.value) || 2 }
        : undefined,
      memoria: c.memoria.checked,
      escuta: this.marcadas('#pp-escuta') ?? 'todas',
      resumoPara: this.marcadas('#pp-resumo') ?? [],
      curadoria: c.curadoria.checked,
      quando: c.quando.value,
      limite: Math.max(200, Number(c.limite.value) || 4000),
    };
    $('#pp-saida-box').hidden = !c.saida.checked;
    this.mostrarHumana(c.humana.checked);
    $('#pp-depois-box').hidden = !c.saida.checked;
    $('#pp-memoria-box').hidden = !c.memoria.checked;
    this.sujo = true;
    this.status('alterações não salvas…');
    clearTimeout(this.timer);
    this.timer = window.setTimeout(() => void this.salvar(), SALVAR_MS);
  }

  async salvar() {
    clearTimeout(this.timer);
    const p = this.atual;
    if (!p || !this.sujo) return;
    this.sujo = false;
    const res = await fetch(`${this.api}/personas/${encodeURIComponent(p.id)}`, { method: 'PUT', body: personaMd(p) });
    const json = await res.json().catch(() => ({}));
    if (!res.ok) return this.status(json.error ?? `Erro ${res.status}`, true);
    this.status('salvo');
    this.mudou(json.persona);
  }

  status(texto, erro = false) {
    const el = $('#pp-status');
    el.textContent = texto;
    el.classList.toggle('error', erro);
  }
}

/** Editor só do template de saída (personas/<id>.saida.md): grava sozinho, sem tocar na persona. */
class TemplateEditor {
  dialog = $('#template-dialog');
  texto = $('#td-texto');
  url = '';
  timer;
  sujo = false;

  constructor() {
    this.texto.addEventListener('input', () => {
      this.sujo = true;
      this.status('alterações não salvas…');
      clearTimeout(this.timer);
      this.timer = window.setTimeout(() => void this.salvar(), SALVAR_MS);
    });
    $('#td-fechar').addEventListener('click', () => this.dialog.close());
    // Fechar (botão, Esc ou clique fora) grava o que estiver pendente.
    this.dialog.addEventListener('close', () => void this.salvar());
    this.dialog.addEventListener('click', (e) => e.target === this.dialog && this.dialog.close());
  }

  async abrir(url, arquivo) {
    await this.salvar();
    this.url = url;
    $('#td-titulo').textContent = `Template de saída · ${arquivo}`;
    const res = await fetch(url);
    const json = await res.json().catch(() => ({}));
    if (!res.ok) return this.status(json.error ?? `Erro ${res.status}`, true);
    this.texto.value = json.template;
    this.status(json.padrao ? 'template padrão (ainda não gravado)' : '');
    this.dialog.showModal();
    this.texto.focus();
    this.texto.setSelectionRange(0, 0);
    this.texto.scrollTop = 0;
  }

  async salvar() {
    clearTimeout(this.timer);
    if (!this.sujo || !this.url) return;
    this.sujo = false;
    const res = await fetch(this.url, { method: 'PUT', body: this.texto.value });
    const json = await res.json().catch(() => ({}));
    this.status(res.ok ? 'salvo' : (json.error ?? `Erro ${res.status}`), !res.ok);
  }

  status(texto, erro = false) {
    const el = $('#td-status');
    el.textContent = texto;
    el.classList.toggle('error', erro);
  }
}
