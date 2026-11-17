// src/utils/examErrors.js
// Messaggi per gli errori delle azioni sugli esami.
// TODO(API): il server risponde ai 404 e 409 con un titolo generico, senza il motivo: il messaggio si
// ricava dall'azione tentata.
const NOT_FOUND = "L'esame non esiste più.";
const CONFLICT = {
  publish: "Non puoi pubblicare l'esame ora: controlla che abbia almeno uno studente assegnato e che la disponibilità non sia già terminata.",
  close: "Ci sono consegne in corso: non puoi chiudere l'esame ora.",
  cancel: "Non puoi annullare l'esame: almeno uno studente lo ha già iniziato.",
  remove: "Non puoi eliminare l'esame: ha già delle consegne o un recupero collegato.",
  save: "L'esame non è più in bozza: non si può modificare.",
  start: "L'esame non è disponibile in questo momento.",
};

export function examErrorMessage(action, error) {
  if (error?.status === 404) return NOT_FOUND;
  if (error?.status === 409 && CONFLICT[action]) return CONFLICT[action];
  return error?.message || "Si è verificato un errore.";
}
