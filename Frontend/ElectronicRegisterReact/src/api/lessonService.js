// src/api/lessonService.js
// Orario delle lezioni della settimana corrente, in sola lettura, dal calendario del server (docs/API.md).
import axiosClient, { authHeader } from "./axiosClient";
import { getAllClasses } from "./classService";

export const DAYS = [
  { id: 1, label: "Lunedì", short: "Lun" }, { id: 2, label: "Martedì", short: "Mar" }, { id: 3, label: "Mercoledì", short: "Mer" },
  { id: 4, label: "Giovedì", short: "Gio" }, { id: 5, label: "Venerdì", short: "Ven" }, { id: 6, label: "Sabato", short: "Sab" },
  { id: 7, label: "Domenica", short: "Dom" },
];

const pad = (n) => String(n).padStart(2, "0");
const ymd = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;

// Da lunedì a domenica della settimana di "now", nel fuso locale
export function currentWeek(now = new Date()) {
  const monday = new Date(now.getFullYear(), now.getMonth(), now.getDate() - ((now.getDay() + 6) % 7));
  const sunday = new Date(monday.getFullYear(), monday.getMonth(), monday.getDate() + 6);
  return { from: ymd(monday), to: ymd(sunday) };
}

// "AAAA-MM-GG" → 1 (lunedì) … 7 (domenica)
function dayOfWeek(date) {
  const [y, m, d] = String(date).split("-").map(Number);
  return ((new Date(y, m - 1, d).getDay() + 6) % 7) + 1;
}

// teacherName: nome dell'utente collegato, se il server non lo indica nella lezione.
// Date e orari sono già quelli dell'istituto: non vanno convertiti.
export function toLesson(lesson, teacherName) {
  return {
    id: lesson.id, day: dayOfWeek(lesson.date), start: String(lesson.startsAt).slice(0, 5), end: String(lesson.endsAt).slice(0, 5),
    title: lesson.moduleName || "Lezione", teacherName: lesson.teacherName || teacherName || "", className: lesson.className || "", room: lesson.room || "",
  };
}

// role: "teacher" e "student" vedono il proprio orario, "admin" quello della classe scelta (solo lezioni pubblicate)
export async function getSchedule({ role, classId, teacherName }, token) {
  const path = role === "teacher" ? "/institution/calendar/teachers/me"
    : role === "student" ? "/institution/calendar/students/me"
    : role === "admin" && classId ? `/institution/calendar/classes/${classId}` : null;
  if (!path) return [];
  const params = { ...currentWeek(), ...(role === "admin" ? { publication: "Published" } : null) };
  const { data } = await axiosClient.get(path, { headers: authHeader(token), params });
  return (Array.isArray(data) ? data : []).map(l => toLesson(l, teacherName))
    .sort((a, b) => a.day - b.day || a.start.localeCompare(b.start));
}

export async function getClassesForPicker(token) {
  const classes = await getAllClasses(token);
  return classes.map(c => ({ id: c.id, name: c.name })).sort((a, b) => a.name.localeCompare(b.name));
}
