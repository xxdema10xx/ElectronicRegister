// src/utils/plural.js
// Conteggi con il plurale giusto: plural(1, "lezione", "lezioni") → "1 lezione", plural(3, …) → "3 lezioni".
// Unico helper per tutti i conteggi dell'interfaccia (materie, studenti, lezioni, ore…).
export function plural(n, one, many) {
  const count = Number(n) || 0;
  return `${count} ${count === 1 ? one : many}`;
}
