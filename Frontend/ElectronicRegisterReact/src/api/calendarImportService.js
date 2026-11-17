// src/api/calendarImportService.js
// Import delle lezioni da file (XLSX/CSV) e pubblicazione del mese, lato amministratore (docs/API.md §24).
// Flusso del backend: anteprima → conferma (lezioni in bozza) → pubblicazione del mese per ogni classe.
// Docenti e studenti vedono solo le lezioni pubblicate.
import { Platform } from "react-native";
import * as Crypto from "expo-crypto";
import axiosClient, { authHeader } from "./axiosClient";

const BASE = "/institution/calendar";
const PAGE_SIZE = 100; // massimo ammesso da CalendarQuery.size
const UPLOAD_TIMEOUT = 60000; // anteprima e conferma rileggono tutto il file: più del timeout standard

// Ogni scrittura sul calendario porta un eventId nuovo (il backend lo usa contro i doppi invii)
export const newEventId = () => globalThis.crypto?.randomUUID?.() ?? Crypto.randomUUID();

export const IMPORT_MODES = [
  { id: "AppendDraft", label: "Aggiungi alle bozze del mese" },
  { id: "ReplaceDraftScope", label: "Sostituisci le bozze del mese" },
];

const pad = (n) => String(n).padStart(2, "0");
export function monthRange(year, month) {
  const last = new Date(year, month, 0).getDate();
  return { from: `${year}-${pad(month)}-01`, to: `${year}-${pad(month)}-${pad(last)}` };
}

// Impostazioni del calendario: null se non ancora configurato (409 calendar.settings-required)
export async function getCalendarSettings(token) {
  try {
    return (await axiosClient.get(`${BASE}/settings`, { headers: authHeader(token) })).data;
  } catch (e) {
    if (e.code === "calendar.settings-required") return null;
    throw e;
  }
}

// file: asset di expo-document-picker. Su web c'è il File del browser, sull'app uri/nome/tipo.
function appendFile(form, file) {
  if (Platform.OS === "web" && file.file) form.append("file", file.file, file.name);
  else form.append("file", { uri: file.uri, name: file.name || "lezioni.xlsx", type: file.mimeType || "application/octet-stream" });
}

// Anteprima: il backend accetta solo i campi "file", "settings" e (facoltativo) "parsing"
export async function previewImport({ file, year, month, classIds, mode, reason }, token) {
  const form = new FormData();
  appendFile(form, file);
  form.append("settings", JSON.stringify({ eventId: newEventId(), year, month, classIds, mode, mapping: null, reason: reason.trim() }));
  const { data } = await axiosClient.post(`${BASE}/imports/preview`, form, {
    // multipart esplicito: con il Content-Type JSON predefinito axios convertirebbe il FormData in JSON
    headers: { ...authHeader(token), "Content-Type": "multipart/form-data" },
    timeout: UPLOAD_TIMEOUT,
  });
  return data;
}

// Conferma: crea le lezioni in bozza. revision e digest sono quelli dell'anteprima.
export async function confirmImport(preview, { acknowledgeWarnings, reason }, token) {
  const body = { eventId: newEventId(), expectedRevision: preview.revision, digest: preview.digest, acknowledgeWarnings: !!acknowledgeWarnings, reason: reason.trim() };
  const { data } = await axiosClient.post(`${BASE}/imports/${preview.id}/confirm`, body, { headers: authHeader(token), timeout: UPLOAD_TIMEOUT });
  return data; // CalendarReceipt: lessonIds
}

// Periodo (classe, mese): null se per quel mese la classe non ha ancora lezioni (404)
export async function getPeriod(classId, year, month, token) {
  try {
    return (await axiosClient.get(`${BASE}/months/${year}/${month}/classes/${classId}`, { headers: authHeader(token) })).data;
  } catch (e) {
    if (e.status === 404) return null;
    throw e;
  }
}

export async function publishPeriod(classId, year, month, expectedRevision, reason, token) {
  const body = { eventId: newEventId(), expectedRevision, reason: reason.trim() };
  return (await axiosClient.post(`${BASE}/months/${year}/${month}/classes/${classId}/publish`, body, { headers: authHeader(token) })).data;
}

// Numero di lezioni (non annullate) della classe nel mese, per stato di pubblicazione
async function countLessons(classId, year, month, publication, token) {
  let total = 0;
  for (let page = 1; ; page++) {
    const { data } = await axiosClient.get(`${BASE}/classes/${classId}`, {
      headers: authHeader(token), params: { ...monthRange(year, month), publication, page, size: PAGE_SIZE },
    });
    const n = Array.isArray(data) ? data.length : 0;
    total += n;
    if (n < PAGE_SIZE) return total;
  }
}

// Stato del mese per una classe: periodo, lezioni in bozza e pubblicate
export async function getMonthStatus(classId, year, month, token) {
  const [period, drafts, published] = await Promise.all([
    getPeriod(classId, year, month, token),
    countLessons(classId, year, month, "Draft", token),
    countLessons(classId, year, month, "Published", token),
  ]);
  return { period, drafts, published };
}

