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
