// src/api/calendarImportService.js
// Import delle lezioni da file (XLSX/CSV) e pubblicazione del mese, lato amministratore (docs/API.md §24).
// Flusso del backend: anteprima → conferma (lezioni in bozza) → pubblicazione del mese per ogni classe.
// Docenti e studenti vedono solo le lezioni pubblicate.
import { Platform } from "react-native";
import * as Crypto from "expo-crypto";
import axiosClient, { authHeader } from "./axiosClient";
import { CALENDAR_MAX_UPLOAD_MB } from "../appConfig";
import { HOURS_WARNING_LABELS } from "./calendarService";

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

// ── Messaggi ────
export const TEACHING_PLAN_MESSAGE = "La classe non ha un piano formativo pubblicato: chiedi all'amministrazione di pubblicarlo, poi riprova.";
export const FILE_TOO_LARGE_MESSAGE = "File troppo grande per il server: dividilo per classe o salvalo in CSV.";
export const MAX_UPLOAD_BYTES = CALENDAR_MAX_UPLOAD_MB * 1024 * 1024;
export const maxUploadLabel = () => `${String(CALENDAR_MAX_UPLOAD_MB).replace(".", ",")} MB`;
export const DELIMITER_MESSAGE = "Separatore del CSV non riconosciuto: usa il punto e virgola o la virgola.";
export const NO_LESSONS_MESSAGE = "Il file non contiene lezioni.";

// ────────────────────────────────────────────────────────────────────────────────
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
  "calendar.teaching-configuration-required": TEACHING_PLAN_MESSAGE,
  "calendar.published-plan-required": TEACHING_PLAN_MESSAGE,
  // Comandi sulle lezioni (annulla, sposta, sostituisci)
  "calendar.event-payload-conflict": "La lezione è stata modificata nel frattempo.",
  "calendar.scheduled-required": "Si possono modificare solo le lezioni programmate, non quelle annullate o svolte.",
  "calendar.published-scheduled-required": "Si possono modificare solo le lezioni programmate e pubblicate.",
  "calendar.invalid-lesson-state": "La lezione non si può modificare nel suo stato attuale.",
  "calendar.overlap": "Nel nuovo orario la classe o il docente hanno già un'altra lezione.",
  "calendar.teacher-unavailable": "Il docente scelto non è disponibile in questo istituto.",
  "calendar.invalid-substitution": "Il docente scelto non può sostituire quello attuale.",
  "calendar.same-day-positive-interval-required": "L'ora di fine deve essere dopo l'ora di inizio, nello stesso giorno.",
  "calendar.nonexistent-local-time": "Orario inesistente per il cambio dell'ora legale: scegli un altro orario.",
  "calendar.ambiguous-local-time": "Orario ambiguo per il cambio dell'ora legale: scegli un altro orario.",
  "calendar.minute-precision-required": "Gli orari vanno indicati al minuto (HH:mm).",
  "calendar.invalid-date": "Data non valida: usa AAAA-MM-GG.",
  "calendar.invalid-time": "Ora non valida: usa HH:mm.",
  "calendar.database-conflict": "Il calendario è occupato da un'altra operazione: riprova.",
  "calendar.range-limit": "Intervallo troppo ampio: al massimo 366 giorni.",
  "calendar.invalid-text": "Un testo contiene caratteri non ammessi o è troppo lungo.",
  "calendar.reference-required": "Manca un riferimento obbligatorio (classe, modulo o docente).",
  "calendar.inconsistent-module-subject": "Il modulo non corrisponde alla materia della classe: controlla il piano formativo.",
  "calendar.inconsistent-plan-binding": "Il piano formativo della classe è cambiato: ricarica e riprova.",
  "calendar.invalid-plan-modules": "Il piano formativo della classe non ha moduli validi.",
  "calendar.duplicate-identity": "Questa operazione è già stata registrata.",
  "calendar.draft-required": "L'operazione è possibile solo sulle lezioni in bozza.",
  "calendar.completed-required": "L'operazione è possibile solo sulle lezioni svolte.",
  "calendar.invalid-delivered-minutes": "Minuti svolti non validi: da 1 a 1440.",
  "calendar.invalid-recovery": "La lezione di recupero indicata non è valida.",
  "calendar.invalid-period": "Mese non valido.",
  // Impostazioni
  "calendar.invalid-time-zone": "Fuso orario non riconosciuto dal server (es. Europe/Rome).",
  "calendar.invalid-approval-lead-days": "I giorni di anticipo devono essere tra 0 e 366.",
  "calendar.invalid-lead-days": "I giorni di anticipo devono essere tra 0 e 366.",
  FileTooLarge: FILE_TOO_LARGE_MESSAGE,
  UnsupportedFormat: "Formato non supportato: usa un file .xlsx o .csv.",
  NoDataRows: "Il file non contiene righe di dati.",
  InvalidEncoding: "Il file CSV non è in UTF-8.",
  UnsupportedEncoding: "Codifica del file non supportata.",
  DelimiterNotDetected: DELIMITER_MESSAGE,
  AmbiguousDelimiter: "Separatore del CSV ambiguo: usa un solo tipo di separatore.",
  StageTooLarge: "Il file contiene troppi dati.",
  // Lettore dei file del backend (InstitutionImportFileReader*): rifiuti prima dell'anteprima
  EmptyFile: "Il file è vuoto.",
  MalformedCsv: "Il CSV non è leggibile: controlla virgolette e separatori.",
  InvalidHeader: "Intestazioni non riconosciute: usa il modello.",
  ColumnCountMismatch: "Alcune righe hanno un numero di colonne diverso dalle intestazioni: controlla il separatore.",
  InvalidDelimiter: DELIMITER_MESSAGE,
  InvalidParsingOptions: "Opzioni di lettura del file non valide.",
  RecordTooLong: "Una riga del file è troppo lunga.",
  CellTooLong: "Una cella del file è troppo lunga (al massimo 512 caratteri).",
  TooManyColumns: "Il file ha troppe colonne (al massimo 16).",
  TooManyRows: "Il file ha troppe righe (al massimo 2000).",
  MalformedWorkbook: "Il file Excel è danneggiato o non è un .xlsx valido.",
  UnsupportedWorkbook: "Il file Excel usa funzioni non supportate: salvalo in CSV.",
  HostileWorkbook: "Il file Excel non è sicuro o è troppo complesso: salvalo in CSV.",
  ZipLimitExceeded: "Il file Excel è troppo complesso: salvalo in CSV.",
  XmlLimitExceeded: "Il file Excel è troppo complesso: salvalo in CSV.",
  TooManySharedStrings: "Il file Excel è troppo complesso: salvalo in CSV.",
  FormulaNotAllowed: "Il file Excel contiene formule: sostituiscile con i valori.",
  ExternalReferenceNotAllowed: "Il file Excel contiene collegamenti esterni: rimuovili.",
  WorksheetSelectionRequired: "Il file Excel ha più fogli: lasciane uno solo.",
  UnsupportedCellType: "Il file Excel contiene celle di tipo non supportato: scrivi date e ore come testo.",
  AmbiguousCalendarCell: "Data o ora numerica senza formato: scrivila come testo (AAAA-MM-GG, HH:mm) o in formato data/ora di Excel.",
};

