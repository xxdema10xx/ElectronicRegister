// src/api/testService.js
import axiosClient, { authHeader } from "./axiosClient";

export async function getTests(search, token) {
  const path = search ? `/Test/byname/${search}` : "/Test";
  const res = await axiosClient.get(path, { headers: authHeader(token) });
  return res.data;
}

export async function createTest(
  { name, description, examId, subjectId, numberOfStudents, totalQuestions, closedQuestions, openQuestions, totalPoints, questions, status = "Confirmed" },
  token
) {
  const res = await axiosClient.post(
    "/Test",
    { name, description, examId, subjectId, numberOfStudents, totalQuestions, closedQuestions, openQuestions, totalPoints, questions, status },
    { headers: authHeader(token) }
  );
  return res.data;
}

export async function updateTest(id, { name, description, examId, subjectId, numberOfStudents, totalQuestions, closedQuestions, openQuestions, totalPoints, questions, status }, token) {
  const res = await axiosClient.put(`/Test/update/${id}`, { name, description, examId, subjectId, numberOfStudents, totalQuestions, closedQuestions, openQuestions, totalPoints, questions, status }, { headers: authHeader(token) });
  return res.data;
}

export async function deleteTest(id, token) {
  const res = await axiosClient.delete(`/Test/${id}`, { headers: authHeader(token) });
  return res.data;
}
