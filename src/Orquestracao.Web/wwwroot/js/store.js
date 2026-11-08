import * as ops from './ops.js';
import { OpError } from './ops.js';

/** De onde veio a mudança: `local` (canvas), `code` (painel de código) ou `remote` (servidor, ex.: um curl). */

/**
 * `view`: só seleção/modo mudou. Os demais trazem o evento exato, para atualizar só o que mudou.
 * Em atualizações locais, `patch` diz quais campos mudaram (é o que vai no PATCH ao servidor).
 */

/** Id gerado no navegador: aleatório, para não colidir com o de outra aba criando ao mesmo tempo. */
function localId(prefix, taken) {
  let id;
  do id = prefix + Math.random().toString(36).slice(2, 6);
  while (taken.some((x) => x.id === id));
  return id;
}

export class Store {
  selection = null;
  mode = 'select';
  listeners = [];

  constructor(diagram) {
    this.diagram = diagram;
  }

  subscribe(fn) {
    this.listeners.push(fn);
  }

  emit(change) {
    for (const fn of this.listeners) fn(change);
  }

  node(id) {
    return this.diagram.nodes.find((n) => n.id === id);
  }

  edge(id) {
    return this.diagram.edges.find((e) => e.id === id);
  }

  select(selection) {
    this.selection = selection;
    this.emit({ source: 'view' });
  }

  setMode(mode) {
    this.mode = mode;
    this.emit({ source: 'view' });
  }

  /** Aplica uma operação local e avisa; operações inválidas (ex.: seta duplicada) são ignoradas. */
  run(op, source = 'local', patch) {
    let ev;
    try {
      ev = op(this.diagram);
    } catch (err) {
      if (err instanceof OpError) return;
      throw err;
    }
    if (ev.type === 'diagram.replaced') this.diagram = ev.diagram;
    this.keepSelection();
    this.emit({ source, event: ev, patch });
    return ev;
  }

  /** Tira a seleção se o elemento selecionado deixou de existir. */
  keepSelection() {
    const sel = this.selection;
    if (sel && !(sel.kind === 'node' ? this.node(sel.id) : this.edge(sel.id))) this.selection = null;
  }

  replace(diagram, source) {
    this.run(() => ({ type: 'diagram.replaced', diagram }), source);
  }

  /** Evento vindo do servidor. */
  applyRemote(ev) {
    this.diagram = ops.applyEvent(this.diagram, ev);
    this.keepSelection();
    this.emit({ source: 'remote', event: ev });
  }

  addNode(x, y, text = 'Novo') {
    const ev = this.run((d) => ops.addNode(d, { id: localId('n', d.nodes), text, x, y }));
    if (ev?.type !== 'node.added') throw new Error('falha ao criar retângulo');
    this.select({ kind: 'node', id: ev.node.id });
    return ev.node;
  }

  updateNode(id, patch) {
    this.run((d) => ops.updateNode(d, id, patch), 'local', patch);
  }

  toggleActive(id) {
    const n = this.node(id);
    if (n) this.updateNode(id, { active: !n.active });
  }

  addEdge(from, to, text = '') {
    const ev = this.run((d) => ops.addEdge(d, { id: localId('e', d.edges), from, to, text }));
    if (ev?.type !== 'edge.added') return;
    this.select({ kind: 'edge', id: ev.edge.id });
    return ev.edge;
  }

  toggleAnimated(id) {
    const e = this.edge(id);
    if (e) this.updateEdge(id, { animated: !e.animated });
  }

  updateEdge(id, patch) {
    this.run((d) => ops.updateEdge(d, id, patch), 'local', patch);
  }

  removeSelected() {
    const sel = this.selection;
    if (!sel) return;
    this.run((d) => (sel.kind === 'node' ? ops.removeNode(d, sel.id) : ops.removeEdge(d, sel.id)));
  }
}
