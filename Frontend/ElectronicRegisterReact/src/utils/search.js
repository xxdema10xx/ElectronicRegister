// src/utils/search.js
// Filtro "cerca per nome": corrisponde anche a pezzi di parola, senza distinguere maiuscole e accenti.
// Più parole devono comparire tutte (in qualsiasi ordine), es. "mar ros" trova "Mario Rossi".
const norm = (v) => String(v ?? "").normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();

export function filterBySearch(items, search, getFields) {
  const words = norm(search).split(/\s+/).filter(Boolean);
  if (words.length === 0) return items;
  return items.filter(item => {
    const haystack = norm(getFields(item).join(" "));
    return words.every(w => haystack.includes(w));
  });
}
