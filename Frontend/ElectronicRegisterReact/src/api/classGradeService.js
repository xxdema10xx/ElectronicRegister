// src/api/classGradeService.js
// Voti per gli studenti "di prova" di MyClassesScreen (vedi classesMock.js): il collegamento
// docente-classe non esiste ancora sul backend reale, quindi questi studenti hanno id finti
// ("st-1"...) che il vero endpoint /Grade rifiuta (non sono GUID). Finché quel collegamento non
// esiste, i voti restano coerenti nello stesso mondo finto, salvati nel browser (localStorage).
import { Platform } from "react-native";
import { CLASS_DETAILS } from "../data/classesMock";

const KEY = "classGradesMock1";
const uid = () => (globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 10)}`);

let state = null;
function persist() { try { if (Platform.OS === "web") window.localStorage.setItem(KEY, JSON.stringify(state)); } catch {} }
function seed() {
  const grades = [];
  const add = (studentId, subjectId, value, daysAgo) =>
    grades.push({ id: uid(), studentId, subjectId, value, date: new Date(Date.now() - daysAgo * 86400000).toISOString().slice(0, 10) });
  for (const detail of Object.values(CLASS_DETAILS)) {
    const subs = detail.subjects.slice(0, 2);
    detail.students.slice(0, 4).forEach((st, i) => {
      subs.forEach((sb, j) => add(st.id, sb.subjectId, [6, 7, 8, 7.5, 9][(i + j) % 5], 10 + i * 3 + j * 20));
    });
  }
  return { grades };
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

export function getGradesForStudent(studentId) {
  return load().grades.filter(g => g.studentId === studentId).sort((a, b) => b.date.localeCompare(a.date));
}
export function addClassGrade({ studentId, subjectId, value, date }) {
  const g = { id: uid(), studentId, subjectId, value: Number(value), date };
  load().grades.push(g);
  persist();
  return g;
}
export function updateClassGrade(id, { subjectId, value, date }) {
  const l = load();
  const g = l.grades.find(x => x.id === id);
  if (!g) throw Object.assign(new Error("Voto non trovato."), { status: 404 });
  Object.assign(g, { subjectId, value: Number(value), date });
  persist();
  return g;
}
export function deleteClassGrade(id) {
  const l = load();
  l.grades = l.grades.filter(x => x.id !== id);
  persist();
}
