// src/utils/plural.js
// Conteggi con il plurale giusto: plural(1, "lezione", "lezioni") → "1 lezione", plural(3, …) → "3 lezioni".
// Unico helper per tutti i conteggi dell'interfaccia (materie, studenti, lezioni, domande, punti…).
export function pluralWord(n, one, many) {
  return Number(n) === 1 ? one : many;
}

export function plural(n, one, many) {
  const count = Number(n) || 0;
  return `${count} ${pluralWord(count, one, many)}`;
}
