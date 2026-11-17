// src/utils/gradeErrors.js
// Messaggi per i 409 sui voti (docs/API.md §21). Il DTO dei voti non dice se un voto viene da un esame: un 409
// su modifica o eliminazione è il caso del voto d'esame ufficiale, che si cambia dalla correzione dell'esame.
export const GRADE_UPDATE_CONFLICT = "Il voto non si può modificare: è un voto generato da un esame (si modifica dalla correzione dell'esame), oppure la materia non è assegnata alla classe dello studente.";
export const GRADE_DELETE_CONFLICT = "Il voto non si può eliminare: è un voto generato da un esame (si modifica dalla correzione dell'esame).";
export const GRADE_CREATE_CONFLICT = "La materia non è assegnata alla classe dello studente, oppure lo studente non è in nessuna classe.";

// action: "update" | "delete" | "create"
export function gradeErrorMessage(action, error) {
  if (error?.status !== 409) return error?.message || "Si è verificato un errore.";
  return action === "update" ? GRADE_UPDATE_CONFLICT : action === "delete" ? GRADE_DELETE_CONFLICT : GRADE_CREATE_CONFLICT;
}
