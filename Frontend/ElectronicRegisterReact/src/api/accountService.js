// src/api/accountService.js
// Account di accesso creati dall'istituto (POST /api/institution/accounts, docs/api/PLATFORM_IDENTITY_ACCOUNTS.md).
// L'amministratore crea account di ogni ruolo, la segreteria solo Student o Teacher. L'email la genera il
// server; la password temporanea arriva una volta sola nella risposta e va cambiata al primo accesso.
import * as Crypto from "expo-crypto";
import axiosClient, { CONCURRENT_CHANGE_MESSAGE, authHeader } from "./axiosClient";

export const newEventId = () => globalThis.crypto?.randomUUID?.() ?? Crypto.randomUUID();

export const ACCOUNT_ROLES = [
  { id: "Student", label: "Studente" },
  { id: "Teacher", label: "Docente" },
  { id: "Staff", label: "Segreteria" },
  { id: "InstitutionAdmin", label: "Amministratore" },
];

// Codice fiscale personale: 16 caratteri (con omocodia). Il controllo completo lo fa il server.
const FISCAL_CODE = /^[A-Z]{6}[0-9LMNPQRSTUV]{2}[A-EHLMPR-T][0-9LMNPQRSTUV]{2}[A-Z][0-9LMNPQRSTUV]{3}[A-Z]$/;
export const normalizeFiscalCode = (v) => String(v || "").replace(/\s+/g, "").toUpperCase();

// Controlli prima dell'invio, con gli stessi limiti del server. Restituisce { campo: messaggio }.
export function accountFormErrors({ firstName, lastName, fiscalCode, roles, reason }) {
  const e = {};
  if (!firstName?.trim()) e.firstName = "Inserisci il nome.";
  else if (firstName.trim().length > 100) e.firstName = "Al massimo 100 caratteri.";
  if (!lastName?.trim()) e.lastName = "Inserisci il cognome.";
  else if (lastName.trim().length > 100) e.lastName = "Al massimo 100 caratteri.";
  if (!FISCAL_CODE.test(normalizeFiscalCode(fiscalCode))) e.fiscalCode = "Codice fiscale non valido: servono 16 caratteri.";
  if (!roles?.length) e.roles = "Scegli almeno un ruolo.";
  else if (roles.includes("Student") && roles.includes("Teacher")) e.roles = "Studente e docente non possono stare nello stesso account.";
  if (!reason?.trim()) e.reason = "Indica la motivazione (resta nello storico).";
  else if (reason.trim().length > 500) e.reason = "Al massimo 500 caratteri.";
  return e;
}

// eventId: uno per intenzione. Ripetendo la stessa richiesta (risposta persa, 503) si riusa lo stesso.
// Risposta: { userId, membershipId, email, roles, studentId?, teacherId?, replayed, temporaryPassword }
export async function createAccount({ eventId, firstName, lastName, fiscalCode, roles, classId, studentId, teacherId, reason }, token) {
  const body = {
    eventId, firstName: firstName.trim(), lastName: lastName.trim(), fiscalCode: normalizeFiscalCode(fiscalCode),
    roles, classId: classId || null, studentId: studentId || null, teacherId: teacherId || null, reason: reason.trim(),
  };
  const res = await axiosClient.post("/institution/accounts", body, { headers: authHeader(token) });
  return res.data;
}

// Messaggio per gli errori della creazione (409 e 422 hanno significati precisi in questo endpoint)
export function accountErrorMessage(e) {
  if (e?.status === 404) return "La creazione degli account non è attiva su questo server, oppure la scheda collegata non esiste.";
  if (e?.status === 422) return "Nome e cognome non coincidono con quelli della scheda collegata.";
  if (e?.status === 409) {
    // Creazione in conflitto con un'altra modifica contemporanea: si ricarica e si riprova
    if (e.code === "ConcurrentChange") return CONCURRENT_CHANGE_MESSAGE;
    return e.problem && /email/i.test(e.problem)
      ? "Non ci sono più indirizzi email disponibili per questo nome: contatta l'amministratore della piattaforma."
      : "Questa persona è già nell'istituto, oppure la scheda è già collegata a un altro account.";
  }
  if (e?.status === 503) return "Il server è occupato: riprova tra qualche secondo (la richiesta non è stata registrata).";
  return e?.message || "Errore imprevisto.";
}
