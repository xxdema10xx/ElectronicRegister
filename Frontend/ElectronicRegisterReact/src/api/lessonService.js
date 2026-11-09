// src/api/lessonService.js
// Orario delle lezioni. Senza dati finti: finché non è collegato al calendario del server è vuoto.
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

export const getClassesForPicker = () => [];
export const getSubjectsOfClass = () => [];
export const getClassSchedule = () => [];
export const getTeacherSchedule = () => [];
export const getStudentSchedule = () => [];
