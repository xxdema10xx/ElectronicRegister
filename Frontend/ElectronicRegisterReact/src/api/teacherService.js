// src/api/teacherService.js
// Anagrafica docenti dell'istituto (docs/API.md §19)
import axiosClient, { authHeader } from "./axiosClient";

export async function getTeachers(token) {
  const res = await axiosClient.get("/institution/teachers", { headers: authHeader(token) });
  return res.data;
}

export async function createTeacher({ firstName, lastName }, token) {
  const res = await axiosClient.post("/institution/teachers", { firstName: firstName.trim(), lastName: lastName.trim() }, { headers: authHeader(token) });
  return res.data; // id
}

export async function updateTeacher(id, { firstName, lastName }, token) {
  const res = await axiosClient.put(`/institution/teachers/${id}`, { firstName: firstName.trim(), lastName: lastName.trim() }, { headers: authHeader(token) });
  return res.data;
}

export async function deleteTeacher(id, token) {
  const res = await axiosClient.delete(`/institution/teachers/${id}`, { headers: authHeader(token) });
  return res.data;
}
