// src/api/gradeService.js
// Voti (docs/API.md §21). Il server filtra da solo: lo studente vede i propri, il docente quelli delle sue classi-materie.
import axiosClient, { authHeader } from "./axiosClient";
import { cleanDescription } from "../constants/gradeScale";

export async function getGradesPage({ pageNumber, pageSize, subjectId, studentId, date }, token) {
  const params = { pageNumber, pageSize };
  if (subjectId) params.subjectId = subjectId;
  if (studentId) params.studentId = studentId;
  if (date) params.date = date;
  const res = await axiosClient.get("/institution/grades/paged", { headers: authHeader(token), params });
  return res.data; // { items, totalCount }
}

export async function getGradeFilters(token) {
  const res = await axiosClient.get("/institution/grades/filters", { headers: authHeader(token) });
  return res.data;
}

// Voti interi 0–30 (constants/gradeScale.js): le schermate validano prima, il server rifiuta il resto con 400
const toValue = (v) => Number(String(v).trim());

// description: facoltativa (1–200 caratteri); vuota → non inviata
export async function createGrade({ studentId, subjectId, value, date, description }, token) {
  const body = { studentId, subjectId, value: toValue(value), date };
  const desc = cleanDescription(description);
  if (desc) body.description = desc;
  const res = await axiosClient.post("/institution/grades", body, { headers: authHeader(token) });
  return res.data; // id
}

// PUT: description assente o null = invariata; "" = cancellata; testo = sostituito (il server fa il trim).
// Dal form la descrizione si invia sempre (campo vuoto → ""); undefined (chiamante che non la conosce) → non inviata.
export async function updateGrade(id, { subjectId, value, date, description }, token) {
  const body = { subjectId, value: toValue(value), date };
  if (description !== undefined) body.description = cleanDescription(description) ?? "";
  const res = await axiosClient.put(`/institution/grades/${id}`, body, { headers: authHeader(token) });
  return res.data;
}

export async function deleteGrade(id, token) {
  const res = await axiosClient.delete(`/institution/grades/${id}`, { headers: authHeader(token) });
  return res.data;
}
