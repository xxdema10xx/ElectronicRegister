// src/api/gradeBatchService.js
// Voti alla classe in blocco: POST /api/institution/grades/batch (A, Staff, Teacher con GradeCreate).
// Body: { eventId, classSubjectId, date, description?, grades: [{ studentId, value }] } (1–100 righe).
// 201 { eventId, created: [{ studentId, gradeId }] }. 400 GradeBatchInvalid con errori per riga (niente salvato).
// Lo stesso eventId con lo stesso payload restituisce la stessa 201 (nessun duplicato); con payload diverso 409.
import * as Crypto from "expo-crypto";
import axiosClient, { authHeader } from "./axiosClient";
import { getStudents } from "./studentService";
import { GRADE_ERROR, cleanDescription } from "../constants/gradeScale";

export const BATCH_MAX_ROWS = 100;
export const newBatchEventId = () => globalThis.crypto?.randomUUID?.() ?? Crypto.randomUUID();

const PAGE = 100;

// Classi-materia tra cui scegliere: [{ classSubjectId, classId, label }], ordinate per classe e materia.
// Admin e segreteria ricevono tutte quelle dell'istituto; il docente solo le proprie (teacherId filtra comunque).
// null = il ruolo non può leggerle (403/404): il modulo mostra un avviso invece della tabella.
export async function getGradeClassSubjects(token, { teacherId } = {}) {
  const all = [];
  try {
    for (let pageNumber = 1; ; pageNumber++) {
      const res = await axiosClient.get("/institution/class-subjects", { headers: authHeader(token), params: { pageNumber, pageSize: PAGE } });
      const items = Array.isArray(res.data) ? res.data : (res.data?.items || []);
      all.push(...items);
      if (items.length < PAGE) break;
    }
  } catch (e) {
    if (e.status === 403 || e.status === 404) return null;
    throw e;
  }
  return all
    .filter(cs => !teacherId || cs.teacherId === teacherId)
    .map(cs => ({ classSubjectId: cs.classSubjectId || cs.id, classId: cs.classId, label: [cs.className, cs.subjectName].filter(Boolean).join(" · ") }))
    .sort((a, b) => a.label.localeCompare(b.label));
}

// Studenti della classe, per cognome e nome
export async function getClassStudents(classId, token) {
  const list = await getStudents(undefined, token, classId);
  return (Array.isArray(list) ? list : [])
    .slice().sort((a, b) => `${a.lastName} ${a.firstName}`.localeCompare(`${b.lastName} ${b.firstName}`));
}

export async function createGradesBatch({ eventId, classSubjectId, date, description, grades }, token) {
  const body = { eventId, classSubjectId, date, grades };
  const desc = cleanDescription(description);
  if (desc) body.description = desc;
  const res = await axiosClient.post("/institution/grades/batch", body, { headers: authHeader(token) });
  return res.data; // { eventId, created: [{ studentId, gradeId }] }
}

// Messaggi italiani per i codici degli errori per riga
export const BATCH_ROW_MESSAGES = {
  duplicate_student: "Studente presente più volte nel blocco.",
  student_not_found: "Studente non trovato in questo istituto.",
  student_not_in_class: "Lo studente non fa parte di questa classe.",
  invalid_value: GRADE_ERROR,
};

// Errori per riga di un 400 GradeBatchInvalid → { [studentId]: messaggio }; null se non è quel tipo di errore.
// sent: le righe inviate, per risalire allo studente dall'index quando studentId manca.
export function batchRowErrors(error, sent) {
  if (error?.status !== 400 || error.code !== "GradeBatchInvalid" || !Array.isArray(error.errors)) return null;
  const out = {};
  for (const row of error.errors) {
    const studentId = row?.studentId || sent?.[row?.index]?.studentId;
    if (!studentId) continue;
    out[studentId] = BATCH_ROW_MESSAGES[row.code] || row.message || "Riga non valida.";
  }
  return out;
}
