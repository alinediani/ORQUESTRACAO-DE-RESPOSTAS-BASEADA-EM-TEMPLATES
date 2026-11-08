/**
 * Utilitários do modelo do diagrama usados no navegador (canvas e ops.js).
 *
 * O código Mermaid (ler e gerar) fica só no servidor, em C# (Orquestracao.Core/Diagramas/Mermaid.cs):
 * o painel de código usa POST /api/mermaid/ler e POST /api/mermaid/gerar.
 *
 * Formato de um diagrama: { nodes: [{ id, text, x, y, active?, owner? }], edges: [{ id, from, to, text, animated? }] }
 */

export const ID_RE = /^[A-Za-z_][\w-]*$/;

export const OWNER_MAX = 60;
/** Responsável em uma linha só: espaços repetidos e quebras viram um espaço. */
export const cleanOwner = (s) => s.trim().replace(/\s+/g, ' ');

/** Primeira vaga livre de uma grade, longe dos retângulos em `taken`. */
export function freeSlot(taken) {
  for (let slot = 0; ; slot++) {
    const p = { x: 140 + (slot % 4) * 220, y: 80 + Math.floor(slot / 4) * 110 };
    if (!taken.some((t) => Math.abs(t.x - p.x) < 180 && Math.abs(t.y - p.y) < 70)) return p;
  }
}

export function nextId(prefix, ids) {
  let max = 0;
  for (const id of ids) {
    const m = new RegExp(`^${prefix}(\\d+)$`).exec(id);
    if (m) max = Math.max(max, Number(m[1]));
  }
  return `${prefix}${max + 1}`;
}
