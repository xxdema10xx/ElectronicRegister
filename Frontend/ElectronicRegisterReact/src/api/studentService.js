// src/api/studentService.js
import axiosClient, { authHeader } from "./axiosClient";

export async function getStudents(search, token) {
  const path = search ? `/Student/bylastname/${search}` : "/Student";
  const res = await axiosClient.get(path, { headers: authHeader(token) });
  return res.data;
}

export async function updateStudent(id, { firstName, lastName }, token) {
  const res = await axiosClient.put(`/Student/update/${id}`, { firstName, lastName }, { headers: authHeader(token) });
  return res.data;
}

export async function deleteStudent(id, token) {
  const res = await axiosClient.delete(`/Student/${id}`, { headers: authHeader(token) });
  return res.data;
}
