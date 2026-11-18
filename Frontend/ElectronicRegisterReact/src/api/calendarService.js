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
