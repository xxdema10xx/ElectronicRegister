// src/api/gradeService.js
import axiosClient, { authHeader } from "./axiosClient";

export async function getGradesPage({ pageNumber, pageSize, subjectId, studentId, date }, token) {
  const params = new URLSearchParams({ pageNumber: String(pageNumber), pageSize: String(pageSize) });
  if (subjectId) params.set("subjectId", subjectId);
  if (studentId) params.set("studentId", studentId);
  if (date) params.set("date", date);

  const res = await axiosClient.get(`/Grade/paged?${params.toString()}`, { headers: authHeader(token) });
  return res.data;
}

export async function getGradeFilters(token) {
  const res = await axiosClient.get("/Grade/filters", { headers: authHeader(token) });
  return res.data;
}

export async function createGrade({ studentId, subjectId, value, date }, token) {
  const res = await axiosClient.post(
    "/Grade",
    { studentId, subjectId, value: parseFloat(value), date },
    { headers: authHeader(token) }
  );
  return res.data;
}

export async function updateGrade(id, { subjectId, value, date }, token) {
  const res = await axiosClient.put(
    `/Grade/update/${id}`,
    { subjectId, value: parseFloat(value), date },
    { headers: authHeader(token) }
  );
  return res.data;
}

export async function deleteGrade(id, token) {
  const res = await axiosClient.delete(`/Grade/${id}`, { headers: authHeader(token) });
  return res.data;
}
