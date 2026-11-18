// src/api/lessonService.js
// Orario delle lezioni dal calendario del server (docs/api/INSTITUTION_CALENDAR.md), una settimana alla volta.
import axiosClient, { authHeader } from "./axiosClient";
import { getAllClasses } from "./classService";

export const DAYS = [
  { id: 1, label: "Lunedì", short: "Lun" }, { id: 2, label: "Martedì", short: "Mar" }, { id: 3, label: "Mercoledì", short: "Mer" },
  { id: 4, label: "Giovedì", short: "Gio" }, { id: 5, label: "Venerdì", short: "Ven" }, { id: 6, label: "Sabato", short: "Sab" },
  { id: 7, label: "Domenica", short: "Dom" },
];

export const MONTHS = ["gennaio", "febbraio", "marzo", "aprile", "maggio", "giugno", "luglio", "agosto", "settembre", "ottobre", "novembre", "dicembre"];

const PAGE_SIZE = 100; // massimo ammesso da CalendarQuery.size
const BASE = "/institution/calendar";

const pad = (n) => String(n).padStart(2, "0");
export const ymd = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;

// "AAAA-MM-GG" → Date a mezzanotte locale; null se la data non esiste (es. 2026-02-30)
export function parseYmd(value) {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(String(value || ""));
  if (!m) return null;
  const d = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
  return d.getFullYear() === Number(m[1]) && d.getMonth() === Number(m[2]) - 1 && d.getDate() === Number(m[3]) ? d : null;
}

// Da lunedì a domenica della settimana che contiene `date` (Date o "AAAA-MM-GG"), nel fuso locale.
// I giorni si contano con il costruttore di Date (anno, mese, giorno), non sommando millisecondi:
// così il cambio dell'ora legale non sposta le date.
export function weekOf(date = new Date()) {
  const d = date instanceof Date ? date : parseYmd(date) || new Date();
  const monday = new Date(d.getFullYear(), d.getMonth(), d.getDate() - ((d.getDay() + 6) % 7));
  const sunday = new Date(monday.getFullYear(), monday.getMonth(), monday.getDate() + 6);
  return { from: ymd(monday), to: ymd(sunday) };
}

export const currentWeek = (now = new Date()) => weekOf(now);

// Settimana spostata di `weeks` (negativo = indietro)
export function shiftWeek(from, weeks) {
  const d = parseYmd(from) || new Date();
  return weekOf(new Date(d.getFullYear(), d.getMonth(), d.getDate() + 7 * weeks));
}

// "16–22 novembre 2026" · "30 novembre – 6 dicembre 2026" · "28 dicembre 2026 – 3 gennaio 2027"
export function formatWeekRange({ from, to }) {
  const a = parseYmd(from), b = parseYmd(to);
  if (!a || !b) return "";
  const day = (d) => d.getDate();
  const month = (d) => MONTHS[d.getMonth()];
  if (a.getFullYear() !== b.getFullYear()) return `${day(a)} ${month(a)} ${a.getFullYear()} – ${day(b)} ${month(b)} ${b.getFullYear()}`;
  if (a.getMonth() !== b.getMonth()) return `${day(a)} ${month(a)} – ${day(b)} ${month(b)} ${b.getFullYear()}`;
  return `${day(a)}–${day(b)} ${month(b)} ${b.getFullYear()}`;
}

// "AAAA-MM-GG" → 1 (lunedì) … 7 (domenica)
function dayOfWeek(date) {
  const d = parseYmd(date);
  return d ? ((d.getDay() + 6) % 7) + 1 : 1;
}

// teacherName: nome dell'utente collegato, se il server non lo indica nella lezione.
// Date e orari sono già quelli dell'istituto: non vanno convertiti.
export function toLesson(lesson, teacherName) {
  const substitute = !!(lesson.teacherId && lesson.originalTeacherId && lesson.teacherId !== lesson.originalTeacherId);
  return {
    id: lesson.id, date: lesson.date, day: dayOfWeek(lesson.date), start: String(lesson.startsAt).slice(0, 5), end: String(lesson.endsAt).slice(0, 5),
    title: lesson.moduleName || "Lezione", teacherName: lesson.teacherName || teacherName || "", className: lesson.className || "", room: lesson.room || "",
    status: lesson.status || "Scheduled", publication: lesson.publication || "Published",
    substitute, recovery: !!lesson.recoveryOfLessonId,
  };
}

// Etichette di stato di una lezione (sempre testo, mai solo colore). showDraft: solo per admin e staff.
export function lessonLabels(lesson, { showDraft = false } = {}) {
  const labels = [];
  if (lesson.status === "Cancelled") labels.push({ key: "cancelled", label: "Annullata", tone: "red" });
  if (lesson.status === "Completed") labels.push({ key: "completed", label: "Svolta", tone: "green" });
  if (lesson.substitute) labels.push({ key: "substitute", label: "Supplenza", tone: "amber" });
  if (lesson.recovery) labels.push({ key: "recovery", label: "Recupero", tone: "blue" });
  if (showDraft && lesson.publication === "Draft") labels.push({ key: "draft", label: "Bozza", tone: "gray" });
  return labels;
}

// Tutte le pagine di una lista del calendario (size ≤ 100). signal: AbortSignal per annullare la richiesta.
export async function getAllLessons(path, params, token, { signal } = {}) {
  const all = [];
  for (let page = 1; ; page++) {
    const { data } = await axiosClient.get(path, { headers: authHeader(token), params: { ...params, page, size: PAGE_SIZE }, signal });
    const items = Array.isArray(data) ? data : [];
    all.push(...items);
    if (items.length < PAGE_SIZE) return all;
  }
}

// Percorso della lista per ruolo: docente e studente il proprio orario, admin e staff quello della classe
export function schedulePath(role, classId) {
  if (role === "teacher") return `${BASE}/teachers/me`;
  if (role === "student") return `${BASE}/students/me`;
  if ((role === "admin" || role === "staff") && classId) return `${BASE}/classes/${classId}`;
  return null;
}

// Lezioni di un intervallo (default: settimana corrente).
//  - publication: per admin/staff "Published" (Home) oppure null per bozze e pubblicate (gestione)
//  - includeCancelled: anche le lezioni annullate (le viste personali restano solo pubblicate)
export async function getSchedule({ role, classId, teacherName, from, to, includeCancelled = false, publication = "Published", signal }, token) {
  const path = schedulePath(role, classId);
  if (!path) return [];
  const range = from && to ? { from, to } : currentWeek();
  const params = {
    ...range,
    ...((role === "admin" || role === "staff") && publication ? { publication } : null),
    ...(includeCancelled ? { includeCancelled: true } : null),
  };
  const all = await getAllLessons(path, params, token, { signal });
  return all.map(l => toLesson(l, teacherName))
    .sort((a, b) => String(a.date).localeCompare(String(b.date)) || a.day - b.day || a.start.localeCompare(b.start));
}

export async function getClassesForPicker(token) {
  const classes = await getAllClasses(token);
  return classes.map(c => ({ id: c.id, name: c.name })).sort((a, b) => a.name.localeCompare(b.name));
}
