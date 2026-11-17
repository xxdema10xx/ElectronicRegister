// src/api/axiosClient.js
import axios from "axios";
import { API_BASE } from "../appConfig";
import { session } from "./session";
import { markHandled } from "../utils/handledErrors";

const axiosClient = axios.create({
  baseURL: API_BASE,
  timeout: 15000,
  headers: { "Content-Type": "application/json" },
});

// Chiamato quando il token istituzionale non è più valido (401) o la membership è stata sospesa/revocata:
// l'app torna al login. Riceve il messaggio da mostrare (assente = sessione scaduta).
let onUnauthorized = null;
export const REVOKED_MESSAGE = "Il tuo accesso a questo istituto è stato sospeso o revocato.";
export function setUnauthorizedHandler(fn) { onUnauthorized = fn; }
// Token per cui l'app è già tornata al login: gli errori delle richieste ancora in volo con quel token sono
// "già gestiti" (un solo dialog "Sessione terminata", nessun "Errore" in più dalle schermate)
const endedTokens = new Set();
const bearerOf = (config) => String(config?.headers?.Authorization || "").replace(/^Bearer\s+/i, "");
const isInstitutionRoute = (config) => /(^|\/)institution(\/|$)/i.test(String(config?.url || "").replace(/^https?:\/\/[^/]+/i, ""));

// Un 403 su /institution/** può voler dire "permesso mancante" oppure "membership sospesa/revocata".
// Si chiede una volta GET /Auth/context con lo stesso token: 401/403 → accesso non più valido (logout);
// 200 → normale permesso mancante. Una sola verifica alla volta, condivisa dai 403 arrivati in parallelo;
// la richiesta di verifica non passa dalla gestione del 401 (skipAuthHandling), quindi niente cicli.
let membershipCheck = null; // { token, promise }
function checkMembership(token) {
  if (membershipCheck?.token === token) return membershipCheck.promise;
  const promise = axiosClient.get("/Auth/context", { headers: { Authorization: `Bearer ${token}` }, skipAuthHandling: true })
    .then(() => false, (e) => e.status === 401 || e.status === 403)
    .then((revoked) => {
      if (revoked && onUnauthorized && session.accessToken === token && !endedTokens.has(token)) {
        endedTokens.add(token);
        onUnauthorized(REVOKED_MESSAGE);
      }
      return revoked;
    })
    .finally(() => { if (membershipCheck?.promise === promise) membershipCheck = null; });
  membershipCheck = { token, promise };
  return promise;
}

// Messaggi leggibili per gli errori che il backend restituisce solo con un titolo generico (docs/CONVENTIONS.md §2)
const STATUS_MESSAGES = {
  401: "Sessione scaduta: accedi di nuovo.",
  403: "Non hai i permessi per questa operazione (o la funzione non è attiva).",
  409: "Operazione non consentita nello stato attuale. Ricarica e riprova.",
  413: "Il file o la richiesta sono troppo grandi.",
  415: "Formato del file non supportato.",
  428: "Ricarica l'elemento e riprova.",
  429: "Troppe richieste: attendi un minuto e riprova.",
  500: "Errore interno del server.",
};
const GENERIC_TITLES = ["Operazione non consentita", "Richiesta non valida", "Non autorizzato", "Non autenticato", "Errore interno del server", "La risorsa non esiste"];
const TIMEOUT_MESSAGE = "Il server non risponde, riprova";
const NETWORK_MESSAGE = "Impossibile contattare il server. Controlla la connessione e riprova.";
const GATEWAY_MESSAGE = "Il server non è disponibile in questo momento, riprova tra poco.";
const NOT_FOUND_MESSAGE = "Elemento non trovato: potrebbe essere stato eliminato.";
const SERVER_ERROR_MESSAGE = "Si è verificato un errore sul server. Riprova più tardi.";
// Codici dei 409 con un significato preciso per qualsiasi schermata (account, inviti, membri…)
export const CONCURRENT_CHANGE_MESSAGE = "Modifica concorrente: ricarica e riprova.";
const CONFLICT_CODE_MESSAGES = { ConcurrentChange: CONCURRENT_CHANGE_MESSAGE };
const isHtml = (text) => /<\s*(!doctype|html|head|body)\b/i.test(text);

