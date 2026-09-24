// src/api/testService.js
import axiosClient, { authHeader } from "./axiosClient";

export async function getTests(search, token) {
  const path = search ? `/Test/byname/${search}` : "/Test";
  const res = await axiosClient.get(path, { headers: authHeader(token) });
  return res.data;
}

export async function createTest({ name, examId, subjectId, numberOfStudents, totalQuestions, closedQuestions, openQuestions }, token) {
  const res = await axiosClient.post("/Test", { name, examId, subjectId, numberOfStudents, totalQuestions, closedQuestions, openQuestions }, { headers: authHeader(token) });
  return res.data;
}

export async function updateTest(id, { name, examId, subjectId, numberOfStudents, totalQuestions, closedQuestions, openQuestions }, token) {
  const res = await axiosClient.put(`/Test/update/${id}`, { name, examId, subjectId, numberOfStudents, totalQuestions, closedQuestions, openQuestions }, { headers: authHeader(token) });
  return res.data;
}

export async function deleteTest(id, token) {
  const res = await axiosClient.delete(`/Test/${id}`, { headers: authHeader(token) });
  return res.data;
}