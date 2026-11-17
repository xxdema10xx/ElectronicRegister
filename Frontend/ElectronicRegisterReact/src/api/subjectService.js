// src/api/subjectService.js
// Materie dell'istituto (docs/API.md §20). La materia non ha un docente: il docente si assegna per classe
// (classi-materie, vedi classService).
import axiosClient, { authHeader } from "./axiosClient";

export async function getSubjects(search, token) {
  if (search) {
    // byname restituisce una sola materia (corrispondenza esatta) oppure 404
    try {
      const res = await axiosClient.get(`/institution/subjects/byname/${encodeURIComponent(search)}`, { headers: authHeader(token) });
      return res.data ? [res.data] : [];
    } catch (e) { if (e.status === 404) return []; throw e; }
  }
  const res = await axiosClient.get("/institution/subjects", { headers: authHeader(token) });
  return res.data;
}

export async function createSubject({ name }, token) {
  const res = await axiosClient.post("/institution/subjects", { name: name.trim() }, { headers: authHeader(token) });
  return res.data;
}

export async function updateSubject(id, { name }, token) {
  const res = await axiosClient.put(`/institution/subjects/${id}`, { name: name.trim() }, { headers: authHeader(token) });
  return res.data;
}

export async function deleteSubject(id, token) {
  const res = await axiosClient.delete(`/institution/subjects/${id}`, { headers: authHeader(token) });
  return res.data;
}
