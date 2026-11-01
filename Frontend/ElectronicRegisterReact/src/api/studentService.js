// src/api/studentService.js
// Anagrafica studenti dell'istituto (docs/API.md §17)
import axiosClient, { authHeader } from "./axiosClient";

export async function getStudents(search, token, classId) {
  const path = search ? `/institution/students/bylastname/${encodeURIComponent(search)}` : "/institution/students";
  const res = await axiosClient.get(path, { headers: authHeader(token), params: !search && classId ? { classId } : undefined });
  return res.data;
}

export async function createStudent({ firstName, lastName }, token, classId) {
  const res = await axiosClient.post("/institution/students", { firstName: firstName.trim(), lastName: lastName.trim() },
    { headers: authHeader(token), params: classId ? { classId } : undefined });
  return res.data; // id
}

export async function updateStudent(id, { firstName, lastName }, token) {
  const res = await axiosClient.put(`/institution/students/${id}`, { firstName: firstName.trim(), lastName: lastName.trim() }, { headers: authHeader(token) });
  return res.data;
}

export async function deleteStudent(id, token) {
  const res = await axiosClient.delete(`/institution/students/${id}`, { headers: authHeader(token) });
  return res.data;
}
