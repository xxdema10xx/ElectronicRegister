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

// ── Monte ore ───────────────────────────────────────────────────────────────────────────────
// Unico formatter delle durate del calendario (monte ore, "Le mie ore", anteprima dell'import).
// Minuti → "12 h 30 min" · "45 min" · "12 h" · "0 h" (lo zero in ore come gli altri valori).
// Nessun arrotondamento: 599 minuti restano "9 h 59 min".
export function formatMinutes(minutes) {
  const n = Number(minutes) || 0;
  const abs = Math.abs(Math.trunc(n));
  const sign = n < 0 && abs ? "-" : "";
  const h = Math.floor(abs / 60), m = abs % 60;
  if (!abs) return "0 h";
  if (!h) return `${sign}${m} min`;
  return m ? `${sign}${h} h ${m} min` : `${sign}${h} h`;
}

// Unica mappa degli avvisi del monte ore: la usano l'anteprima dell'import e la sezione "Monte ore",
// così lo stesso codice ha sempre lo stesso testo
export const HOURS_WARNING_LABELS = {
  "calendar.under-scheduled": "Ore sotto il previsto",
  "calendar.over-scheduled": "Ore oltre il previsto",
  "calendar.delivery-incomplete": "Ore ancora da svolgere",
  "calendar.over-delivered": "Svolte oltre il previsto",
};

// Admin e staff: monte ore della classe per modulo, unità formativa e mese
export async function getClassHours(classId, token) {
  return (await axiosClient.get(`${BASE}/hours`, { headers: authHeader(token), params: { classId } })).data;
}

// Nomi di moduli e unità formative dalla versione del piano usata dal report (admin e staff).
// Se il piano non si legge (rollout spento, permessi) si restituiscono mappe vuote: i nomi mancanti
// diventano "Modulo" / "Unità formativa".
// order: posizione di unità e moduli nel piano (displayOrder, poi codice), per mostrarli nello stesso ordine
// del piano invece che in quello della risposta del monte ore.
const byPlanOrder = (a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0) || String(a.code || "").localeCompare(String(b.code || ""), "it", { numeric: true });

export async function getPlanNames(versionId, token) {
  const names = { modules: {}, units: {}, order: { modules: {}, units: {} } };
  if (!versionId) return names;
  try {
    const { data } = await axiosClient.get(`/institution/formative-plan-versions/${versionId}`, { headers: authHeader(token) });
    let unitRank = 0, moduleRank = 0;
    for (const u of (data?.units || []).slice().sort(byPlanOrder)) {
      names.units[u.id] = [u.code, u.name].filter(Boolean).join(" · ") || null;
      names.order.units[u.id] = unitRank++;
      for (const m of (u.modules || []).slice().sort(byPlanOrder)) {
        names.modules[m.id] = [m.code, m.name].filter(Boolean).join(" · ") || null;
        names.order.modules[m.id] = moduleRank++;
      }
    }
  } catch { /* nomi facoltativi */ }
  return names;
}

// Elementi del monte ore nell'ordine del piano; quelli che il piano non conosce restano in fondo,
// nell'ordine ricevuto
export function sortByPlan(items, idKey, rank = {}) {
  const pos = (x) => (Number.isFinite(rank[x[idKey]]) ? rank[x[idKey]] : Infinity);
  return items.map((x, i) => ({ x, i })).sort((a, b) => (pos(a.x) - pos(b.x)) || a.i - b.i).map(e => e.x);
}

// Anno scolastico corrente: 1 settembre – 31 agosto (al massimo 366 giorni, limite del server)
export function schoolYear(now = new Date()) {
  const start = now.getMonth() >= 8 ? now.getFullYear() : now.getFullYear() - 1;
  return { from: `${start}-09-01`, to: `${start + 1}-08-31` };
}

export function rangeError({ from, to }) {
  const a = parseYmd(from), b = parseYmd(to);
  if (!a || !b) return "Date non valide: usa AAAA-MM-GG.";
  if (b < a) return "La data finale deve essere uguale o successiva a quella iniziale.";
  const days = Math.round((Date.UTC(b.getFullYear(), b.getMonth(), b.getDate()) - Date.UTC(a.getFullYear(), a.getMonth(), a.getDate())) / 86400000);
  if (days > 366) return "Intervallo troppo ampio: al massimo 366 giorni.";
  return null;
}

const lessonMinutes = (l) => {
  if (Number.isFinite(l.durationMinutes)) return l.durationMinutes;
  const a = parseUtc(l.startUtc), b = parseUtc(l.endUtc);
  return a && b && !isNaN(a) && !isNaN(b) ? Math.round((b - a) / 60000) : 0;
};

// Docente: "Le mie ore" dalle sole lezioni che può leggere (GET teachers/me, paginato) raggruppate per
// classe + modulo, con le ore previste del modulo (GET lessons/{id}/module, una chiamata per modulo).
// Non è il totale del modulo: le lezioni dei supplenti non sono visibili al docente.
// loadLessons(range) e loadModule(lessonId) sono iniettabili per i test; moduleCache: Map moduleId → Promise.
export async function getTeacherHours(range, token, { loadLessons, loadModule, moduleCache = new Map() } = {}) {
  const lessons = await loadLessons(range);
  const groups = new Map();
  for (const l of lessons) {
    if (l.status === "Cancelled") continue;
    const key = `${l.classId}|${l.moduleId}`;
    if (!groups.has(key)) groups.set(key, { classId: l.classId, moduleId: l.moduleId, className: l.className || "Classe", moduleName: l.moduleName || "Modulo", sampleLessonId: l.id, scheduledMinutes: 0, deliveredMinutes: 0, lessons: 0 });
    const g = groups.get(key);
    g.lessons += 1;
    g.scheduledMinutes += lessonMinutes(l);
    if (l.status === "Completed") g.deliveredMinutes += Number(l.deliveredMinutes) || 0;
  }
  const rows = [...groups.values()];
  await Promise.all(rows.map(async (g) => {
    if (!moduleCache.has(g.moduleId)) moduleCache.set(g.moduleId, loadModule(g.sampleLessonId).catch(() => null));
    const mod = await moduleCache.get(g.moduleId);
    g.plannedMinutes = mod && Number.isFinite(mod.plannedMinutes) ? mod.plannedMinutes : null;
    if (mod?.name && g.moduleName === "Modulo") g.moduleName = mod.name;
  }));
  return rows.sort((a, b) => a.className.localeCompare(b.className) || a.moduleName.localeCompare(b.moduleName));
}
