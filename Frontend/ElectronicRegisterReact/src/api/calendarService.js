// src/api/calendarService.js
// Lezioni del calendario: dettaglio, modulo e storico (docs/api/INSTITUTION_CALENDAR.md).
// Il dettaglio è leggibile da chi è autorizzato alla lezione; lo storico (con i motivi delle modifiche)
// solo da admin e staff: la proiezione pubblica della lezione non contiene motivi di proposito.
import axiosClient, { authHeader } from "./axiosClient";
import { parseUtc } from "./examContract";
import { MONTHS, parseYmd } from "./lessonService";

const BASE = "/institution/calendar";
export const HISTORY_PAGE_SIZE = 20;

export async function getLesson(id, token) {
  return (await axiosClient.get(`${BASE}/lessons/${id}`, { headers: authHeader(token) })).data;
}

// Modulo del piano formativo della lezione (plannedMinutes = ore previste del modulo)
export async function getLessonModule(id, token) {
  return (await axiosClient.get(`${BASE}/lessons/${id}/module`, { headers: authHeader(token) })).data;
}

// Una pagina dello storico (20 voci), in ordine cronologico
export async function getLessonHistory(id, page, token) {
  const { data } = await axiosClient.get(`${BASE}/lessons/${id}/history`, { headers: authHeader(token), params: { page, size: HISTORY_PAGE_SIZE } });
  return Array.isArray(data) ? data : [];
}

// ── Testi ───────────────────────────────────────────────────────────────────────────────────
const WEEKDAYS = ["domenica", "lunedì", "martedì", "mercoledì", "giovedì", "venerdì", "sabato"];
const pad = (n) => String(n).padStart(2, "0");
export const hhmm = (t) => String(t || "").slice(0, 5);

// "2026-11-19" → "giovedì 19 novembre 2026"
export function formatLongDate(date) {
  const d = parseYmd(String(date || "").slice(0, 10));
  return d ? `${WEEKDAYS[d.getDay()]} ${d.getDate()} ${MONTHS[d.getMonth()]} ${d.getFullYear()}` : "";
}

// "2026-11-19" → "19/11/2026"
export function formatShortDate(date) {
  const d = parseYmd(String(date || "").slice(0, 10));
  return d ? `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()}` : "";
}

export const STATUS_LABELS = { Scheduled: "Programmata", Cancelled: "Annullata", Completed: "Svolta" };
export const PUBLICATION_LABELS = { Draft: "Bozza", Published: "Pubblicata" };

export const OPERATION_LABELS = {
  LessonCreated: "Creazione", LessonUpdated: "Modifica", LessonPublished: "Pubblicazione",
  LessonCancelled: "Annullamento", LessonRescheduled: "Spostamento", TeacherSubstituted: "Sostituzione del docente",
  LessonCompleted: "Lezione svolta", DeliveredMinutesCorrected: "Correzione dei minuti svolti",
};

// Il JSON dello storico è amministrativo e cambia forma: il record SQL precedente (local_date, starts_at…)
// e la nuova proiezione (Date, StartsAt…). Si legge in modo difensivo: un campo assente vale null.
function parseJson(text) {
  if (!text || typeof text !== "string") return null;
  try { const v = JSON.parse(text); return v && typeof v === "object" ? v : null; } catch { return null; }
}
function pick(obj, keys) {
  for (const k of keys) if (obj && obj[k] !== undefined && obj[k] !== null && obj[k] !== "") return obj[k];
  return null;
}
function slot(obj) {
  if (!obj) return null;
  const date = pick(obj, ["Date", "date", "local_date", "LocalDate"]);
  const start = pick(obj, ["StartsAt", "startsAt", "starts_at"]);
  const end = pick(obj, ["EndsAt", "endsAt", "ends_at"]);
  const day = formatShortDate(typeof date === "string" ? date : "");
  if (!day || typeof start !== "string" || typeof end !== "string") return null;
  return `${day} ${hhmm(start)}–${hhmm(end)}`;
}
const teacherOf = (obj) => (obj ? pick(obj, ["TeacherId", "teacherId", "teacher_id"]) : null);

