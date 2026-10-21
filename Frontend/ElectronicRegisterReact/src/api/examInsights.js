// src/api/examInsights.js
// Aggregazioni sugli esami che richiedono una chiamata per esame (elenco consegne): isolate qui per
// essere riusate sia dalla home che dall'elenco esami, senza duplicare la logica.
import { EXAM_STATUS, SUBMISSION_STATUS, fromEnum } from "./examContract";

// Esami per cui ha senso guardare le consegne (pubblicati o chiusi)
export function relevantForSubmissions(exams) {
  return exams.filter(e => { const st = fromEnum(e.status, EXAM_STATUS); return st === "published" || st === "closed"; });
}

// Per ogni studente in difficoltà, l'elenco degli esami non superati (bocciato o "non superabile")
export async function getRetakeGroups(api, exams) {
  const relevant = relevantForSubmissions(exams);
  const lists = await Promise.all(
    relevant.map(e => api.listSubmissions(e.id).then(items => ({ exam: e, items })).catch(() => ({ exam: e, items: [] })))
  );
  const byStudent = new Map();
  for (const { exam, items } of lists) {
    for (const it of items) {
      const st = fromEnum(it.status, SUBMISSION_STATUS);
      const failed = st === "failed_auto" || ((st === "graded" || st === "finalized") && it.passed === false);
      if (!failed) continue;
      const key = it.studentId;
      if (!byStudent.has(key)) byStudent.set(key, { studentId: key, firstName: it.studentFirstName, lastName: it.studentLastName, exams: [] });
      byStudent.get(key).exams.push({ id: exam.id, name: exam.name, classSubjectId: exam.classSubjectId, status: st });
    }
  }
  return [...byStudent.values()].sort((a, b) => `${a.lastName}${a.firstName}`.localeCompare(`${b.lastName}${b.firstName}`));
}
