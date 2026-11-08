import { cleanOwner, freeSlot, ID_RE, nextId, OWNER_MAX } from './model.js';

/**
 * Operações granulares sobre o diagrama, usadas pelo servidor e pelo navegador.
 * Cada uma altera o `Diagram` recebido e devolve o evento que descreve a mudança;
 * o mesmo evento, aplicado com `applyEvent`, reproduz a mudança em outra cópia.
 */

/** `owner` vazio ou `null` remove o responsável. */

export class OpError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

const NODE_FIELDS = ['text', 'x', 'y', 'active', 'owner'];
const EDGE_FIELDS = ['text', 'from', 'to', 'animated'];

function checkAnimated(v) {
  if (v !== undefined && typeof v !== 'boolean') throw new OpError(400, '"animated" deve ser true ou false.');
}

function checkFields(input, allowed) {
  if (typeof input !== 'object' || input === null || Array.isArray(input)) throw new OpError(400, 'O corpo deve ser um objeto JSON.');
  const unknown = Object.keys(input).filter((k) => !allowed.includes(k));
  if (unknown.length) throw new OpError(400, `Campo(s) desconhecido(s): ${unknown.join(', ')}. Aceitos: ${allowed.join(', ')}.`);
}

function checkNodePatch(p) {
  if (p.text !== undefined && (typeof p.text !== 'string' || !p.text.trim())) throw new OpError(400, '"text" deve ser um texto não vazio.');
  for (const k of ['x', 'y']) {
    if (p[k] !== undefined && !Number.isFinite(p[k])) throw new OpError(400, `"${k}" deve ser um número.`);
  }
  if (p.active !== undefined && typeof p.active !== 'boolean') throw new OpError(400, '"active" deve ser true ou false.');
  if (p.owner !== undefined && p.owner !== null) {
    if (typeof p.owner !== 'string') throw new OpError(400, '"owner" deve ser um texto (ou null para remover).');
    if (cleanOwner(p.owner).length > OWNER_MAX) throw new OpError(400, `"owner" deve ter no máximo ${OWNER_MAX} caracteres.`);
  }
}

function checkNewId(id, taken) {
  if (id === undefined) return;
  if (typeof id !== 'string' || !ID_RE.test(id)) throw new OpError(400, `Id inválido: "${id}".`);
  if (taken.some((x) => x.id === id)) throw new OpError(409, `Já existe um elemento com o id "${id}".`);
}

export function getNode(d, id) {
  const n = d.nodes.find((n) => n.id === id);
  if (!n) throw new OpError(404, `Retângulo "${id}" não encontrado.`);
  return n;
}

export function getEdge(d, id) {
  const e = d.edges.find((e) => e.id === id);
  if (!e) throw new OpError(404, `Seta "${id}" não encontrada.`);
  return e;
}

/** Encontra um retângulo pelo id ou, se não houver, pelo texto (sem diferenciar maiúsculas). */
export function resolveNode(d, ref) {
  const byId = d.nodes.find((n) => n.id === ref);
  if (byId) return byId;
  const wanted = ref.trim().toLowerCase();
  const byText = d.nodes.filter((n) => n.text.trim().toLowerCase() === wanted);
  if (byText.length > 1) throw new OpError(409, `Mais de um retângulo com o texto "${ref}"; use o id.`);
  if (!byText[0]) throw new OpError(404, `Retângulo "${ref}" não encontrado.`);
  return byText[0];
}

function checkEdgeEnds(d, from, to, ignoreId) {
  for (const [k, id] of [
    ['from', from],
    ['to', to],
  ]) {
    if (typeof id !== 'string' || !d.nodes.some((n) => n.id === id)) throw new OpError(400, `"${k}": retângulo "${id}" não existe.`);
  }
  if (d.edges.some((e) => e.id !== ignoreId && e.from === from && e.to === to)) {
    throw new OpError(409, `Já existe uma seta de "${from}" para "${to}".`);
  }
}

// ---- retângulos ------------------------------------------------------------

export function addNode(d, input) {
  const { id, ...patch } = input;
  checkFields(patch, NODE_FIELDS);
  checkNodePatch(patch);
  checkNewId(id, d.nodes);
  const slot = patch.x === undefined || patch.y === undefined ? freeSlot(d.nodes) : undefined;
  const node = {
    id:
      id ??
      nextId(
        'n',
        d.nodes.map((n) => n.id),
      ),
    text: patch.text?.trim() ?? 'Novo',
    x: patch.x ?? slot.x,
    y: patch.y ?? slot.y,
  };
  if (patch.active) node.active = true;
  const owner = patch.owner ? cleanOwner(patch.owner) : '';
  if (owner) node.owner = owner;
  d.nodes.push(node);
  return { type: 'node.added', node };
}

