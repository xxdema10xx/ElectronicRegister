// src/api/teacherService.js
import axiosClient, { authHeader } from "./axiosClient";

export async function getTeachers(token) {
  const res = await axiosClient.get("/Teacher", { headers: authHeader(token) });
  return res.data;
}

export async function updateTeacher(id, { firstName, lastName }, token) {
  const res = await axiosClient.put(`/Teacher/update/${id}`, { firstName, lastName }, { headers: authHeader(token) });
  return res.data;
}

export async function deleteTeacher(id, token) {
  const res = await axiosClient.delete(`/Teacher/${id}`, { headers: authHeader(token) });
  return res.data;
}
