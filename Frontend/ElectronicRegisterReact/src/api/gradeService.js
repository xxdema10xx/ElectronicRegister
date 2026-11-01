// src/api/gradeService.js
// Voti (docs/API.md §21). Il server filtra da solo: lo studente vede i propri, il docente quelli delle sue classi-materie.
import axiosClient, { authHeader } from "./axiosClient";

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

const toValue = (v) => parseFloat(String(v).replace(",", "."));

export async function createGrade({ studentId, subjectId, value, date }, token) {
  const res = await axiosClient.post("/institution/grades",
    { studentId, subjectId, value: toValue(value), date }, { headers: authHeader(token) });
  return res.data; // id
}

export async function updateGrade(id, { subjectId, value, date }, token) {
  const res = await axiosClient.put(`/institution/grades/${id}`,
    { subjectId, value: toValue(value), date }, { headers: authHeader(token) });
  return res.data;
}

export async function deleteGrade(id, token) {
  const res = await axiosClient.delete(`/institution/grades/${id}`, { headers: authHeader(token) });
  return res.data;
}
