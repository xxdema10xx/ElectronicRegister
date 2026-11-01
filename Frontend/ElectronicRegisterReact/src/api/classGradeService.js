// src/api/classGradeService.js
// Voti degli studenti visti dal docente in "Classi" (MyClassesScreen): stessi endpoint del registro voti
// (docs/API.md §21). Il server consente al docente solo le materie che insegna nella classe dello studente.
import { createGrade, deleteGrade, getGradesPage, updateGrade } from "./gradeService";

export async function getGradesForStudent(studentId, token) {
  const page = await getGradesPage({ pageNumber: 1, pageSize: 100, studentId }, token);
  const items = Array.isArray(page?.items) ? page.items : [];
  return items.sort((a, b) => String(b.date).localeCompare(String(a.date)));
}
export const addClassGrade = ({ studentId, subjectId, value, date }, token) => createGrade({ studentId, subjectId, value, date }, token);
export const updateClassGrade = (id, { subjectId, value, date }, token) => updateGrade(id, { subjectId, value, date }, token);
export const deleteClassGrade = (id, token) => deleteGrade(id, token);