// Normalizza gli errori di rete/HTTP in un semplice Error con un messaggio
// leggibile, così tutte le schermate possono continuare a fare
// `Alert.alert("Errore", e.message)` esattamente come prima.
axiosClient.interceptors.response.use(
  (res) => res,
  async (error) => {
    const data = error.response?.data;
    const status = error.response?.status;
    const code = data && typeof data === "object" ? data.code : undefined;
    // ProblemDetails del backend: il testo utile è in "detail" solo se non è il titolo generico
    let problem = data && typeof data === "object" ? (data.detail || data.title || null) : null;
    if (problem && GENERIC_TITLES.includes(problem)) problem = null;
    if (data?.errors && typeof data.errors === "object") problem = "Dati non validi: controlla i campi.";
    const timedOut = error.code === "ECONNABORTED" || error.code === "ETIMEDOUT";
    // 5xx: il detail del server può contenere dettagli interni e non si mostra mai; si dà il codice di
    // correlazione (X-Correlation-ID) da comunicare all'assistenza
    const serverError = status >= 500;
    if (serverError) problem = null;
    const correlationId = error.response?.headers?.["x-correlation-id"] || error.response?.headers?.["X-Correlation-ID"];
    // Mai testo grezzo: errori di rete e risposte del gateway diventano un messaggio comprensibile.
    // Per i casi 404 e 409 degli esami le schermate usano utils/examErrors.js.
    const message =
      timedOut ? TIMEOUT_MESSAGE :
      !error.response ? NETWORK_MESSAGE :
      status >= 502 && status <= 504 ? GATEWAY_MESSAGE :
      (typeof data === "string" && isHtml(data)) ? GATEWAY_MESSAGE :
      status === 429 ? STATUS_MESSAGES[429] :
      serverError ? `${SERVER_ERROR_MESSAGE}${correlationId ? ` Codice per l'assistenza: ${correlationId}` : ""}` :
      status === 404 ? NOT_FOUND_MESSAGE :
      status === 409 && CONFLICT_CODE_MESSAGES[code] ? CONFLICT_CODE_MESSAGES[code] :
      (typeof data === "string" && data) ? data :
      problem ? problem :
      code ? `Errore: ${code}` :
      STATUS_MESSAGES[status] || (status === 400 ? "Dati non validi." : error.message || "Errore di rete");
    // errors: dettaglio della validazione (es. errori per riga del POST /grades/batch), mai per i 5xx
    const errors = !serverError && data && typeof data === "object" ? data.errors : undefined;
    const err = Object.assign(new Error(message), { status, problem, code, errors });
    if (error.config?.skipAuthHandling) return Promise.reject(err);
    const sentToken = bearerOf(error.config);
    if (sentToken && endedTokens.has(sentToken)) return Promise.reject(markHandled(err));
    if (status === 403 && onUnauthorized && sentToken && sentToken === session.accessToken && isInstitutionRoute(error.config)) {
      if (await checkMembership(sentToken)) return Promise.reject(markHandled(err));
      return Promise.reject(err);
    }
    if (status === 401 && onUnauthorized && session.accessToken && sentToken === session.accessToken) {
      endedTokens.add(sentToken);
      onUnauthorized();
      return Promise.reject(markHandled(err));
    }
    return Promise.reject(err);
  }
);

// Header per le API istituzionali: usa sempre il token corrente della sessione (che si rinnova da solo),
// il parametro resta per compatibilità con le chiamate esistenti.
export function authHeader(token) {
  const current = session.accessToken || token;
  return current ? { Authorization: `Bearer ${current}` } : {};
}

// Header con il ticket di selezione (account, scelta istituto)
export function ticketHeader(ticket) {
  return ticket ? { Authorization: `Bearer ${ticket}` } : {};
}

export default axiosClient;
