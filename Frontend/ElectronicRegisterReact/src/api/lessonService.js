// src/api/lessonService.js
// Orario delle lezioni: classe + materia (che fissa il docente, come class_subject) + giorno + ora.
// Il backend non ha ancora queste entità: i dati vivono nel browser (localStorage), nella stessa forma
// pensata per un futuro endpoint, così le schermate non dovranno cambiare quando arriverà.
import { Platform } from "react-native";
import { BIENNI, AREAS, PATHS, CLASSES, CLASS_DETAILS } from "../data/classesMock";

const KEY = "lessonsCalendar1";
const uid = () => (globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 10)}`);

export const DAYS = [
  { id: 1, label: "Lunedì", short: "Lun" }, { id: 2, label: "Martedì", short: "Mar" }, { id: 3, label: "Mercoledì", short: "Mer" },
  { id: 4, label: "Giovedì", short: "Gio" }, { id: 5, label: "Venerdì", short: "Ven" },
];
export const PERIODS = [
  { id: 1, label: "1ª ora", start: "08:00", end: "09:00" },
  { id: 2, label: "2ª ora", start: "09:00", end: "10:00" },
  { id: 3, label: "3ª ora", start: "10:10", end: "11:10" },
  { id: 4, label: "4ª ora", start: "11:10", end: "12:10" },
  { id: 5, label: "5ª ora", start: "12:20", end: "13:20" },
  { id: 6, label: "6ª ora", start: "13:20", end: "14:20" },
];

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
  const add = (classId, subjectId, day, period) => lessons.push({ id: uid(), classId, subjectId, day, period });
  for (const c of allClassesFlat()) {
    let day = 1, period = 1;
    for (const sb of c.subjects.slice(0, 5)) {
      add(c.id, sb.subjectId, day, period);
      period++;
      if (period > 4) { period = 1; day = day < 5 ? day + 1 : 1; }
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
  const period = PERIODS.find(p => p.id === lesson.period);
  return {
    ...lesson, className: cls?.name || "—", subjectName: sb?.subjectName || "—",
    teacherId: sb?.teacherId, teacherFirstName: sb?.teacherFirstName, teacherLastName: sb?.teacherLastName,
    start: period?.start, end: period?.end,
  };
}

export const getClassSchedule = (classId) => load().lessons.filter(l => l.classId === classId).map(withDetails);
export const getTeacherSchedule = (teacherId) => {
  const key = mockTeacherKey(teacherId);
  return load().lessons.map(withDetails).filter(l => l.teacherId === key);
};
export const getStudentSchedule = (studentId) => {
  const classId = mockClassKey(studentId, allClassesFlat());
  return classId ? getClassSchedule(classId) : [];
};
export const getStudentClassId = (studentId) => mockClassKey(studentId, allClassesFlat());

export function addLesson({ classId, subjectId, day, period }) {
  const l = load();
  if (l.lessons.some(x => x.classId === classId && x.day === day && x.period === period))
    throw Object.assign(new Error("In quell'ora la classe ha già una lezione."), { status: 409 });
  const lesson = { id: uid(), classId, subjectId, day, period };
  l.lessons.push(lesson);
  persist();
  return withDetails(lesson);
}
export function updateLesson(id, { subjectId, day, period }) {
  const l = load();
  const lesson = l.lessons.find(x => x.id === id);
  if (!lesson) throw Object.assign(new Error("Lezione non trovata."), { status: 404 });
  if (l.lessons.some(x => x.id !== id && x.classId === lesson.classId && x.day === day && x.period === period))
    throw Object.assign(new Error("In quell'ora la classe ha già una lezione."), { status: 409 });
  Object.assign(lesson, { subjectId, day, period });
  persist();
  return withDetails(lesson);
}
export function deleteLesson(id) {
  const l = load();
  l.lessons = l.lessons.filter(x => x.id !== id);
  persist();
}
