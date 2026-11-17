// src/api/session.js
// Stato della sessione verso il backend (vedi docs/AUTH.md):
//  - ticket di selezione: dal login, dura 5 minuti, serve per scegliere l'istituto e per l'account;
//  - token istituzionale: da select-institution, dura 15 minuti, serve per tutte le API /institution.
// Il backend non ha refresh token: per non interrompere l'utente (es. durante un esame) il token si
// rinnova poco prima della scadenza, con il ticket se ancora valido oppure rifacendo il login con le
// credenziali tenute SOLO in memoria (mai salvate: dopo un refresh della pagina non ci sono più).

const RENEW_MARGIN_MS = 60 * 1000;

const state = {
  ticket: null,
  ticketExpiresAt: 0,
  accessToken: null,
  accessExpiresAt: 0,
  institution: null, // { institutionId, membershipId, name }
  credentials: null, // { email, password } solo in memoria
};

export const session = {
  get accessToken() { return state.accessToken; },
  get accessExpiresAt() { return state.accessExpiresAt; },
  get institution() { return state.institution; },
  get credentials() { return state.credentials; },
  hasValidTicket() { return !!state.ticket && Date.now() < state.ticketExpiresAt - 5000; },
  get ticket() { return this.hasValidTicket() ? state.ticket : null; },

  setTicket(ticket, expiresInSeconds) {
    state.ticket = ticket;
    state.ticketExpiresAt = Date.now() + expiresInSeconds * 1000;
  },
  setAccess(token, expiresInSeconds, institution) {
    state.accessToken = token;
    state.accessExpiresAt = Date.now() + expiresInSeconds * 1000;
    if (institution) state.institution = institution;
  },
  restore({ accessToken, accessExpiresAt, institution }) {
    state.accessToken = accessToken;
    state.accessExpiresAt = accessExpiresAt;
    state.institution = institution;
  },
  setCredentials(credentials) { state.credentials = credentials; },
  msUntilRenew() { return Math.max(0, state.accessExpiresAt - RENEW_MARGIN_MS - Date.now()); },
  clear() {
    Object.assign(state, { ticket: null, ticketExpiresAt: 0, accessToken: null, accessExpiresAt: 0, institution: null, credentials: null });
  },
};
