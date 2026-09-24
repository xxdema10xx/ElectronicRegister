// src/api/examService.js
import axiosClient, { authHeader } from "./axiosClient";

export async function getExams(search, token) {
  const path = search ? `/Exam/byname/${search}` : "/Exam";
  const res = await axiosClient.get(path, { headers: authHeader(token) });
  return res.data;
}

export async function createExam({ name, subjectId, date }, token) {
  const res = await axiosClient.post("/Exam", { name, subjectId, date }, { headers: authHeader(token) });
  return res.data;
}

export async function updateExam(id, { name, subjectId, date }, token) {
  const res = await axiosClient.put(`/Exam/update/${id}`, { name, subjectId, date }, { headers: authHeader(token) });
  return res.data;
}

export async function deleteExam(id, token) {
  const res = await axiosClient.delete(`/Exam/${id}`, { headers: authHeader(token) });
  return res.data;
}
