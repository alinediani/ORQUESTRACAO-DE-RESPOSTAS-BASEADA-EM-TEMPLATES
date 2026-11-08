/**
 * Estado do chat a partir dos eventos do servidor (chat.inicio, chat.turno, chat.delta,
 * chat.mensagem, chat.fim) ou de uma conversa salva. Funções puras: o painel só desenha o resultado.
 */

/** Uma persona com a vez: o texto vai chegando (no Ollama e na API) ou fica "pensando…" (no MCP). */

/** A vez da persona humana: a conversa espera a resposta do usuário. */

export const inicial = () => ({ status: 'vazio', mensagens: [], pendentes: [], custo: 0 });

const somaCusto = (ms) => ms.reduce((t, m) => t + (m.uso?.custo ?? 0), 0);

export function aplicar(s, ev) {
  if (ev.type === 'carregar') {
    const c = ev.conversa;
    const pendentes = (c.pendentes ?? []).map((p) => ({ ...p, texto: '' }));
    return {
      conversa: c.id,
      assunto: c.assunto,
      modo: c.modo,
      status: c.status,
      espera: c.espera,
      mensagens: [...c.mensagens],
      pendentes,
      resposta: c.resposta,
      custo: somaCusto(c.mensagens),
    };
  }
  if (ev.type === 'chat.inicio') {
    return { ...inicial(), conversa: ev.conversa, assunto: ev.assunto, modo: ev.modo, status: 'rodando' };
  }
  // Eventos de outra conversa (outra aba, ou a do MCP) não mexem nesta.
  if (!s.conversa || ev.conversa !== s.conversa) return s;
  switch (ev.type) {
    case 'chat.turno': {
      if (ev.status === 'aguardando') return { ...s, status: 'aguardando', espera: ev.espera };
      if (s.espera?.turno === ev.turno) s = { ...s, status: 'rodando', espera: undefined };
      const sem = s.pendentes.filter((p) => p.turno !== ev.turno);
      if (ev.status !== 'pensando') return { ...s, pendentes: sem };
      return {
        ...s,
        status: 'rodando',
        pendentes: [...sem, { turno: ev.turno, persona: ev.persona, nome: ev.nome, tipo: ev.tipo, motor: ev.motor, texto: '' }],
      };
    }
    case 'chat.delta':
      return { ...s, pendentes: s.pendentes.map((p) => (p.turno === ev.turno ? { ...p, texto: p.texto + ev.texto } : p)) };
    case 'chat.mensagem': {
      const m = ev.mensagem;
      if (s.mensagens.some((x) => x.id === m.id)) return s;
      const mensagens = [...s.mensagens, m];
      const status = m.tipo === 'usuario' && s.status !== 'rodando' ? 'rodando' : s.status;
      return { ...s, status, mensagens, pendentes: s.pendentes.filter((p) => p.turno !== m.turno), custo: somaCusto(mensagens) };
    }
    case 'chat.mensagem.atualizada': {
      const m = ev.mensagem;
      return { ...s, mensagens: s.mensagens.map((x) => (x.id === m.id ? m : x)) };
    }
    case 'chat.fim':
      return { ...s, status: ev.status, resposta: ev.resposta ?? s.resposta, pendentes: [], espera: undefined };
  }
  return s;
}

/** Os .md que as personas de saída geraram nesta conversa. */
export function saidasDe(s) {
  return s.mensagens.filter((m) => m.tipo === 'saida' && m.arquivo).map((m) => ({ id: m.id, nome: m.nome, arquivo: m.arquivo, texto: m.texto }));
}

/** Abre o diagrama já na conversa (o chat lê o ?c=). */
export const urlDaConversa = (diagrama, conversa) => `?d=${encodeURIComponent(diagrama)}&c=${encodeURIComponent(conversa)}`;

/** Onde baixar o .md gerado. */
export const urlDaSaida = (api, conversa, arquivo) => `${api}/conversas/${encodeURIComponent(conversa)}/saidas/${encodeURIComponent(arquivo)}`;

const normalizar = (t) => t.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();

/** As personas que completam a @menção sendo digitada no fim do texto. */
export function sugerirMencoes(texto, pessoas) {
  const m = /@([\p{L}\p{N} ]*)$/u.exec(texto);
  if (!m) return [];
  const parcial = normalizar(m[1]);
  // "@Cética já completa " → a menção acabou.
  if (pessoas.some((p) => parcial.startsWith(`${normalizar(p.nome)} `))) return [];
  return pessoas.filter((p) => normalizar(p.nome).startsWith(parcial.trimEnd()));
}

export function formatarCusto(dolares) {
  const casas = dolares > 0 && dolares < 0.01 ? 4 : 2;
  return `US$ ${dolares.toFixed(casas).replace('.', ',')}`;
}
