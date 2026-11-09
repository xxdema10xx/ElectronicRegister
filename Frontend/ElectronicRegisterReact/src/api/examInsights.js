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

// ── Cache in memoria ─────────────────────────────────────────────────────────────────────────
// Elenco consegne e dettaglio (assegnati) per esame, condivisi tra Home, "Chi deve recuperare" della lista
// e step Studenti del recupero, per sessione (api.cacheKey). Si invalida quando un esame cambia stato o
// quando si correggono consegne (invalidateExamInsights).
// TODO(API): con GET /api/institution/exams/{id}/retake-candidates queste chiamate non servirebbero più.
const caches = new Map(); // cacheKey → { subs: Map(examId → Promise), details: Map(examId → Promise) }
function cacheFor(api) {
  const key = api.cacheKey?.() || "default";
  if (!caches.has(key)) caches.set(key, { subs: new Map(), details: new Map() });
  return caches.get(key);
}
function cached(map, id, load) {
  if (!map.has(id)) map.set(id, load().catch((e) => { map.delete(id); throw e; }));
  return map.get(id);
}
const submissionsOf = (api, examId) => cached(cacheFor(api).subs, examId, () => api.listSubmissions(examId));
const detailOf = (api, examId) => cached(cacheFor(api).details, examId, () => api.getExam(examId));

// examId: solo quell'esame; senza argomenti: tutto
export function invalidateExamInsights(examId) {
  for (const c of caches.values()) {
    if (examId) { c.subs.delete(examId); c.details.delete(examId); }
    else { c.subs.clear(); c.details.clear(); }
  }
}

// ── Chi deve recuperare: una sola regola per step 3, lista e Home ──────────────────────────────
//   "failed"  Deve recuperare: non sufficiente o "non superabile"
//   "missing" Non ha consegnato: nessuna consegna (o non finita) e l'esame è CHIUSO
//   "pending" In correzione: si saprà dopo la correzione
//   "passed"  Superato
// Gli "assenti" esistono solo per gli esami chiusi: per gli altri basta l'elenco delle consegne.
export const RETAKE_LABEL = { failed: "Deve recuperare", missing: "Non ha consegnato", pending: "In correzione", passed: "Superato" };
export const RETAKE_TONE = { failed: "red", missing: "amber", pending: "gray", passed: "green" };

function submissionReason(sub) {
  const s = fromEnum(sub?.status, SUBMISSION_STATUS);
  if (s === "failed_auto" || ((s === "graded" || s === "finalized") && sub.passed === false)) return "failed";
  if (s === "pending_review") return "pending";
  if (s === "graded" || s === "finalized") return "passed";
  return null; // in corso
}

// Studenti di un esame con il loro esito: Map studentId → { reason, firstName, lastName }
// assigned: elenco degli assegnati (serve solo per gli esami chiusi, per trovare chi non ha consegnato)
function classify(submissions, { closed, assigned }) {
  const out = new Map();
  const byStudent = new Map((submissions || []).map(it => [it.studentId, it]));
  for (const it of submissions || []) {
    const reason = submissionReason(it) || (closed ? "missing" : null);
    if (reason) out.set(it.studentId, { reason, firstName: it.studentFirstName, lastName: it.studentLastName });
  }
  if (closed) {
    for (const st of assigned || []) {
      if (!byStudent.has(st.studentId)) out.set(st.studentId, { reason: "missing", firstName: st.firstName, lastName: st.lastName });
    }
  }
  return out;
}

const statusOf = (exam) => fromEnum(exam?.status, EXAM_STATUS);

// "7 da recuperare (2 assenti)": stesse parole dello step Studenti
export function formatRetakeCount(total, absent) {
  if (!total) return "Nessuno da recuperare";
  return `${total} da recuperare${absent ? ` (${absent} ${absent === 1 ? "assente" : "assenti"})` : ""}`;
}

// Esami per cui ha senso guardare le consegne (pubblicati o chiusi)
export function relevantForSubmissions(exams) {
  return exams.filter(e => { const st = statusOf(e); return st === "published" || st === "closed"; });
}

