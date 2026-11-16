// src/api/session.js
// Stato della sessione verso il backend (vedi docs/AUTH.md):
//  - ticket di selezione: dal login, dura 5 minuti, serve per scegliere l'istituto e per l'account;
//  - token istituzionale: da select-institution, dura 15 minuti, serve per tutte le API /institution.
// Il backend non ha refresh token: per non interrompere l'utente (es. durante un esame) il token si
// rinnova poco prima della scadenza, con il ticket se ancora valido oppure rifacendo il login con le
// credenziali tenute SOLO in memoria (mai salvate: dopo un refresh della pagina non ci sono più).
//
// In parole semplici: questo file è il "portachiavi" dell'app. Tiene in memoria i gettoni di accesso
// e offre piccole funzioni per leggerli, salvarli e cancellarli. (Flusso completo: GUIDA §6)

// Quanto tempo PRIMA della scadenza si rinnova il token: 60 * 1000 millisecondi = 1 minuto.
const RENEW_MARGIN_MS = 60 * 1000;

// "state" è l'oggetto che contiene i dati veri. Non è esportato: gli altri file non lo toccano
// direttamente, ma solo tramite le funzioni di "session" qui sotto.
const state = {
  ticket: null,            // il ticket del login (null = non c'è)
  ticketExpiresAt: 0,      // quando scade il ticket (in millisecondi dal 1970, formato che usa Date.now())
  accessToken: null,       // il token per chiamare le API dell'istituto
  accessExpiresAt: 0,      // quando scade il token
  institution: null, // { institutionId, membershipId, name }  -> l'istituto scelto
  credentials: null, // { email, password } solo in memoria
};

// "session" è l'oggetto pubblico (export) con tutto ciò che gli altri file possono usare.
export const session = {
  // I "get" sono proprietà che si leggono come session.accessToken (senza parentesi) ma che
  // dietro le quinte eseguono una piccola funzione.
  get accessToken() { return state.accessToken; },
  get accessExpiresAt() { return state.accessExpiresAt; },
  get institution() { return state.institution; },
  get credentials() { return state.credentials; },
  // Il ticket è valido se esiste (!!) E la scadenza (meno 5 secondi di sicurezza) non è ancora passata.
  // Date.now() = ora attuale in millisecondi.
  hasValidTicket() { return !!state.ticket && Date.now() < state.ticketExpiresAt - 5000; },
  // Restituisce il ticket solo se ancora valido, altrimenti null (così nessuno usa un ticket scaduto).
  get ticket() { return this.hasValidTicket() ? state.ticket : null; },

  // Salva un nuovo ticket e calcola quando scade: ora + durata in secondi convertita in millisecondi (* 1000).
  setTicket(ticket, expiresInSeconds) {
    state.ticket = ticket;
    state.ticketExpiresAt = Date.now() + expiresInSeconds * 1000;
  },
  // Salva il token istituzionale e la sua scadenza; se è stato passato anche l'istituto, lo ricorda.
  setAccess(token, expiresInSeconds, institution) {
    state.accessToken = token;
    state.accessExpiresAt = Date.now() + expiresInSeconds * 1000;
    if (institution) state.institution = institution;
  },
  // Ripristina la sessione salvata (usato dopo un refresh della pagina web, vedi App.js).
  restore({ accessToken, accessExpiresAt, institution }) {
    state.accessToken = accessToken;
    state.accessExpiresAt = accessExpiresAt;
    state.institution = institution;
  },
  // Tiene email+password in memoria (non su disco!) per poter rifare il login in automatico.
  setCredentials(credentials) { state.credentials = credentials; },
  // Quanti millisecondi mancano al momento di rinnovare il token.
  // Math.max(0, …) evita numeri negativi: se siamo già in ritardo, rinnova subito (0).
  msUntilRenew() { return Math.max(0, state.accessExpiresAt - RENEW_MARGIN_MS - Date.now()); },
  // Dimentica tutto (logout): rimette ogni campo al valore iniziale.
  // Object.assign copia i valori elencati dentro "state".
  clear() {
    Object.assign(state, { ticket: null, ticketExpiresAt: 0, accessToken: null, accessExpiresAt: 0, institution: null, credentials: null });
  },
};
