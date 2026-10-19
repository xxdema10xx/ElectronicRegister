// src/api/classService.js
// Flusso Classi: biennio → area di studio → corso di studio → classe → dettaglio.
// Finché il backend non ha gli endpoint (USE_MOCK = true) si usano i dati fissi di src/data/classesMock.js.
// Gli endpoint reali seguono il contratto proposto per il backend (tutti riservati all'admin).
import axiosClient, { authHeader } from "./axiosClient";
import { BIENNI, AREAS, PATHS, CLASSES, CLASS_DETAILS } from "../data/classesMock";

const USE_MOCK = true;

const delay = (value) => new Promise(resolve => setTimeout(() => resolve(value), 250));
const get = async (path, token) => (await axiosClient.get(path, { headers: authHeader(token) })).data;

// Bienni attivi: id, startYear, endYear
export const getActiveBienni = (token) =>
  USE_MOCK ? delay(BIENNI) : get("/Biennium?active=true", token);

// Aree di studio del biennio: id (bienniumStudyAreaId), name, description
export const getStudyAreas = (bienniumId, token) =>
  USE_MOCK ? delay(AREAS[bienniumId] || []) : get(`/Biennium/${bienniumId}/StudyArea`, token);

// Corsi di studio dell'area: id (bienniumStudyPathId), name, description
export const getStudyPaths = (bienniumStudyAreaId, token) =>
  USE_MOCK ? delay(PATHS[bienniumStudyAreaId] || []) : get(`/BienniumStudyArea/${bienniumStudyAreaId}/StudyPath`, token);

// Classi del corso: id, name
export const getClasses = (bienniumStudyPathId, token) =>
  USE_MOCK ? delay(CLASSES[bienniumStudyPathId] || []) : get(`/BienniumStudyPath/${bienniumStudyPathId}/Class`, token);

// Dettaglio classe: { id, name, students: [{id, firstName, lastName}],
//   subjects: [{subjectId, subjectName, teacherId, teacherFirstName, teacherLastName}] }
export function getClassDetail(classId, className, token) {
  if (!USE_MOCK) return get(`/Class/${classId}`, token);
  const d = CLASS_DETAILS[classId] || { students: [], subjects: [] };
  return delay({ id: classId, name: className, ...d });
}

// Tutte le classi, appiattendo biennio → area → corso.
function allClassesFlat() {
  const out = [];
  for (const b of BIENNI) for (const area of (AREAS[b.id] || []))
    for (const path of (PATHS[area.id] || [])) for (const klass of (CLASSES[path.id] || [])) out.push(klass);
  return out;
}

// In attesa che il backend collegasse le classi al docente reale, si deriva in modo stabile
// (sempre lo stesso risultato per lo stesso docente) un insegnante di prova dal suo id reale.
const MOCK_TEACHER_KEYS = ["t1", "t2", "t3", "t4", "t5", "t6"];
function mockTeacherKey(teacherId) {
  if (!teacherId) return MOCK_TEACHER_KEYS[0];
  let hash = 0;
  for (let i = 0; i < teacherId.length; i++) hash = (hash * 31 + teacherId.charCodeAt(i)) >>> 0;
  return MOCK_TEACHER_KEYS[hash % MOCK_TEACHER_KEYS.length];
}

// Classi in cui insegna il docente: { id, name, students, subjects } per ciascuna.
// "subjects" contiene solo le materie insegnate DA LUI in quella classe, non quelle dei colleghi
// (un docente non è un admin: non deve vedere chi insegna le altre materie della classe).
export function getMyClasses(teacherId, token) {
  if (!USE_MOCK) return get(`/Teacher/${teacherId}/Class`, token);
  const key = mockTeacherKey(teacherId);
  const classes = allClassesFlat()
    .map(k => {
      const detail = CLASS_DETAILS[k.id] || { students: [], subjects: [] };
      return { ...k, students: detail.students, subjects: detail.subjects.filter(sb => sb.teacherId === key) };
    })
    .filter(k => k.subjects.length > 0);
  return delay(classes);
}