// Codice sconosciuto: mai il codice grezzo come messaggio. Testo generico, il codice resta tra parentesi.
export const UNKNOWN_ERROR_MESSAGE = "Il server ha rifiutato l'operazione.";

// ctx.tabSeparated: il file usa il tab. Il backend lo rifiuta come InvalidHeader (l'intestazione intera
// diventa una sola colonna con dei tab), ma il problema vero è il separatore.
export function calendarErrorMessage(e, ctx = {}) {
  if (e?.status === 413) return FILE_TOO_LARGE_MESSAGE;
  if (e?.code === "InvalidHeader" && ctx.tabSeparated) return DELIMITER_MESSAGE;
  if (e?.code && CODE_MESSAGES[e.code]) return CODE_MESSAGES[e.code];
  if (e?.status === 403) return "Non hai i permessi per questa operazione sul calendario (o il calendario non è attivo per il tuo ruolo).";
  if (e?.status === 404) return "Il calendario non è attivo per questo istituto, o l'elemento non esiste.";
  if (e?.status === 409 && e?.code) return `Operazione non consentita (${e.code}).`;
  if (e?.code) return `${UNKNOWN_ERROR_MESSAGE} (${e.code})`;
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
  "calendar.teaching-configuration-required": "La classe non ha un piano formativo pubblicato: chiedilo all'amministrazione",
  // Nel file non c'è un motivo di sostituzione: un docente diverso dal titolare della materia scarta la riga
  "calendar.reason-required": "Il docente non è il titolare di questa materia nella classe: importa la lezione con il titolare e poi usa «Sostituisci docente»",
  "calendar.class-subject-required": "La materia del modulo non è assegnata a questa classe",
  "calendar.module-or-class-subject-unavailable": "Modulo o materia non disponibili per questa classe",
  "calendar.under-scheduled": HOURS_WARNING_LABELS["calendar.under-scheduled"],
  "calendar.over-scheduled": HOURS_WARNING_LABELS["calendar.over-scheduled"],
  "calendar.overlap": "Sovrapposta a un'altra lezione",
  "calendar.invalid-text": "Testo non valido (caratteri non ammessi o troppo lungo)",
  "calendar.reference-required": "Manca il riferimento a classe, modulo o docente",
  "calendar.inconsistent-module-subject": "Il modulo non corrisponde alla materia assegnata alla classe",
  "calendar.nonexistent-local-time": "Orario inesistente per il cambio dell'ora legale",
  "calendar.ambiguous-local-time": "Orario ambiguo per il cambio dell'ora legale",
  "calendar.invalid-excel-date": "Data di Excel non valida: scrivila come testo (AAAA-MM-GG) o in formato data",
  "calendar.invalid-excel-time": "Ora di Excel non valida: scrivila come testo (HH:mm) o in formato ora",
  "calendar.excel-minute-precision-required": "Ora di Excel con secondi: usa ore e minuti",
  "calendar.minute-precision-required": "Ora con secondi: usa ore e minuti (HH:mm)",
};

