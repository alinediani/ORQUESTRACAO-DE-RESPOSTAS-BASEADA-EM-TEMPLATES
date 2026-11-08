// Mini harness de testes que roda no navegador (Chromium, pelo Playwright), com um `expect` no estilo
// do Vitest. Os arquivos *.spec.js são colados depois deste e registram os testes com describe/it;
// o C# (FrontUnitTests) chama rodar() e confere o resultado de cada um.
const testes = [];
const prefixos = [];
const describe = (nome, fn) => {
  prefixos.push(nome);
  fn();
  prefixos.pop();
};
const it = (nome, fn) => testes.push({ nome: [...prefixos, nome].join(' › '), fn });

const mostrar = (v) => JSON.stringify(v, (_, x) => (x === undefined ? '«undefined»' : x));
/** Igualdade profunda que ignora chaves com valor undefined (como o toEqual do Vitest). */
function igual(a, b) {
  if (Object.is(a, b)) return true;
  if (typeof a !== 'object' || typeof b !== 'object' || a === null || b === null) return false;
  if (Array.isArray(a) !== Array.isArray(b)) return false;
  if (Array.isArray(a)) return a.length === b.length && a.every((x, i) => igual(x, b[i]));
  const ka = Object.keys(a).filter((k) => a[k] !== undefined);
  const kb = Object.keys(b).filter((k) => b[k] !== undefined);
  return ka.length === kb.length && ka.every((k) => igual(a[k], b[k]));
}
/** `a` contém `b` (toMatchObject): objetos por subconjunto, listas elemento a elemento. */
function contem(a, b) {
  if (typeof b !== 'object' || b === null) return Object.is(a, b);
  if (typeof a !== 'object' || a === null) return false;
  if (Array.isArray(b)) return Array.isArray(a) && a.length === b.length && b.every((x, i) => contem(a[i], x));
  return Object.keys(b).every((k) => (b[k] === undefined ? a[k] === undefined : contem(a[k], b[k])));
}
function expect(v) {
  const falha = (msg) => {
    throw new Error(msg);
  };
  const m = {
    toBe: (e) => Object.is(v, e) || falha(`esperava ${mostrar(e)}, veio ${mostrar(v)}`),
    toEqual: (e) => igual(v, e) || falha(`esperava igual a ${mostrar(e)}, veio ${mostrar(v)}`),
    toMatchObject: (e) => contem(v, e) || falha(`esperava conter ${mostrar(e)}, veio ${mostrar(v)}`),
    toHaveLength: (n) => v?.length === n || falha(`esperava tamanho ${n}, veio ${v?.length}`),
    toBeCloseTo: (e, casas = 2) => Math.abs(v - e) < 10 ** -casas / 2 || falha(`esperava ~${e}, veio ${v}`),
    toContain: (e) => v.includes(e) || falha(`esperava conter ${mostrar(e)} em ${mostrar(v)}`),
    toBeUndefined: () => v === undefined || falha(`esperava undefined, veio ${mostrar(v)}`),
  };
  m.not = { toContain: (e) => !v.includes(e) || falha(`não esperava ${mostrar(e)} em ${mostrar(v)}`) };
  return m;
}

export async function rodar() {
  const resultado = [];
  for (const t of testes) {
    try {
      await t.fn();
      resultado.push({ nome: t.nome, ok: true });
    } catch (err) {
      resultado.push({ nome: t.nome, ok: false, erro: String(err?.message ?? err) });
    }
  }
  return resultado;
}