// Voce dello storico → { when, operation, reason, change } senza ID tecnici.
// teacherName(id): nome di un docente (per la sostituzione); se non si trova la riga si omette.
export function describeHistory(entry, teacherName = () => null) {
  const when = parseUtc(entry.occurredAtUtc);
  const operation = OPERATION_LABELS[entry.operation] || "Modifica";
  const before = parseJson(entry.beforeJson), after = parseJson(entry.stateJson);
  let change = null;
  if (entry.operation === "LessonRescheduled") {
    const a = slot(before), b = slot(after);
    if (a && b) change = `da ${a} a ${b}`;
  } else if (entry.operation === "TeacherSubstituted") {
    const a = teacherName(teacherOf(before)), b = teacherName(teacherOf(after));
    if (a && b) change = `da ${a} a ${b}`;
    else if (b) change = `nuovo docente: ${b}`;
  }
  return {
    key: entry.eventId,
    when: when && !isNaN(when) ? when.toLocaleString("it-IT", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" }) : "",
    operation, reason: entry.reason || "", change,
  };
}

// ── Comandi (admin e staff) ─────────────────────────────────────────────────────────────────
// JSON rigoroso: solo i campi del contratto. expectedRevision = revision letta; eventId riusato nei
// retry dello stesso payload (vedi useIntentId). Dopo la receipt la lezione va riletta.
const toTime = (t) => (/^\d{2}:\d{2}$/.test(t) ? `${t}:00` : t);

export async function cancelLesson(id, { eventId, expectedRevision, reason }, token) {
  const body = { eventId, expectedRevision, reason };
  return (await axiosClient.post(`${BASE}/lessons/${id}/cancel`, body, { headers: authHeader(token) })).data;
}

export async function rescheduleLesson(id, { eventId, expectedRevision, date, startsAt, endsAt, reason }, token) {
  const body = { eventId, expectedRevision, date, startsAt: toTime(startsAt), endsAt: toTime(endsAt), reason };
  return (await axiosClient.post(`${BASE}/lessons/${id}/reschedule`, body, { headers: authHeader(token) })).data;
}

export async function substituteLesson(id, { eventId, expectedRevision, teacherId, reason }, token) {
  const body = { eventId, expectedRevision, teacherId, reason };
  return (await axiosClient.post(`${BASE}/lessons/${id}/substitute`, body, { headers: authHeader(token) })).data;
}

// Il backend accetta la lezione solo se modificabile: le Completed (e le annullate) non hanno comandi
export const canManageLesson = (lesson) => !!lesson && lesson.status === "Scheduled";

export const REASON_MAX = 500;
export const validReason = (reason) => {
  const t = String(reason || "").trim();
  return t.length >= 1 && t.length <= REASON_MAX;
};

// "AAAA-MM-GG" e "HH:mm" (00–23, 00–59); fine dopo l'inizio, nello stesso giorno
export function rescheduleError({ date, startsAt, endsAt }) {
  if (!parseYmd(date)) return "Data non valida: usa AAAA-MM-GG.";
  const time = /^([01]\d|2[0-3]):[0-5]\d$/;
  if (!time.test(startsAt || "")) return "Ora di inizio non valida: usa HH:mm.";
  if (!time.test(endsAt || "")) return "Ora di fine non valida: usa HH:mm.";
  if (endsAt <= startsAt) return "L'ora di fine deve essere dopo l'ora di inizio.";
  return null;
}

// ── Impostazioni del calendario (admin e staff) ─────────────────────────────────────────────
// Prima configurazione con expectedRevision -1, poi con la revisione letta. Il fuso non si cambia più
// dopo che esistono lezioni (lo decide il server).
export async function saveCalendarSettings({ eventId, expectedRevision, timeZoneId, approvalLeadDays, reason }, token) {
  const body = { eventId, expectedRevision, timeZoneId, approvalLeadDays, reason };
  return (await axiosClient.put(`${BASE}/settings`, body, { headers: authHeader(token) })).data;
}