// ── Messaggi ────────────────────────────────────────────────────────────────────────────────
const CODE_MESSAGES = {
  "calendar.settings-required": "Il calendario dell'istituto non è ancora configurato (fuso orario e scadenze).",
  "calendar.invalid-import-settings": "Impostazioni dell'import non valide.",
  "calendar.exact-scope-required": "Scegli almeno una classe (al massimo 100).",
  "calendar.staging-limit": "Il file deve contenere da 1 a 2000 righe di lezioni.",
  "calendar.preview-limit": "Hai troppe anteprime aperte: confermane una o attendi 30 minuti che scadano.",
  "calendar.invalid-mapping": "Le intestazioni delle colonne non sono valide.",
  "calendar.invalid-row-width": "Alcune righe hanno un numero di colonne diverso dall'intestazione.",
  "calendar.reason-required": "Indica una motivazione (al massimo 500 caratteri).",
  "calendar.preview-expired-or-consumed": "L'anteprima è scaduta o è già stata confermata: generane una nuova.",
  "calendar.preview-digest-conflict": "L'anteprima non è più valida: generane una nuova.",
  "calendar.preview-stale-or-invalid": "Nel frattempo il calendario è cambiato: genera una nuova anteprima.",
  "calendar.warnings-not-acknowledged": "Conferma di aver letto gli avvisi prima di procedere.",
  "calendar.revision-conflict": "Il mese è stato modificato nel frattempo: ricarica e riprova.",
  "calendar.no-draft-lessons": "Non ci sono lezioni in bozza da pubblicare per questo mese.",
  "calendar.teaching-configuration-required": "La classe non ha un piano formativo pubblicato.",
  FileTooLarge: "Il file è troppo grande (massimo 10 MB).",
  UnsupportedFormat: "Formato non supportato: usa un file .xlsx o .csv.",
  NoDataRows: "Il file non contiene righe di dati.",
  InvalidEncoding: "Il file CSV non è in UTF-8.",
  UnsupportedEncoding: "Codifica del file non supportata.",
  DelimiterNotDetected: "Separatore del CSV non riconosciuto: usa il punto e virgola o la virgola.",
  AmbiguousDelimiter: "Separatore del CSV ambiguo: usa un solo tipo di separatore.",
  StageTooLarge: "Il file contiene troppi dati.",
};

export function calendarErrorMessage(e) {
  if (e?.code && CODE_MESSAGES[e.code]) return CODE_MESSAGES[e.code];
  if (e?.status === 404) return "Il calendario non è attivo per questo istituto, o l'elemento non esiste.";
  if (e?.status === 409 && e?.code) return `Operazione non consentita (${e.code}).`;
  return e?.message || "Errore imprevisto.";
}

const ISSUE_MESSAGES = {
  RequiredColumnMissing: "Colonna obbligatoria mancante",
  RequiredFieldMissing: "Valore obbligatorio mancante",
  InvalidValue: "Valore non valido",
  IgnoredColumn: "Colonna non riconosciuta, verrà ignorata",
  AmbiguousMapping: "Più colonne per lo stesso campo",
  DuplicateWithinFile: "Lezione duplicata nel file",
  "calendar.invalid-date": "Data non valida: usa AAAA-MM-GG",
  "calendar.invalid-time": "Ora non valida: usa HH:mm",
  "calendar.same-day-positive-interval-required": "L'ora di fine deve essere dopo l'ora di inizio",
  "calendar.class-unavailable": "Classe non trovata",
  "calendar.ambiguous-class": "Più classi con questo nome",
  "calendar.row-outside-scope": "Classe non tra quelle selezionate",
  "calendar.module-unavailable": "Modulo non trovato nel piano formativo della classe",
  "calendar.ambiguous-module": "Più moduli con questo nome",
  "calendar.teacher-unavailable": "Docente non trovato: usa l'ID del docente",
  "calendar.row-outside-month": "Data fuori dal mese selezionato",
  "calendar.teaching-configuration-required": "La classe non ha un piano formativo pubblicato",
};

const GENERIC_ISSUES = ["RequiredColumnMissing", "RequiredFieldMissing", "InvalidValue", "AmbiguousMapping"];
const FIELD_LABELS = { Date: "Data", StartsAt: "Ora inizio", EndsAt: "Ora fine", Class: "Classe", Module: "Modulo", Teacher: "Docente", Room: "Aula", Description: "Descrizione", ExternalReference: "Riferimento esterno" };

export function issueMessage(issue, columns) {
  const text = ISSUE_MESSAGES[issue.code] || issue.message || issue.code;
  // Per le colonne ignorate "field" è l'indice della colonna
  if (issue.code === "IgnoredColumn" && columns && /^\d+$/.test(issue.field)) return `${text}: "${columns[Number(issue.field)]}"`;
  // Il nome del campo si aggiunge solo ai messaggi generici ("Data: valore non valido"), non a quelli che lo dicono già
  const field = ISSUE_MESSAGES[issue.code] && !GENERIC_ISSUES.includes(issue.code) ? null : FIELD_LABELS[issue.field];
  return field ? `${field}: ${text.charAt(0).toLowerCase()}${text.slice(1)}` : text;
}

// CalendarConflictKind arriva come numero (enum senza convertitore) o, in futuro, come nome
const CONFLICT_LABELS = { 0: "Lezione già presente", 1: "Sovrapposta a un'altra lezione della classe", 2: "Il docente ha già un'altra lezione", Duplicate: "Lezione già presente", ClassOverlap: "Sovrapposta a un'altra lezione della classe", TeacherOverlap: "Il docente ha già un'altra lezione" };
export const conflictMessage = (c) => CONFLICT_LABELS[c.kind] || "Conflitto di orario";