// Avviso del monte ore nell'anteprima (CalendarImportImpact.warning): stessa mappa della sezione "Monte ore"
export const HOUR_WARNING_LABELS = HOURS_WARNING_LABELS;

const GENERIC_ISSUES = ["RequiredColumnMissing", "RequiredFieldMissing", "InvalidValue", "AmbiguousMapping"];
const FIELD_LABELS = { Date: "Data", StartsAt: "Ora inizio", EndsAt: "Ora fine", Class: "Classe", Module: "Modulo", Teacher: "Docente", Room: "Aula", Description: "Descrizione", ExternalReference: "Riferimento esterno" };
// Testi liberi: il backend rifiuta quelli che iniziano con = + - @ (formula injection), con InvalidValue
const TEXT_FIELDS = ["Room", "Description", "ExternalReference"];
export const FORMULA_PREFIX_MESSAGE = "il testo non può iniziare con = + - @ (protezione dalle formule di Excel)";
// Riga senza codice riconosciuto: messaggio generico, mai il codice grezzo da solo
export const UNKNOWN_ISSUE_MESSAGE = "Valore non accettato";

// Avviso di riga → { text, code }: code è valorizzato solo per i codici sconosciuti, da mostrare piccolo
export function issueParts(issue, columns) {
  if (issue.code === "InvalidValue" && TEXT_FIELDS.includes(issue.field)) return { text: `${FIELD_LABELS[issue.field]}: ${FORMULA_PREFIX_MESSAGE}`, code: null };
  const known = ISSUE_MESSAGES[issue.code];
  const text = known || UNKNOWN_ISSUE_MESSAGE;
  const code = known ? null : issue.code || null;
  // Per le colonne ignorate "field" è l'indice della colonna
  if (issue.code === "IgnoredColumn" && columns && /^\d+$/.test(issue.field)) return { text: `${text}: "${columns[Number(issue.field)]}"`, code };
  // Il nome del campo si aggiunge solo ai messaggi generici ("Data: valore non valido"), non a quelli che lo dicono già
  const field = known && !GENERIC_ISSUES.includes(issue.code) ? null : FIELD_LABELS[issue.field];
  return { text: field ? `${field}: ${text.charAt(0).toLowerCase()}${text.slice(1)}` : text, code };
}

export function issueMessage(issue, columns) {
  const { text, code } = issueParts(issue, columns);
  return code ? `${text} (${code})` : text;
}

// ── Controlli del CSV prima dell'invio ────────────────────────────────────────────────────
// Ogni anteprima occupa uno dei 20 posti per utente (30 minuti, non annullabili): i file che il backend
// rifiuterebbe di sicuro si fermano qui. Alias delle intestazioni = CalendarImportColumns del backend.
const HEADER_ALIASES = {
  data: "Date", date: "Date",
  orainizio: "StartsAt", "ora inizio": "StartsAt", startsat: "StartsAt", "start time": "StartsAt",
  orafine: "EndsAt", "ora fine": "EndsAt", endsat: "EndsAt", "end time": "EndsAt",
  classe: "Class", class: "Class", modulo: "Module", module: "Module", docente: "Teacher", teacher: "Teacher",
  aula: "Room", room: "Room", descrizione: "Description", description: "Description", argomento: "Description",
  riferimentoesterno: "ExternalReference", externalreference: "ExternalReference",
};
const REQUIRED_FIELDS = ["Date", "StartsAt", "EndsAt", "Class", "Module", "Teacher"];

