// src/api/lessonService.js
// Orario delle lezioni: classe + materia (che fissa il docente, come class_subject) + giorno + orario libero
// (es. 9:30–12:30, non vincolato a ore tonde). Il backend non ha ancora queste entità: i dati vivono nel
// browser (localStorage), nella stessa forma pensata per un futuro endpoint.
import { Platform } from "react-native";
import { BIENNI, AREAS, PATHS, CLASSES, CLASS_DETAILS } from "../data/classesMock";

const KEY = "lessonsCalendar2";
const uid = () => (globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 10)}`);

export const DAYS = [
  { id: 1, label: "Lunedì", short: "Lun" }, { id: 2, label: "Martedì", short: "Mar" }, { id: 3, label: "Mercoledì", short: "Mer" },
  { id: 4, label: "Giovedì", short: "Gio" }, { id: 5, label: "Venerdì", short: "Ven" },
];
export const DAY_START = "08:00";
export const DAY_END = "18:00";
// Solo ore spaccate o mezz'ore, dalle 8:00 alle 18:00.
export const TIME_OPTIONS = Array.from({ length: 21 }, (_, i) => {
  const totalMin = 8 * 60 + i * 30;
  const h = String(Math.floor(totalMin / 60)).padStart(2, "0");
  const m = String(totalMin % 60).padStart(2, "0");
  return `${h}:${m}`;
});

const toMinutes = (hhmm) => { const [h, m] = String(hhmm).split(":").map(Number); return h * 60 + (m || 0); };
const overlaps = (aStart, aEnd, bStart, bEnd) => toMinutes(aStart) < toMinutes(bEnd) && toMinutes(bStart) < toMinutes(aEnd);

// Tutte le classi con nome e materie (stessa forma usata da classService per "Classi").
function allClassesFlat() {
  const out = [];
  for (const b of BIENNI) for (const area of (AREAS[b.id] || []))
    for (const path of (PATHS[area.id] || [])) for (const k of (CLASSES[path.id] || [])) {
      const detail = CLASS_DETAILS[k.id] || { subjects: [] };
      out.push({ id: k.id, name: k.name, subjects: detail.subjects });
    }
  return out;
}
export const getClassesForPicker = () => allClassesFlat().map(c => ({ id: c.id, name: c.name }));
export const getSubjectsOfClass = (classId) => allClassesFlat().find(c => c.id === classId)?.subjects || [];

// Stessa derivazione stabile usata da classService per collegare un docente reale a uno di prova
// (finché il backend non espone davvero quali classi/materie insegna un docente).
const MOCK_TEACHER_KEYS = ["t1", "t2", "t3", "t4", "t5", "t6"];
function mockTeacherKey(teacherId) {
  if (!teacherId) return MOCK_TEACHER_KEYS[0];
  let hash = 0;
  for (let i = 0; i < teacherId.length; i++) hash = (hash * 31 + teacherId.charCodeAt(i)) >>> 0;
  return MOCK_TEACHER_KEYS[hash % MOCK_TEACHER_KEYS.length];
}
// Stessa idea per collegare uno studente reale a una classe di prova (il backend non espone ancora
// a quale classe appartiene lo studente che ha fatto login).
function mockClassKey(studentId, classes) {
  if (!classes.length) return null;
  if (!studentId) return classes[0].id;
  let hash = 0;
  for (let i = 0; i < studentId.length; i++) hash = (hash * 31 + studentId.charCodeAt(i)) >>> 0;
  return classes[hash % classes.length].id;
}

let state = null;
function persist() { try { if (Platform.OS === "web") window.localStorage.setItem(KEY, JSON.stringify(state)); } catch {} }
function seed() {
  const lessons = [];
  const add = (classId, subjectId, day, start, end) => lessons.push({ id: uid(), classId, subjectId, day, start, end });
  for (const c of allClassesFlat()) {
    let day = 1, hour = 8;
    for (const sb of c.subjects.slice(0, 5)) {
      const pad = (n) => String(n).padStart(2, "0");
      add(c.id, sb.subjectId, day, `${pad(hour)}:00`, `${pad(hour + 1)}:00`);
      hour++;
      if (hour > 11) { hour = 8; day = day < 5 ? day + 1 : 1; }
    }
  }
  return { lessons };
}
function load() {
  if (state) return state;
  try {
    const raw = Platform.OS === "web" ? window.localStorage.getItem(KEY) : null;
    if (raw) { state = JSON.parse(raw); return state; }
  } catch {}
  state = seed();
  persist();
  return state;
}
export function resetLessons() { state = seed(); persist(); }

function withDetails(lesson) {
  const cls = allClassesFlat().find(c => c.id === lesson.classId);
  const sb = cls?.subjects.find(s => s.subjectId === lesson.subjectId);
  return {
    ...lesson, className: cls?.name || "—", subjectName: sb?.subjectName || "—",
    teacherId: sb?.teacherId, teacherFirstName: sb?.teacherFirstName, teacherLastName: sb?.teacherLastName,
  };
}

export const getClassSchedule = (classId) => load().lessons.filter(l => l.classId === classId).map(withDetails)
  .sort((a, b) => a.day - b.day || toMinutes(a.start) - toMinutes(b.start));
export const getTeacherSchedule = (teacherId) => {
  const key = mockTeacherKey(teacherId);
  return load().lessons.map(withDetails).filter(l => l.teacherId === key)
    .sort((a, b) => a.day - b.day || toMinutes(a.start) - toMinutes(b.start));
};
export const getStudentSchedule = (studentId) => {
  const classId = mockClassKey(studentId, allClassesFlat());
  return classId ? getClassSchedule(classId) : [];
};
export const getStudentClassId = (studentId) => mockClassKey(studentId, allClassesFlat());

function validateSlot(start, end) {
  if (!start || !end) throw Object.assign(new Error("Imposta l'orario di inizio e fine."), { status: 400 });
  if (toMinutes(end) <= toMinutes(start)) throw Object.assign(new Error("L'orario di fine deve essere dopo l'inizio."), { status: 400 });
  if (toMinutes(start) < toMinutes(DAY_START) || toMinutes(end) > toMinutes(DAY_END))
    throw Object.assign(new Error(`L'orario deve essere compreso tra le ${DAY_START} e le ${DAY_END}.`), { status: 400 });
}

export function addLesson({ classId, subjectId, day, start, end }) {
  validateSlot(start, end);
  const l = load();
  if (l.lessons.some(x => x.classId === classId && x.day === day && overlaps(start, end, x.start, x.end)))
    throw Object.assign(new Error("In quell'orario la classe ha già una lezione."), { status: 409 });
  const lesson = { id: uid(), classId, subjectId, day, start, end };
  l.lessons.push(lesson);
  persist();
  return withDetails(lesson);
}
export function updateLesson(id, { subjectId, day, start, end }) {
  validateSlot(start, end);
  const l = load();
  const lesson = l.lessons.find(x => x.id === id);
  if (!lesson) throw Object.assign(new Error("Lezione non trovata."), { status: 404 });
  if (l.lessons.some(x => x.id !== id && x.classId === lesson.classId && x.day === day && overlaps(start, end, x.start, x.end)))
    throw Object.assign(new Error("In quell'orario la classe ha già una lezione."), { status: 409 });
  Object.assign(lesson, { subjectId, day, start, end });
  persist();
  return withDetails(lesson);
}
export function deleteLesson(id) {
  const l = load();
  l.lessons = l.lessons.filter(x => x.id !== id);
  persist();
}
