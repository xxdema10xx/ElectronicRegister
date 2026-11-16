// src/api/axiosClient.js
// Il "postino" dell'app: tutte le richieste al backend passano da qui. (vedi GUIDA §4.5)
// Fa tre cose: 1) sa a quale indirizzo spedire, 2) traduce gli errori in frasi leggibili,
// 3) se il backend dice "non sei autenticato" riporta l'utente al login.

// axios = libreria che invia le richieste HTTP.
import axios from "axios";
// API_BASE = indirizzo base del backend (es. http://127.0.0.1:5000), letto dalla configurazione.
import { API_BASE } from "../appConfig";
// session = il portachiavi con i token (vedi session.js).
import { session } from "./session";

// Crea un "postino" già configurato:
const axiosClient = axios.create({
  baseURL: API_BASE,                                  // tutti gli indirizzi partono da qui
  timeout: 15000,                                     // dopo 15 secondi senza risposta si rinuncia
  headers: { "Content-Type": "application/json" },    // si dichiara che i dati inviati sono in formato JSON
});

// Chiamato quando il token istituzionale non è più valido (401): l'app torna al login.
// La variabile parte vuota; App.js ci mette dentro la funzione giusta tramite setUnauthorizedHandler.
let onUnauthorized = null;
export function setUnauthorizedHandler(fn) { onUnauthorized = fn; }

// Messaggi leggibili per gli errori che il backend restituisce solo con un titolo generico (docs/CONVENTIONS.md §2)
// Tabella "codice di stato HTTP → frase per l'utente". (codici: GUIDA §5.4)
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
// Titoli troppo generici per essere utili all'utente: se il backend manda solo questi, si ignorano.
const GENERIC_TITLES = ["Operazione non consentita", "Richiesta non valida", "Non autorizzato", "Non autenticato", "Errore interno del server", "La risorsa non esiste"];
// Frasi fisse per i casi di rete.
const TIMEOUT_MESSAGE = "Il server non risponde, riprova";
const NETWORK_MESSAGE = "Impossibile contattare il server. Controlla la connessione e riprova.";
const GATEWAY_MESSAGE = "Il server non è disponibile in questo momento, riprova tra poco.";
const NOT_FOUND_MESSAGE = "Elemento non trovato: potrebbe essere stato eliminato.";
// Funzione che controlla se un testo sembra una pagina HTML (segno che a rispondere non è il backend ma un gateway).
// /.../i è una "espressione regolare": un modello di ricerca nel testo; la i = ignora maiuscole/minuscole.
const isHtml = (text) => /<\s*(!doctype|html|head|body)\b/i.test(text);

// Normalizza gli errori di rete/HTTP in un semplice Error con un messaggio
// leggibile, così tutte le schermate possono continuare a fare
// `Alert.alert("Errore", e.message)` esattamente come prima.
// "interceptors.response.use" = "metti un controllore su ogni risposta": il primo pezzo lascia passare
// le risposte riuscite, il secondo si occupa degli errori.
axiosClient.interceptors.response.use(
  (res) => res,   // risposta ok: la si restituisce così com'è
  (error) => {    // risposta in errore: la si analizza
    // "?." evita crash se qualcosa non esiste (vedi GUIDA §4.1).
    const data = error.response?.data;     // il contenuto della risposta di errore
    const status = error.response?.status; // il codice numerico (404, 500…)
    // Il backend può inviare un campo "code" con un codice tecnico dell'errore.
    const code = data && typeof data === "object" ? data.code : undefined;
    // ProblemDetails del backend: il testo utile è in "detail" solo se non è il titolo generico
    let problem = data && typeof data === "object" ? (data.detail || data.title || null) : null;
    if (problem && GENERIC_TITLES.includes(problem)) problem = null;
    // Se ci sono errori di validazione sui campi, si usa un messaggio unico.
    if (data?.errors && typeof data.errors === "object") problem = "Dati non validi: controlla i campi.";
    // Vero se la richiesta è scaduta per tempo massimo.
    const timedOut = error.code === "ECONNABORTED" || error.code === "ETIMEDOUT";
    // Mai testo grezzo: errori di rete e risposte del gateway diventano un messaggio comprensibile.
    // Per i casi 404 e 409 degli esami le schermate usano utils/examErrors.js.
    // Catena di "? :" (vedi GUIDA §4.1): si prova la prima condizione, se falsa la seconda, e così via;
    // vince la prima che è vera.
    const message =
      timedOut ? TIMEOUT_MESSAGE :                                       // tempo scaduto
      !error.response ? NETWORK_MESSAGE :                                // nessuna risposta: manca la rete
      status >= 502 && status <= 504 ? GATEWAY_MESSAGE :                 // server intermedio non disponibile
      (typeof data === "string" && isHtml(data)) ? GATEWAY_MESSAGE :     // risposta HTML al posto di dati
      status === 404 ? NOT_FOUND_MESSAGE :                               // elemento inesistente
      (typeof data === "string" && data) ? data :                        // il backend ha mandato già un testo
      problem ? problem :                                                // dettaglio dal backend
      code ? `Errore: ${code}` :                                         // solo il codice tecnico
      STATUS_MESSAGES[status] || (status === 400 ? "Dati non validi." : error.message || "Errore di rete"); // ultimo ripiego
    // Il token con cui era stata inviata la richiesta fallita.
    const sentToken = String(error.config?.headers?.Authorization || "");
    // Se è un 401 e la richiesta usava il token ATTUALE, la sessione è davvero finita: si avvisa App.js (→ login).
    // (Il controllo sul token evita di fare logout per una vecchia richiesta partita con un token già sostituito.)
    if (status === 401 && onUnauthorized && session.accessToken && sentToken.endsWith(session.accessToken)) onUnauthorized();
    // Si "rifiuta" la promessa con un Error leggibile, a cui si allegano status, problem e code per chi vuole usarli.
    return Promise.reject(Object.assign(new Error(message), { status, problem, code }));
  }
);

// Header per le API istituzionali: usa sempre il token corrente della sessione (che si rinnova da solo),
// il parametro resta per compatibilità con le chiamate esistenti.
// Un "header" è un'informazione allegata alla richiesta, come il mittente su una busta.
export function authHeader(token) {
  const current = session.accessToken || token;
  // "Bearer <token>" è il formato standard per dire "ecco il mio lasciapassare".
  return current ? { Authorization: `Bearer ${current}` } : {};
}

// Header con il ticket di selezione (account, scelta istituto)
export function ticketHeader(ticket) {
  return ticket ? { Authorization: `Bearer ${ticket}` } : {};
}

// Esporta il postino come elemento principale del file: gli altri file lo importano per fare le chiamate.
export default axiosClient;