// Una riga CSV in celle, con le virgolette doppie ("" dentro un valore tra virgolette)
function splitCsvLine(line, delimiter) {
  const cells = [];
  let cur = "", quoted = false;
  for (let i = 0; i < line.length; i++) {
    const ch = line[i];
    if (quoted) {
      if (ch === '"' && line[i + 1] === '"') { cur += '"'; i++; }
      else if (ch === '"') quoted = false;
      else cur += ch;
    } else if (ch === '"') quoted = true;
    else if (ch === delimiter) { cells.push(cur); cur = ""; }
    else cur += ch;
  }
  cells.push(cur);
  return cells;
}

// text → { ok, message?, tabSeparated, missing, unknown }. Le colonne sconosciute da sole non bloccano
// (il backend le ignora con un avviso); bloccano file vuoti, senza lezioni o senza colonne obbligatorie.
export function precheckCalendarCsv(text) {
  const body = String(text ?? "").replace(/^﻿/, "");
  const lines = body.split(/\r\n|\n|\r/);
  const header = lines.find(l => l.trim() !== "");
  if (header === undefined) return { ok: false, message: "Il file è vuoto.", tabSeparated: false, missing: [], unknown: [] };
  const tabSeparated = header.includes("\t") && !/[;,]/.test(header);
  if (tabSeparated) return { ok: false, message: DELIMITER_MESSAGE, tabSeparated, missing: [], unknown: [] };
  // Come il backend: si prova ";" e ","; vale il separatore che riconosce più colonne obbligatorie
  const attempt = (d) => {
    const names = splitCsvLine(header, d).map(h => h.trim());
    const fields = names.map(h => HEADER_ALIASES[h.toLowerCase()] || null);
    return { delimiter: d, names, missing: REQUIRED_FIELDS.filter(f => !fields.includes(f)), unknown: names.filter((h, i) => h && !fields[i]) };
  };
  const best = [attempt(";"), attempt(",")].sort((a, b) => a.missing.length - b.missing.length)[0];
  const dataLines = lines.slice(lines.indexOf(header) + 1)
    .filter(l => splitCsvLine(l, best.delimiter).some(c => c.trim() !== "")); // ";;;;" di Excel = riga vuota
  if (best.missing.length > 0) {
    const parts = [`Mancano le colonne obbligatorie: ${best.missing.map(f => FIELD_LABELS[f]).join(", ")}.`];
    if (best.unknown.length) parts.push(`Colonne non riconosciute: ${best.unknown.join(", ")}.`);
    parts.push("Usa le intestazioni del modello.");
    return { ok: false, message: parts.join(" "), tabSeparated, missing: best.missing, unknown: best.unknown };
  }
  if (dataLines.length === 0) return { ok: false, message: NO_LESSONS_MESSAGE, tabSeparated, missing: [], unknown: best.unknown };
  return { ok: true, tabSeparated, missing: [], unknown: best.unknown };
}

export const isCsvFile = (file) => /\.csv$/i.test(file?.name || "");

// Testo del file scelto: su web il File del browser, sull'app l'uri locale (copiato in cache dal picker).
// null se non si riesce a leggere: allora il controllo si salta e decide il backend.
export async function readFileText(file) {
  try {
    if (file?.file && typeof file.file.text === "function") return await file.file.text();
    if (file?.uri && typeof fetch === "function") return await (await fetch(file.uri)).text();
  } catch { /* lettura facoltativa */ }
  return null;
}

// Testo del pulsante di conferma: "Crea 1 lezione in bozza" / "Crea N lezioni in bozza"; senza righe pronte
// nessun numero
export function confirmLabel(n) {
  if (!n) return "Nessuna lezione da creare";
  return n === 1 ? "Crea 1 lezione in bozza" : `Crea ${n} lezioni in bozza`;
}

// CalendarConflictKind arriva come numero (enum senza convertitore) o, in futuro, come nome
const CONFLICT_LABELS = { 0: "Lezione già presente", 1: "Sovrapposta a un'altra lezione della classe", 2: "Il docente ha già un'altra lezione", Duplicate: "Lezione già presente", ClassOverlap: "Sovrapposta a un'altra lezione della classe", TeacherOverlap: "Il docente ha già un'altra lezione" };
export const conflictMessage = (c) => CONFLICT_LABELS[c.kind] || "Conflitto di orario";
