import { API } from './diagrams.js';

/** Pulso numa seta: efêmero, não muda o diagrama nem o `rev`. */

/** Intervalo mínimo entre PATCHes de posição de um mesmo retângulo durante o arraste. */
const MOVE_THROTTLE_MS = 150;

/**
 * Sincroniza o Store com a API do diagrama aberto (server/api.ts, em /api/d/:slug) mandando só o que mudou:
 * cada criação/edição/remoção de retângulo ou seta vira uma requisição para aquele elemento.
 * Do servidor chegam eventos do mesmo formato; um buraco na sequência `rev` provoca uma ressincronização.
 */
export class ServerSync {
  clientId = crypto.randomUUID?.() ?? Math.random().toString(36).slice(2);
  online = false;
  rev = 0;
  resyncing = false;
  /** Requisições em fila, em ordem: um PATCH nunca chega antes do POST do mesmo elemento. */
  chain = Promise.resolve();
  pending = new Map();

  constructor(
    store,
    onPulse = () => {},
    /** Eventos que não mexem no desenho (chat.*, persona.atualizada): vão para o chat e o canvas. */
    onOther = () => {},
  ) {
    this.store = store;
    this.onPulse = onPulse;
    this.onOther = onOther;
    const events = new EventSource(`${API}/events`);
    events.onmessage = (ev) => this.receive(JSON.parse(ev.data));
    // O EventSource reconecta sozinho; ao voltar, o servidor manda um snapshot novo.
    events.onerror = () => (this.online = false);
    store.subscribe(this.onChange);
  }

  // ---- servidor → navegador -------------------------------------------------

  receive(msg) {
    if (msg.type === 'snapshot') {
      this.online = true;
      this.rev = msg.rev;
      this.pending.clear();
      // Servidor ainda sem diagrama: ele adota o deste navegador.
      if (msg.diagram) this.store.replace(msg.diagram, 'remote');
      else this.send('PUT', `${API}/diagram.json`, this.store.diagram);
      return;
    }
    if (msg.type === 'edge.pulse') return this.onPulse(msg);
    // Conversa e personas não têm rev: não entram na ordem das mudanças do desenho.
    const tipo = msg.type;
    if (tipo.startsWith('chat.') || tipo === 'persona.atualizada') return this.onOther(msg);
    if (this.resyncing) return;
    if (msg.rev !== this.rev + 1) return void this.resync();
    this.rev = msg.rev;
    if (msg.source === this.clientId) return;

    const { rev: _rev, source: _source, ...ev } = msg;
    // Um arraste local ainda não enviado continua valendo sobre a versão remota do retângulo.
    if (ev.type === 'node.updated') {
      const mine = this.pending.get(`${API}/nodes/${ev.node.id}`);
      if (mine) Object.assign(ev.node, mine.patch);
    }
    this.store.applyRemote(ev);
  }

  async resync() {
    if (this.resyncing) return;
    this.resyncing = true;
    try {
      const res = await fetch(`${API}/diagram.json`);
      const { rev, diagram } = await res.json();
      this.rev = rev;
      this.pending.clear();
      if (diagram) this.store.replace(diagram, 'remote');
    } catch {
      // Servidor fora do ar: o snapshot da reconexão resolve.
    } finally {
      this.resyncing = false;
    }
  }

  // ---- navegador → servidor -------------------------------------------------

  onChange = (change) => {
    if (change.source === 'view' || change.source === 'remote' || !this.online) return;
    const ev = change.event;
    switch (ev.type) {
      case 'node.added':
        return this.send('POST', `${API}/nodes`, ev.node);
      case 'node.updated':
        return this.patch(`${API}/nodes/${ev.node.id}`, change.patch ?? {});
      case 'node.removed':
        this.drop(`${API}/nodes/${ev.id}`);
        for (const id of ev.edges) this.drop(`${API}/edges/${id}`);
        return this.send('DELETE', `${API}/nodes/${ev.id}`);
      case 'edge.added':
        return this.send('POST', `${API}/edges`, ev.edge);
      case 'edge.updated':
        return this.patch(`${API}/edges/${ev.edge.id}`, change.patch ?? {});
      case 'edge.removed':
        this.drop(`${API}/edges/${ev.id}`);
        return this.send('DELETE', `${API}/edges/${ev.id}`);
      case 'diagram.replaced':
        for (const path of [...this.pending.keys()]) this.drop(path);
        return this.send('PUT', `${API}/diagram.json`, ev.diagram);
    }
  };

  /** Pede ao servidor um pulso na seta (todas as abas veem). Sem servidor, devolve false. */
  pulse(id, label) {
    if (!this.online) return false;
    this.send('POST', `${API}/edges/${id}/pulse`, label ? { label } : {});
    return true;
  }

  /** Junta PATCHes do mesmo elemento. Só posição (arraste) espera um pouco; o resto vai na hora. */
  patch(path, patch) {
    const p = this.pending.get(path) ?? { path, patch: {} };
    Object.assign(p.patch, patch);
    this.pending.set(path, p);
    const onlyMove = Object.keys(patch).every((k) => k === 'x' || k === 'y');
    if (!onlyMove) return this.flush(path);
    p.timer ??= window.setTimeout(() => this.flush(path), MOVE_THROTTLE_MS);
  }

  flush(path) {
    const p = this.pending.get(path);
    if (!p) return;
    clearTimeout(p.timer);
    this.pending.delete(path);
    this.send('PATCH', path, p.patch);
  }

  drop(path) {
    clearTimeout(this.pending.get(path)?.timer);
    this.pending.delete(path);
  }

  send(method, path, body) {
    this.chain = this.chain.then(async () => {
      try {
        const res = await fetch(path, {
          method,
          headers: { 'Content-Type': 'application/json', 'X-Client-Id': this.clientId },
          body: body === undefined ? undefined : JSON.stringify(body),
        });
        if (!res.ok) throw new Error(`${method} ${path}: ${res.status}`);
      } catch (err) {
        console.warn('mermaid-draw: falha ao sincronizar, recarregando do servidor.', err);
        await this.resync();
      }
    });
  }
}
