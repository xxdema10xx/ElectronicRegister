// src/utils/dateUtils.js

// Converte un oggetto Date in stringa YYYY-MM-DD (formato atteso dal backend)
export function toISODate(d) {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

// Converte una stringa YYYY-MM-DD in oggetto Date, senza problemi di timezone
export function parseISODate(str) {
  if (!str) return new Date();
  const [y, m, d] = str.split("-").map(Number);
  if (!y || !m || !d) return new Date();
  return new Date(y, m - 1, d);
}

export function formatDateForDisplay(str) {
  if (!str) return "";
  const d = parseISODate(str);
  return d.toLocaleDateString("it-IT", { day: "2-digit", month: "2-digit", year: "numeric" });
}
