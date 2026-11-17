// src/constants/gradeScale.js
// Scala dei voti: trentesimi, solo interi, senza lode (il server rifiuta altri valori con 400). Unico punto in cui
// sono definiti limiti, sufficienza e messaggi: schermate, validazioni e colori usano queste costanti.
export const GRADE_MIN = 0;
export const GRADE_MAX = 30;
export const GRADE_PASS = 18; // voto >= 18: sufficiente
export const GRADE_NEAR_PASS = 15; // 15–17: vicino alla sufficienza (arancione)

export const GRADE_LABEL = `Voto (${GRADE_MIN}-${GRADE_MAX})`;
export const GRADE_PLACEHOLDER = `${GRADE_MIN}–${GRADE_MAX}`;
export const GRADE_ERROR = `Il voto deve essere un numero intero tra ${GRADE_MIN} e ${GRADE_MAX}.`;
// Tastiera numerica senza separatore decimale
export const GRADE_KEYBOARD = "number-pad";

// Testo inserito → intero, oppure null se non è un voto valido (vuoto, decimali, fuori scala, altri caratteri)
export function parseGrade(text) {
  const t = String(text ?? "").trim();
  if (!/^\d{1,2}$/.test(t)) return null;
  const n = Number(t);
  return n >= GRADE_MIN && n <= GRADE_MAX ? n : null;
}
export const isValidGrade = (text) => parseGrade(text) !== null;

// Colore del voto: verde se sufficiente, arancione vicino alla soglia, rosso sotto
export function gradeTone(value) {
  const n = Number(value);
  if (!Number.isFinite(n)) return "neutral";
  return n >= GRADE_PASS ? "pass" : n >= GRADE_NEAR_PASS ? "near" : "fail";
}
