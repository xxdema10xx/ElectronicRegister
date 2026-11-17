// src/utils/userName.js
// Nome da mostrare per l'utente collegato. Mai una stringa vuota.
// TODO(API): per admin e staff non esiste un profilo con nome e cognome: resta "Nome visualizzato" o l'email.

// Senza spazi e con punti, trattini bassi o cifre: è un identificativo tecnico, non il nome di una persona
const TECHNICAL = /^\S*[._@\d]\S*$/;

export function userDisplayName(user) {
  const profile = [user?.studentFirstName || user?.teacherFirstName, user?.studentLastName || user?.teacherLastName].filter(Boolean).join(" ").trim();
  if (profile) return profile;
  const shown = user?.displayName?.trim();
  if (shown && !TECHNICAL.test(shown)) return shown;
  return user?.email?.split("@")[0]?.trim() || "Utente";
}