// Consegne di ogni esame pubblicato o chiuso, in parallelo; per i soli esami CHIUSI anche il dettaglio
// (assegnati), per trovare chi non ha consegnato. Tutto passa dalla cache.
async function fetchExamResults(api, exams, { withAssigned }) {
  return Promise.all(relevantForSubmissions(exams).map(async (exam) => {
    const closed = statusOf(exam) === "closed";
    const [items, detail] = await Promise.all([
      submissionsOf(api, exam.id).catch(() => []),
      withAssigned && closed ? detailOf(api, exam.id).catch(() => null) : Promise.resolve(null),
    ]);
    return { exam, items: Array.isArray(items) ? items : [], closed, assigned: detail?.assignedStudents || [] };
  }));
}

// Per ogni studente da recuperare, gli esami e il motivo. Uno studente è "assente" se in nessun esame
// è insufficiente ma non ha consegnato almeno un esame chiuso.
function buildRetakeGroups(results) {
  const byStudent = new Map();
  for (const { exam, items, closed, assigned } of results) {
    for (const [studentId, info] of classify(items, { closed, assigned })) {
      if (info.reason !== "failed" && info.reason !== "missing") continue;
      if (!byStudent.has(studentId)) byStudent.set(studentId, { studentId, firstName: info.firstName, lastName: info.lastName, exams: [] });
      byStudent.get(studentId).exams.push({ id: exam.id, name: exam.name, classSubjectId: exam.classSubjectId, reason: info.reason });
    }
  }
  const groups = [...byStudent.values()].map(g => ({ ...g, absentOnly: g.exams.every(e => e.reason === "missing") }));
  return groups.sort((a, b) => `${a.lastName}${a.firstName}`.localeCompare(`${b.lastName}${b.firstName}`));
}
export const retakeSummary = (groups) => formatRetakeCount(groups.length, groups.filter(g => g.absentOnly).length);

const countPendingReview = (results) => results.reduce((n, { items }) =>
  n + items.filter(it => fromEnum(it.status, SUBMISSION_STATUS) === "pending_review").length, 0);

// "Chi deve recuperare" della lista: si calcola quando si apre
export async function getRetakeGroups(api, exams) {
  return buildRetakeGroups(await fetchExamResults(api, exams, { withAssigned: true }));
}

// Consegne con domande aperte ancora da correggere: lavoro concreto che aspetta il docente.
export async function getPendingReviewCount(api, exams) {
  return countPendingReview(await fetchExamResults(api, exams, { withAssigned: false }));
}

// Step Studenti di un esame di recupero: tutti gli assegnati all'originale con il loro esito.
// Si preseleziona (proposed) solo se l'originale è chiuso e ha consegne: prima non si sa chi deve recuperare.
// Restituisce { closed, hasSubmissions, byStudent: Map studentId → { reason, proposed } }.
// TODO(API): GET /api/institution/exams/{id}/retake-candidates
export async function getRetakeCandidates(api, originalExamId) {
  const [detail, items] = await Promise.all([detailOf(api, originalExamId), submissionsOf(api, originalExamId)]);
  const closed = statusOf(detail) === "closed";
  const list = Array.isArray(items) ? items : [];
  const hasSubmissions = list.some(it => !!submissionReason(it));
  const classified = classify(list, { closed, assigned: detail?.assignedStudents });
  const byStudent = new Map();
  for (const st of detail?.assignedStudents || []) {
    const info = classified.get(st.studentId);
    const reason = info?.reason || (closed ? "missing" : null);
    if (!reason) continue;
    byStudent.set(st.studentId, { reason, proposed: closed && hasSubmissions && (reason === "failed" || reason === "missing") });
  }
  return { closed, hasSubmissions, byStudent };
}

// Le due aggregazioni insieme, con una sola serie di chiamate (utile per la dashboard).
export async function getExamInsights(api, exams) {
  const results = await fetchExamResults(api, exams, { withAssigned: true });
  const retakeGroups = buildRetakeGroups(results);
  return { retakeGroups, retakeSummary: retakeSummary(retakeGroups), pendingReviewCount: countPendingReview(results) };
}