export function updateNode(d, id, patch) {
  checkFields(patch, NODE_FIELDS);
  checkNodePatch(patch);
  const n = getNode(d, id);
  if (patch.text !== undefined) n.text = patch.text.trim();
  if (patch.x !== undefined) n.x = patch.x;
  if (patch.y !== undefined) n.y = patch.y;
  if (patch.active !== undefined) {
    if (patch.active) n.active = true;
    else delete n.active;
  }
  if (patch.owner !== undefined) {
    const owner = patch.owner ? cleanOwner(patch.owner) : '';
    if (owner) n.owner = owner;
    else delete n.owner;
  }
  return { type: 'node.updated', node: n };
}

export function removeNode(d, id) {
  getNode(d, id);
  const edges = d.edges.filter((e) => e.from === id || e.to === id).map((e) => e.id);
  d.nodes = d.nodes.filter((n) => n.id !== id);
  d.edges = d.edges.filter((e) => !edges.includes(e.id));
  return { type: 'node.removed', id, edges };
}

// ---- setas -----------------------------------------------------------------

export function addEdge(d, input) {
  const { id, ...rest } = input;
  checkFields(rest, EDGE_FIELDS);
  checkNewId(id, d.edges);
  if (rest.text !== undefined && typeof rest.text !== 'string') throw new OpError(400, '"text" deve ser um texto.');
  checkAnimated(rest.animated);
  checkEdgeEnds(d, rest.from, rest.to);
  const edge = {
    id:
      id ??
      nextId(
        'e',
        d.edges.map((e) => e.id),
      ),
    from: rest.from,
    to: rest.to,
    text: rest.text?.trim() ?? '',
  };
  if (rest.animated) edge.animated = true;
  d.edges.push(edge);
  return { type: 'edge.added', edge };
}

export function updateEdge(d, id, patch) {
  checkFields(patch, EDGE_FIELDS);
  const e = getEdge(d, id);
  if (patch.text !== undefined && typeof patch.text !== 'string') throw new OpError(400, '"text" deve ser um texto.');
  checkAnimated(patch.animated);
  const from = patch.from ?? e.from;
  const to = patch.to ?? e.to;
  if (from !== e.from || to !== e.to) checkEdgeEnds(d, from, to, e.id);
  e.from = from;
  e.to = to;
  if (patch.text !== undefined) e.text = patch.text.trim();
  if (patch.animated !== undefined) {
    if (patch.animated) e.animated = true;
    else delete e.animated;
  }
  return { type: 'edge.updated', edge: e };
}

export function removeEdge(d, id) {
  getEdge(d, id);
  d.edges = d.edges.filter((e) => e.id !== id);
  return { type: 'edge.removed', id };
}

// ---- diagrama inteiro ------------------------------------------------------

/** Monta um diagrama validando cada retângulo e seta com as mesmas regras das operações. */
export function buildDiagram(input) {
  const src = input;
  if (!src || !Array.isArray(src.nodes) || !Array.isArray(src.edges)) {
    throw new OpError(400, 'Esperado um objeto { "nodes": [...], "edges": [...] }.');
  }
  const d = { nodes: [], edges: [] };
  for (const n of src.nodes) addNode(d, n);
  for (const e of src.edges) addEdge(d, e);
  return d;
}

const clone = (v) => structuredClone(v);

/** Aplica um evento (vindo de outra cópia do diagrama) a `d`. */
export function applyEvent(d, ev) {
  switch (ev.type) {
    case 'node.added':
    case 'node.updated': {
      const i = d.nodes.findIndex((n) => n.id === ev.node.id);
      if (i >= 0) d.nodes[i] = clone(ev.node);
      else d.nodes.push(clone(ev.node));
      return d;
    }
    case 'node.removed':
      d.nodes = d.nodes.filter((n) => n.id !== ev.id);
      d.edges = d.edges.filter((e) => !ev.edges.includes(e.id) && e.from !== ev.id && e.to !== ev.id);
      return d;
    case 'edge.added':
    case 'edge.updated': {
      const i = d.edges.findIndex((e) => e.id === ev.edge.id);
      if (i >= 0) d.edges[i] = clone(ev.edge);
      else d.edges.push(clone(ev.edge));
      return d;
    }
    case 'edge.removed':
      d.edges = d.edges.filter((e) => e.id !== ev.id);
      return d;
    case 'diagram.replaced':
      return clone(ev.diagram);
  }
}
