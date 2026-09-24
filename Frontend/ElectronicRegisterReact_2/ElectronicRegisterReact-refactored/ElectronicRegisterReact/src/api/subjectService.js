// src/api/subjectService.js
import axiosClient, { authHeader } from "./axiosClient";

export async function getSubjects(search, token) {
  const path = search ? `/Subject/byname/${search}` : "/Subject";
  const res = await axiosClient.get(path, { headers: authHeader(token) });
  return res.data;
}

export async function createSubject({ name, teacherId }, token) {
  const res = await axiosClient.post("/Subject", { name, teacherId }, { headers: authHeader(token) });
  return res.data;
}

export async function updateSubject(id, { name, teacherId }, token) {
  const res = await axiosClient.put(`/Subject/update/${id}`, { name, teacherId }, { headers: authHeader(token) });
  return res.data;
}

export async function deleteSubject(id, token) {
  const res = await axiosClient.delete(`/Subject/${id}`, { headers: authHeader(token) });
  return res.data;
}
