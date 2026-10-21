// src/api/mockExamBackend.js
// Backend di PROVA per gli esami: stesse funzioni e stessi dati di ExamController, tenuti nel browser
// (localStorage). Si usa solo quando il server risponde 404 su /Exam (vedi examApi.js).
// Le regole di stato, validazione e correzione imitano il backend; in più la consegna segue la regola
// "esame non superabile": chiuse corrette + massimo delle aperte sotto la soglia → non superato (failed_auto).
import { Platform } from "react-native";
import {
  EXAM_STATUS, SUBMISSION_STATUS, QUESTION_TYPE, SUBMIT_REASON, fromEnum, toEnum, questionToDto,
} from "./examContract";
import { MOCK_CLASS_SUBJECTS, MOCK_CLASS_STUDENTS, MOCK_EXAMS } from "../data/examsMock";

const KEY = "mockExamState3";
const DAY = 86400000;
const uid = () => (globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 10)}`);
const clone = (v) => (v === undefined ? undefined : JSON.parse(JSON.stringify(v)));
const round2 = (n) => Math.round(n * 100) / 100;

const bad = (msg) => Object.assign(new Error(msg), { status: 400 });
const forbidden = (msg) => Object.assign(new Error(msg), { status: 403 });
const missing = (msg) => Object.assign(new Error(msg), { status: 404 });
const conflict = (msg) => Object.assign(new Error(msg), { status: 409 });

// ── Stato persistente ───────────────────────────────────────────────────────────────────────
let state = null;
function persist() {
  try { if (Platform.OS === "web") window.localStorage.setItem(KEY, JSON.stringify(state)); } catch {}
}
function load() {
  if (state) return state;
  try {
    const raw = Platform.OS === "web" ? window.localStorage.getItem(KEY) : null;
    if (raw) { state = JSON.parse(raw); return state; }
  } catch {}
  state = seed();
  persist();
  return state;
}
export function resetMockExams() { state = seed(); persist(); }

// SOLO SVILUPPO: elimina la consegna di un unico studente su un unico esame, per poterlo rifare
// mentre si testa l'app. Non è una funzione del backend reale: va chiamata solo dietro __DEV__.
export function devDiscardAttempt(studentId, examId) {
  const st = load();
  st.submissions = Object.fromEntries(Object.entries(st.submissions).filter(([, s]) => !(s.studentId === studentId && s.examId === examId)));
  persist();
}

// ── Regole ──────────────────────────────────────────────────────────────────────────────────
export function convertToGrade(total, max, passingPercent) {
  const p = total / max;
  const s = passingPercent / 100;
  let g = p <= s ? 1 + 5 * (p / s) : 6 + 4 * ((p - s) / (1 - s));
  if (p < s && g >= 6) g = 5.99;
  g = Math.min(10, Math.max(1, g));
  const r = round2(g);
  return total < max * s && r >= 6 ? 5.99 : r;
}

function validateSchedule(from, until, duration) {
  if (!(new Date(from) < new Date(until))) throw bad("La data di inizio deve precedere la data di fine.");
  if (!(duration > 5)) throw bad("La durata dell'esame deve essere maggiore di 5 minuti.");
  if (duration * 60000 > new Date(until) - new Date(from)) throw bad("La durata dell'esame non può superare la finestra di disponibilità.");
}
function validatePassing(p) {
  if (!(p > 0 && p < 100)) throw bad("La percentuale di sufficienza deve essere maggiore di 0 e minore di 100.");
}
function validateQuestions(questions) {
  if (!questions?.length) throw bad("L'esame deve contenere almeno una domanda.");
  const positions = new Set();
  for (const q of questions) {
    if (!q.text?.trim()) throw bad("Il testo della domanda è obbligatorio.");
    if (!(q.points > 0)) throw bad("Ogni domanda deve avere un punteggio maggiore di zero.");
    if (positions.has(q.position)) throw bad("Le posizioni delle domande devono essere univoche.");
    positions.add(q.position);
    const type = fromEnum(q.type, QUESTION_TYPE);
    const correct = (q.options || []).filter(o => o.isCorrect).length;
    if (type === "open") {
      if (q.options?.length) throw bad("Una domanda aperta non può avere opzioni.");
    } else {
      if ((q.options || []).length < 2) throw bad("Le domande chiuse devono avere almeno due opzioni.");
      if (type === "single" && correct !== 1) throw bad("Una domanda a risposta singola deve avere esattamente una risposta corretta.");
      if (type === "multiple" && correct < 1) throw bad("Una domanda a risposta multipla deve avere almeno una risposta corretta.");
    }
  }
}
const totalPoints = (questions) => questions.reduce((sum, q) => sum + q.points, 0);

function studentsOfClass(classId) { return MOCK_CLASS_STUDENTS[classId] || []; }
function classOf(classSubjectId) {
  const cs = MOCK_CLASS_SUBJECTS.find(c => c.id === classSubjectId);
  if (!cs) throw missing("La materia associata alla classe non esiste.");
  return cs.classId;
}
function studentName(id, me) {
  for (const list of Object.values(MOCK_CLASS_STUDENTS)) {
    const s = list.find(x => x.id === id);
    if (s) return s;
  }
  return { id, firstName: me?.firstName || "Tu", lastName: me?.lastName || "" };
}

function buildQuestions(inputs) {
  return inputs.map(q => ({
    id: uid(), position: q.position, text: q.text.trim(), type: q.type, points: q.points, required: !!q.required,
    options: (q.options || []).map(o => ({ id: uid(), position: o.position, text: o.text, isCorrect: !!o.isCorrect })),
  }));
}
function buildAssigned(inputs, classId) {
  const ids = new Set();
  return inputs.map(s => {
    if (ids.has(s.studentId)) throw bad("Uno studente non può essere assegnato più volte.");
    ids.add(s.studentId);
    if ((s.extraTimeMinutes || 0) < 0) throw bad("Il tempo aggiuntivo non può essere negativo.");
    const st = studentsOfClass(classId).find(x => x.id === s.studentId);
    if (!st) throw bad("Tutti gli studenti devono appartenere alla classe selezionata.");
    return { studentId: st.id, firstName: st.firstName, lastName: st.lastName, extraTimeMinutes: s.extraTimeMinutes || 0, assignedAt: new Date().toISOString() };
  });
}

// ── Correzione ──────────────────────────────────────────────────────────────────────────────
// Corregge le domande chiuse (tutto o niente) e calcola i totali
function gradeClosed(exam, sub) {
  let closedScore = 0;
  let openMax = 0;
  for (const q of exam.questions) {
    const type = fromEnum(q.type, QUESTION_TYPE);
    const ans = sub.answers.find(a => a.questionId === q.id);
    if (type === "open") {
      openMax += q.points;
      if (ans) { ans.isCorrect = null; ans.awardedPoints = ans.awardedPoints ?? null; }
      continue;
    }
    if (!ans) continue;
    const correct = q.options.filter(o => o.isCorrect).map(o => o.id).sort();
    const picked = [...new Set(ans.selectedOptionIds)].sort();
    ans.isCorrect = correct.length > 0 && correct.length === picked.length && correct.every((id, i) => id === picked[i]);
    ans.awardedPoints = ans.isCorrect ? q.points : 0;
    if (ans.isCorrect) closedScore += q.points;
  }
  const maxScore = totalPoints(exam.questions);
  return { closedScore, openMax, maxScore, passingScore: maxScore * exam.passingPercent / 100 };
}

function applyAnswers(sub, inputs) {
  sub.answers = (inputs || []).map(a => ({
    id: uid(), questionId: a.questionId, openText: a.openText ?? null,
    selectedOptionIds: a.selectedOptionIds || [], isCorrect: null, awardedPoints: null, teacherComment: null,
  }));
}

function submitCore(exam, sub, reason) {
  const g = gradeClosed(exam, sub);
  sub.closedScore = g.closedScore;
  sub.openMaxPoints = g.openMax;
  sub.maxScore = g.maxScore;
  sub.passingScore = g.passingScore;
  sub.openScore = 0;
  sub.submittedAt = new Date().toISOString();
  sub.submitReason = reason;
  if (g.openMax <= 0) {
    sub.status = "graded";
    sub.totalScore = g.closedScore;
    sub.suggestedGrade = convertToGrade(g.closedScore, g.maxScore, exam.passingPercent);
    sub.finalGrade = sub.suggestedGrade;
    sub.passed = sub.suggestedGrade >= 6;
  } else if (g.closedScore + g.openMax < g.passingScore) {
    sub.status = "failed_auto"; // la soglia non è più raggiungibile, qualunque punteggio nelle aperte
    sub.totalScore = null;
    sub.passed = false;
  } else {
    sub.status = "pending_review";
    sub.totalScore = null;
    sub.passed = null;
  }
}

// ── DTO ─────────────────────────────────────────────────────────────────────────────────────
const statusInt = (s) => toEnum(s, EXAM_STATUS);
function examSummary(e) {
  return {
    id: e.id, classSubjectId: e.classSubjectId, retakeOfExamId: e.retakeOfExamId, name: e.name, description: e.description,
    availableFrom: e.availableFrom, availableUntil: e.availableUntil, durationMinutes: e.durationMinutes, passingPercent: e.passingPercent,
    totalPoints: e.totalPoints, status: statusInt(e.status), assignedStudentsCount: e.assignedStudents.length,
    publishedAt: e.publishedAt, closedAt: e.closedAt, cancelledAt: e.cancelledAt, isRetake: !!e.retakeOfExamId,
  };
}
const examDetail = (e) => ({ ...examSummary(e), questions: e.questions, assignedStudents: e.assignedStudents });

function submissionSummary(s, me) {
  const st = studentName(s.studentId, me);
  return {
    id: s.id, examId: s.examId, studentId: s.studentId, studentFirstName: st.firstName, studentLastName: st.lastName,
    status: toEnum(s.status, SUBMISSION_STATUS), startedAt: s.startedAt, deadlineAt: s.deadlineAt, submittedAt: s.submittedAt,
    submitReason: s.submitReason ? toEnum(s.submitReason, SUBMIT_REASON) : null, violationCount: s.violationCount || 0,
    totalScore: s.totalScore ?? null, maxScore: s.maxScore ?? null, passed: s.passed ?? null, gradeId: s.finalGrade != null ? `g-${s.id}` : null,
  };
}

// L'esito allo studente: nessun punteggio né voto finché la correzione non è conclusa
function resultDto(s, role) {
  const hidden = role === "student" && (s.status === "pending_review" || s.status === "failed_auto");
  const grade = s.status === "finalized" || s.status === "graded" ? (s.finalGrade ?? s.suggestedGrade ?? null) : null;
  return {
    submissionId: s.id, examId: s.examId, status: toEnum(s.status, SUBMISSION_STATUS), submittedAt: s.submittedAt,
    submitReason: s.submitReason ? toEnum(s.submitReason, SUBMIT_REASON) : null,
    totalScore: hidden ? null : s.totalScore ?? null, maxScore: hidden ? null : s.maxScore ?? null,
    grade: hidden ? null : grade, passed: hidden ? null : s.passed ?? null,
  };
}

function reviewDto(s, exam, me) {
  const st = studentName(s.studentId, me);
  return {
    id: s.id, examId: s.examId, examName: exam.name, studentId: s.studentId, studentFirstName: st.firstName, studentLastName: st.lastName,
    status: toEnum(s.status, SUBMISSION_STATUS), startedAt: s.startedAt, deadlineAt: s.deadlineAt, submittedAt: s.submittedAt,
    submitReason: s.submitReason ? toEnum(s.submitReason, SUBMIT_REASON) : null, violationCount: s.violationCount || 0,
    maxScore: s.maxScore ?? null, passingScore: s.passingScore ?? null, closedScore: s.closedScore ?? null,
    openMaxPoints: s.openMaxPoints ?? null, openScore: s.openScore ?? null, totalScore: s.totalScore ?? null, passed: s.passed ?? null,
    suggestedGrade: s.suggestedGrade ?? null, gradeId: s.finalGrade != null && s.status === "finalized" ? `g-${s.id}` : null,
    finalGrade: s.finalGrade ?? null, finalizedAt: s.finalizedAt ?? null,
    answers: [...exam.questions].sort((a, b) => a.position - b.position).map(q => {
      const a = s.answers.find(x => x.questionId === q.id);
      return {
        answerId: a?.id || `${s.id}-${q.id}`, questionId: q.id, position: q.position, questionText: q.text, questionType: q.type,
        maxPoints: q.points, required: q.required, openText: a?.openText ?? null, isCorrect: a?.isCorrect ?? null,
        awardedPoints: a?.awardedPoints ?? null, teacherComment: a?.teacherComment ?? null,
        options: [...q.options].sort((x, y) => x.position - y.position).map(o => ({
          id: o.id, position: o.position, text: o.text, isCorrect: o.isCorrect, isSelected: !!a?.selectedOptionIds.includes(o.id),
        })),
      };
    }),
  };
}

// Consegna di esempio già corretta dal motore (usata sia per i dati iniziali sia per il libretto demo
// che si genera al primo accesso di un vero studente, così c'è sempre qualcosa da vedere).
const answerPlan = (exam, wrongClosed = [], skipOpen = false) => exam.questions.map((q, i) => {
  const type = fromEnum(q.type, QUESTION_TYPE);
  if (type === "open") return skipOpen ? null : { questionId: q.id, openText: "Risposta di esempio dello studente.", selectedOptionIds: [] };
  const correct = q.options.filter(o => o.isCorrect).map(o => o.id);
  const wrong = q.options.filter(o => !o.isCorrect).map(o => o.id);
  const ids = wrongClosed.includes(i) ? (wrong.length ? [wrong[0]] : []) : correct;
  return { questionId: q.id, openText: null, selectedOptionIds: ids };
}).filter(Boolean);

function buildSubmission(st, exam, studentId, { wrongClosed = [], skipOpen = false, finalize = false, hoursAgo = 3 } = {}) {
  const t = Date.now();
  const start = new Date(t - hoursAgo * 3600000).toISOString();
  const sub = {
    id: uid(), examId: exam.id, studentId, status: "in_progress", startedAt: start,
    deadlineAt: new Date(t - hoursAgo * 3600000 + exam.durationMinutes * 60000).toISOString(), revision: 1, answers: [], violationCount: 0,
  };
  applyAnswers(sub, answerPlan(exam, wrongClosed, skipOpen));
  submitCore(exam, sub, "manual");
  if (finalize) {
    for (const a of sub.answers) {
      const q = exam.questions.find(x => x.id === a.questionId);
      if (fromEnum(q.type, QUESTION_TYPE) === "open") { a.awardedPoints = Math.round(q.points * 0.7); a.teacherComment = "Buon lavoro."; }
    }
    sub.openScore = sub.answers.reduce((n, a) => n + (fromEnum(exam.questions.find(x => x.id === a.questionId).type, QUESTION_TYPE) === "open" ? a.awardedPoints || 0 : 0), 0);
    sub.totalScore = sub.closedScore + sub.openScore;
    sub.suggestedGrade = convertToGrade(sub.totalScore, sub.maxScore, exam.passingPercent);
    sub.finalGrade = sub.suggestedGrade;
    sub.passed = sub.suggestedGrade >= 6;
    sub.status = "finalized";
    sub.finalizedAt = new Date(t - hoursAgo * 3600000 + 2 * 3600000).toISOString();
  }
  st.submissions[sub.id] = sub;
  return sub;
}

// ── Dati iniziali ───────────────────────────────────────────────────────────────────────────
function seed() {
  const st = { exams: {}, submissions: {} };
  const t = Date.now();
  const byKey = {};
  for (const m of MOCK_EXAMS) {
    const id = `ex-${m.key}`;
    byKey[m.key] = id;
    const cl = classOf(m.classSubjectId);
    const questions = m.questions.map((q, i) => {
      const dto = questionToDto(q, i);
      return { id: q.id, ...dto, options: dto.options.map((o, j) => ({ id: q.options[j].id, ...o })) };
    });
    const range = m.status === "draft" ? [t + 2 * DAY, t + 9 * DAY] : m.status === "closed" ? [t - 10 * DAY, t - DAY] : [t - DAY, t + 30 * DAY];
    st.exams[id] = {
      id, classSubjectId: m.classSubjectId, retakeOfExamId: m.retakeOf ? byKey[m.retakeOf] : null, name: m.name, description: m.description,
      availableFrom: new Date(range[0]).toISOString(), availableUntil: new Date(range[1]).toISOString(),
      durationMinutes: m.duration, passingPercent: m.passing, totalPoints: totalPoints(questions), status: m.status,
      publishedAt: m.status === "draft" ? null : new Date(t - 12 * DAY).toISOString(),
      closedAt: m.status === "closed" ? new Date(t - DAY).toISOString() : null, cancelledAt: null, questions,
      assignedStudents: studentsOfClass(cl).map((s, i) => ({
        studentId: s.id, firstName: s.firstName, lastName: s.lastName, extraTimeMinutes: i === 2 ? 10 : 0, assignedAt: new Date(t - 13 * DAY).toISOString(),
      })),
    };
  }
  state = st;

  const make = (examKey, studentId, wrongClosed, skipOpen, finalize) =>
    buildSubmission(st, st.exams[byKey[examKey]], studentId, { wrongClosed, skipOpen, finalize });
  make("manzoni", "stu-a1", [], false);          // chiuse tutte giuste, aperte da correggere → in correzione
  make("manzoni", "stu-a2", [0], false);         // una chiusa sbagliata → in correzione
  make("js", "stu-a3", [0, 1, 2, 3], true);      // chiuse tutte sbagliate e aperte vuote → non superato in automatico
  make("db", "stu-a4", [], false);               // in correzione
  make("grammar", "stu-a5", [], false);          // solo chiuse, tutto giusto → voto immediato
  make("grammar", "stu-a6", [0, 1], false);      // solo chiuse, due sbagliate → voto immediato
  make("react", "stu-a7", [], false, true);      // già valutato dal docente
  return st;
}

// Al primo accesso di un vero studente (id diverso da quelli finti sopra) non c'è nessuna consegna
// a suo nome: gli si genera un libretto di esempio variato, così la UI non è mai vuota per davvero.
function ensureStudentDemo(studentId) {
  const st = load();
  if (!studentId || Object.values(st.submissions).some(s => s.studentId === studentId)) return;
  const plan = [
    ["grammar", { wrongClosed: [] }],            // solo chiuse, tutto giusto → voto immediato, superato
    ["manzoni", { wrongClosed: [0] }],            // una chiusa sbagliata, aperte da correggere → in correzione
    ["js", { wrongClosed: [0, 1, 2, 3], skipOpen: true }], // non superato in automatico
    ["react", { finalize: true }],                // già valutato dal docente
  ];
  let hoursAgo = 72;
  let any = false;
  for (const [key, opts] of plan) {
    const exam = st.exams[`ex-${key}`]; // stesso schema di id usato in seed()
    if (!exam) continue;
    try { buildSubmission(st, exam, studentId, { ...opts, hoursAgo }); any = true; }
    catch { /* un esempio che fallisce non deve bloccare gli altri */ }
    hoursAgo -= 18;
  }
  if (any) persist();
}

// ── API di prova ────────────────────────────────────────────────────────────────────────────
export function createMockExamApi(user) {
  const role = user?.role?.toLowerCase();
  const me = { id: user?.studentId || "stu-me", firstName: user?.studentFirstName, lastName: user?.studentLastName };
  if (role === "student") ensureStudentDemo(me.id);
  const wrap = (fn) => async (...args) => { load(); return clone(await fn(...args)); };
  const exam = (id) => load().exams[id] || (() => { throw missing("L'esame non esiste."); })();
  const sub = (id) => load().submissions[id] || (() => { throw missing("La submission non esiste."); })();
  const staffOnly = () => { if (role !== "teacher" && role !== "admin") throw forbidden("Non sei autorizzato."); };
  const studentOnly = () => { if (role !== "student") throw forbidden("Non sei autorizzato."); };
  const hasSubmissions = (examId) => Object.values(load().submissions).some(s => s.examId === examId);

  return {
    mock: true,

    getClassSubjects: wrap(async () => MOCK_CLASS_SUBJECTS),
    getStudentsOfClassSubject: wrap(async (classSubjectId) => classSubjectId ? studentsOfClass(classOf(classSubjectId)) : []),

    listExams: wrap(async () => { staffOnly(); return Object.values(load().exams).map(examSummary); }),
    getExam: wrap(async (id) => { staffOnly(); return examDetail(exam(id)); }),

    createExam: wrap(async (dto) => {
      staffOnly();
      const classId = classOf(dto.classSubjectId);
      if (!dto.name?.trim()) throw bad("Il nome dell'esame è obbligatorio.");
      validateSchedule(dto.availableFrom, dto.availableUntil, dto.durationMinutes);
      validatePassing(dto.passingPercent);
      validateQuestions(dto.questions);
      if (dto.retakeOfExamId) {
        const orig = load().exams[dto.retakeOfExamId];
        if (!orig) throw missing("L'esame originale non esiste.");
        if (orig.classSubjectId !== dto.classSubjectId) throw bad("Il recupero deve appartenere alla stessa classe-materia.");
      }
      const id = uid();
      const questions = buildQuestions(dto.questions);
      load().exams[id] = {
        id, classSubjectId: dto.classSubjectId, retakeOfExamId: dto.retakeOfExamId || null, name: dto.name.trim(), description: dto.description || null,
        availableFrom: dto.availableFrom, availableUntil: dto.availableUntil, durationMinutes: dto.durationMinutes, passingPercent: dto.passingPercent,
        totalPoints: totalPoints(questions), status: "draft", publishedAt: null, closedAt: null, cancelledAt: null, questions,
        assignedStudents: buildAssigned(dto.assignedStudents || [], classId),
      };
      persist();
      return id;
    }),

    updateExam: wrap(async (id, dto) => {
      staffOnly();
      const e = exam(id);
      if (e.status !== "draft") throw conflict("La struttura dell'esame può essere modificata solo quando l'esame è in bozza.");
      if (dto.classSubjectId !== e.classSubjectId) throw bad("ClassSubjectId cannot be changed for an existing exam.");
      if (!dto.name?.trim()) throw bad("Il nome dell'esame è obbligatorio.");
      validateSchedule(dto.availableFrom, dto.availableUntil, dto.durationMinutes);
      validatePassing(dto.passingPercent);
      validateQuestions(dto.questions);
      const questions = buildQuestions(dto.questions);
      Object.assign(e, {
        retakeOfExamId: dto.retakeOfExamId || null, name: dto.name.trim(), description: dto.description || null, availableFrom: dto.availableFrom,
        availableUntil: dto.availableUntil, durationMinutes: dto.durationMinutes, passingPercent: dto.passingPercent, questions, totalPoints: totalPoints(questions),
      });
      persist();
    }),

    updateExamStudents: wrap(async (id, dto) => {
      staffOnly();
      const e = exam(id);
      if (e.status !== "draft") throw conflict("La struttura dell'esame può essere modificata solo quando l'esame è in bozza.");
      e.assignedStudents = buildAssigned(dto.students || [], classOf(e.classSubjectId));
      persist();
    }),

    publishExam: wrap(async (id) => {
      staffOnly();
      const e = exam(id);
      if (e.status !== "draft") throw conflict("Solo un esame in bozza può essere pubblicato.");
      validateSchedule(e.availableFrom, e.availableUntil, e.durationMinutes);
      validateQuestions(e.questions);
      if (new Date(e.availableUntil) <= new Date()) throw conflict("Non è possibile pubblicare un esame la cui disponibilità è già terminata.");
      if (!e.assignedStudents.length) throw conflict("L'esame deve essere assegnato ad almeno uno studente.");
      e.status = "published";
      e.publishedAt = new Date().toISOString();
      persist();
    }),
    closeExam: wrap(async (id) => {
      staffOnly();
      const e = exam(id);
      if (e.status !== "published") throw conflict("È possibile chiudere solo un esame pubblicato.");
      e.status = "closed";
      e.closedAt = new Date().toISOString();
      persist();
    }),
    cancelExam: wrap(async (id) => {
      staffOnly();
      const e = exam(id);
      if (e.status !== "published") throw conflict("È possibile annullare solo un esame pubblicato.");
      if (hasSubmissions(id)) throw conflict("Non è possibile annullare un esame già iniziato da almeno uno studente.");
      e.status = "cancelled";
      e.cancelledAt = new Date().toISOString();
      persist();
    }),
    deleteExam: wrap(async (id) => {
      staffOnly();
      const e = exam(id);
      if (e.status !== "draft") throw conflict("È possibile eliminare solo un esame in bozza.");
      if (hasSubmissions(id)) throw conflict("Non è possibile eliminare un esame che possiede già delle consegne.");
      if (Object.values(load().exams).some(x => x.retakeOfExamId === id)) throw conflict("An exam referenced by a retake cannot be deleted.");
      delete load().exams[id];
      persist();
    }),

    // ── Studente ──
    getAssigned: wrap(async () => {
      studentOnly();
      const now = Date.now();
      const mine = Object.values(load().submissions).filter(s => s.studentId === me.id);
      return Object.values(load().exams)
        .filter(e => e.status === "published" || mine.some(s => s.examId === e.id))
        .map(e => {
          const s = mine.find(x => x.examId === e.id);
          // Stesso mascheramento di resultDto: niente voto/punteggio finché la correzione non è conclusa.
          const hidden = s && (s.status === "pending_review" || s.status === "failed_auto");
          const grade = s && (s.status === "finalized" || s.status === "graded") ? (s.finalGrade ?? s.suggestedGrade ?? null) : null;
          return {
            id: e.id, classSubjectId: e.classSubjectId, name: e.name, description: e.description, availableFrom: e.availableFrom,
            availableUntil: e.availableUntil, durationMinutes: e.durationMinutes, extraTimeMinutes: 0, status: statusInt(e.status),
            submissionStatus: s ? toEnum(s.status, SUBMISSION_STATUS) : null, startedAt: s?.startedAt ?? null, deadlineAt: s?.deadlineAt ?? null,
            submittedAt: s?.submittedAt ?? null,
            totalScore: s && !hidden ? s.totalScore ?? null : null, maxScore: s && !hidden ? s.maxScore ?? null : null,
            grade: hidden ? null : grade, passed: s && !hidden ? s.passed ?? null : null,
            canStart: e.status === "published" && now >= new Date(e.availableFrom) && now < new Date(e.availableUntil) && !s, isRetake: !!e.retakeOfExamId,
          };
        });
    }),

    startExam: wrap(async (id) => {
      studentOnly();
      const e = exam(id);
      const now = Date.now();
      if (e.status !== "published" || now < new Date(e.availableFrom) || now >= new Date(e.availableUntil)) throw conflict("L'esame non è disponibile in questo momento.");
      if (Object.values(load().submissions).some(s => s.examId === id && s.studentId === me.id)) throw conflict("Hai già iniziato o completato questo esame.");
      const startedAt = new Date(now);
      const s = {
        id: uid(), examId: id, studentId: me.id, status: "in_progress", startedAt: startedAt.toISOString(),
        deadlineAt: new Date(now + e.durationMinutes * 60000).toISOString(), revision: 0, answers: [], violationCount: 0,
      };
      load().submissions[s.id] = s;
      persist();
      return {
        submissionId: s.id, startedAt: s.startedAt, deadlineAt: s.deadlineAt,
        exam: {
          id: e.id, name: e.name, description: e.description, availableUntil: e.availableUntil, durationMinutes: e.durationMinutes, totalPoints: e.totalPoints,
          questions: e.questions.map(q => ({ id: q.id, position: q.position, text: q.text, type: q.type, points: q.points, required: q.required,
            options: q.options.map(o => ({ id: o.id, position: o.position, text: o.text })) })),
        },
      };
    }),

    saveAnswers: wrap(async (submissionId, dto) => {
      studentOnly();
      const s = sub(submissionId);
      if (s.studentId !== me.id) throw forbidden("Non sei autorizzato a modificare questa submission.");
      if (s.status !== "in_progress") throw conflict("La submission non è in stato InProgress.");
      const accepted = dto.clientRevision > s.revision;
      if (accepted) { applyAnswers(s, dto.answers); s.revision = dto.clientRevision; persist(); }
      return { accepted, currentRevision: s.revision, savedAt: new Date().toISOString() };
    }),

    submitExam: wrap(async (submissionId, dto) => {
      studentOnly();
      const s = sub(submissionId);
      if (s.studentId !== me.id) throw forbidden("Non sei autorizzato a inviare questa submission.");
      if (s.status !== "in_progress") throw conflict("La submission non è in stato InProgress.");
      const e = exam(s.examId);
      if (dto.clientRevision >= s.revision) { applyAnswers(s, dto.answers); s.revision = dto.clientRevision; }
      const answered = new Set(s.answers.map(a => a.questionId));
      if (e.questions.some(q => q.required && !answered.has(q.id))) throw bad("Non tutte le domande obbligatorie sono state compilate.");
      submitCore(e, s, new Date() > new Date(s.deadlineAt) ? "time_expired" : "manual");
      persist();
      return resultDto(s, role);
    }),

    getResult: wrap(async (submissionId) => resultDto(sub(submissionId), role)),

    // ── Correzione ──
    listSubmissions: wrap(async (examId, status) => {
      staffOnly();
      exam(examId);
      const wanted = status === undefined || status === null || status === "" ? null : fromEnum(status, SUBMISSION_STATUS);
      return Object.values(load().submissions).filter(s => s.examId === examId && (!wanted || s.status === wanted)).map(s => submissionSummary(s, me));
    }),
    getReview: wrap(async (submissionId) => {
      staffOnly();
      const s = sub(submissionId);
      return reviewDto(s, exam(s.examId), me);
    }),
    reviewSubmission: wrap(async (submissionId, dto) => {
      staffOnly();
      const s = sub(submissionId);
      const e = exam(s.examId);
      if (s.status === "in_progress") throw conflict("La submission non è ancora stata consegnata.");
      if (s.status === "finalized") throw conflict("La submission è già stata finalizzata.");
      for (const input of dto.answers || []) {
        const a = s.answers.find(x => x.id === input.answerId);
        if (!a) throw missing(`Answer ${input.answerId} non trovato nella submission.`);
        const q = e.questions.find(x => x.id === a.questionId);
        if (input.awardedPoints < 0 || input.awardedPoints > q.points) throw bad("Punteggio non valido per la domanda.");
        a.awardedPoints = input.awardedPoints;
        a.teacherComment = input.comment ?? null;
      }
      const openQ = new Set(e.questions.filter(q => fromEnum(q.type, QUESTION_TYPE) === "open").map(q => q.id));
      s.openScore = s.answers.filter(a => openQ.has(a.questionId)).reduce((n, a) => n + (a.awardedPoints || 0), 0);
      s.totalScore = (s.closedScore || 0) + s.openScore;
      s.suggestedGrade = convertToGrade(s.totalScore, s.maxScore, e.passingPercent);
      persist();
    }),
    finalizeSubmission: wrap(async (submissionId, dto) => {
      staffOnly();
      const s = sub(submissionId);
      const e = exam(s.examId);
      if (s.status === "in_progress") throw conflict("La submission non è ancora stata consegnata.");
      if (s.status === "finalized") throw conflict("La submission è già stata finalizzata.");
      if (s.suggestedGrade == null || s.totalScore == null) {
        s.openScore = s.openScore ?? 0;
        s.totalScore = (s.closedScore || 0) + (s.openScore || 0);
        s.suggestedGrade = convertToGrade(s.totalScore, s.maxScore, e.passingPercent);
      }
      const final = dto.finalGrade ?? s.suggestedGrade;
      if (!(final >= 1 && final <= 10)) throw bad("Il valore del voto deve essere compreso tra 1 e 10.");
      if (round2(final) !== round2(s.suggestedGrade) && !dto.overrideReason?.trim()) throw bad("OverrideReason is required when FinalGrade differs from SuggestedGrade.");
      s.finalGrade = round2(final);
      s.passed = s.finalGrade >= 6;
      s.status = "finalized";
      s.finalizedAt = new Date().toISOString();
      persist();
    }),
  };
}
