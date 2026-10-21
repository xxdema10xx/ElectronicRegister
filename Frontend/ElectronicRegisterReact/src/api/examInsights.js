// src/api/examInsights.js
// Aggregazioni sugli esami che richiedono una chiamata per esame (elenco consegne): isolate qui per
// essere riusate sia dalla home che dall'elenco esami, senza duplicare la logica.
import { EXAM_STATUS, SUBMISSION_STATUS, fromEnum, resultView } from "./examContract";
import { getRun } from "../data/studentExams";

// Libretto dello studente: un esame (appello) per riga, con l'esito calcolato come lo vede lui
// (voto mascherato finché la correzione non è conclusa). Usato sia dalla schermata "Voti" sia dalla
// home, così i due restano sempre coerenti tra loro.
export function buildStudentBooklet(assigned) {
  return assigned
    .filter(a => !!a.submissionStatus)
    .map(a => {
      const run = getRun(a.id);
      const r = resultView(run?.result || { status: a.submissionStatus, totalScore: a.totalScore, maxScore: a.maxScore, grade: a.grade, passed: a.passed });
      return { item: a, r, when: a.submittedAt || a.availableFrom };
    })
    .sort((x, y) => new Date(y.when) - new Date(x.when));
}

// Esami per cui ha senso guardare le consegne (pubblicati o chiusi)
export function relevantForSubmissions(exams) {
  return exams.filter(e => { const st = fromEnum(e.status, EXAM_STATUS); return st === "published" || st === "closed"; });
}

// Una chiamata per esame (pubblicati o chiusi), riusata da tutte le aggregazioni sotto.
async function fetchSubmissionLists(api, exams) {
  const relevant = relevantForSubmissions(exams);
  return Promise.all(
    relevant.map(e => api.listSubmissions(e.id).then(items => ({ exam: e, items })).catch(() => ({ exam: e, items: [] })))
  );
}

function buildRetakeGroups(lists) {
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

const countPendingReview = (lists) => lists.reduce((n, { items }) =>
  n + items.filter(it => fromEnum(it.status, SUBMISSION_STATUS) === "pending_review").length, 0);

// Per ogni studente in difficoltà, l'elenco degli esami non superati (bocciato o "non superabile")
export async function getRetakeGroups(api, exams) {
  return buildRetakeGroups(await fetchSubmissionLists(api, exams));
}

// Consegne con domande aperte ancora da correggere: lavoro concreto che aspetta il docente.
export async function getPendingReviewCount(api, exams) {
  return countPendingReview(await fetchSubmissionLists(api, exams));
}

// Le due aggregazioni insieme, con una sola serie di chiamate (utile per la dashboard).
export async function getExamInsights(api, exams) {
  const lists = await fetchSubmissionLists(api, exams);
  return { retakeGroups: buildRetakeGroups(lists), pendingReviewCount: countPendingReview(lists) };
}
