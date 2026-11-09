// src/api/axiosClient.js
import axios from "axios";
import { API_BASE } from "../appConfig";
import { session } from "./session";

const axiosClient = axios.create({
  baseURL: API_BASE,
  timeout: 15000,
  headers: { "Content-Type": "application/json" },
});

// Chiamato quando il token istituzionale non è più valido (401): l'app torna al login.
let onUnauthorized = null;
export function setUnauthorizedHandler(fn) { onUnauthorized = fn; }

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
const isHtml = (text) => /<\s*(!doctype|html|head|body)\b/i.test(text);

// Normalizza gli errori di rete/HTTP in un semplice Error con un messaggio
// leggibile, così tutte le schermate possono continuare a fare
// `Alert.alert("Errore", e.message)` esattamente come prima.
axiosClient.interceptors.response.use(
  (res) => res,
  (error) => {
    const data = error.response?.data;
    const status = error.response?.status;
    const code = data && typeof data === "object" ? data.code : undefined;
    // ProblemDetails del backend: il testo utile è in "detail" solo se non è il titolo generico
    let problem = data && typeof data === "object" ? (data.detail || data.title || null) : null;
    if (problem && GENERIC_TITLES.includes(problem)) problem = null;
    if (data?.errors && typeof data.errors === "object") problem = "Dati non validi: controlla i campi.";
    const timedOut = error.code === "ECONNABORTED" || error.code === "ETIMEDOUT";
    // Mai testo grezzo: errori di rete e risposte del gateway diventano un messaggio comprensibile.
    // Per i casi 404 e 409 degli esami le schermate usano utils/examErrors.js.
    const message =
      timedOut ? TIMEOUT_MESSAGE :
      !error.response ? NETWORK_MESSAGE :
      status >= 502 && status <= 504 ? GATEWAY_MESSAGE :
      (typeof data === "string" && isHtml(data)) ? GATEWAY_MESSAGE :
      status === 404 ? NOT_FOUND_MESSAGE :
      (typeof data === "string" && data) ? data :
      problem ? problem :
      code ? `Errore: ${code}` :
      STATUS_MESSAGES[status] || (status === 400 ? "Dati non validi." : error.message || "Errore di rete");
    const sentToken = String(error.config?.headers?.Authorization || "");
    if (status === 401 && onUnauthorized && session.accessToken && sentToken.endsWith(session.accessToken)) onUnauthorized();
    return Promise.reject(Object.assign(new Error(message), { status, problem, code }));
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
