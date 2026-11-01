// src/api/examApi.js
// Esami e consegne (docs/API.md §22–23). Se il server non espone gli esami (risponde 404 al primo controllo)
// si passa automaticamente alla modalità di prova (src/api/mockExamBackend.js), che ha le stesse funzioni e
// gli stessi dati. Nelle schermate si usa sempre l'oggetto restituito da createExamApi().
import axiosClient, { authHeader } from "./axiosClient";
import { createMockExamApi } from "./mockExamBackend";

function realApi(token) {
  const cfg = (params) => ({ headers: authHeader(token), params });
  const get = async (path, params) => (await axiosClient.get(path, cfg(params))).data;
  const post = async (path, body) => (await axiosClient.post(path, body, cfg())).data;
  const put = async (path, body) => (await axiosClient.put(path, body, cfg())).data;
  const del = async (path) => (await axiosClient.delete(path, cfg())).data;

  // Le classi-materia servono anche per sapere a quale classe appartiene un esame
  let classSubjectsCache = null;
  async function classSubjects() {
    if (!classSubjectsCache) {
      const list = await get("/institution/class-subjects", { pageNumber: 1, pageSize: 100 });
      classSubjectsCache = (Array.isArray(list) ? list : []).map(c => ({ ...c, label: [c.className, c.subjectName].filter(Boolean).join(" · ") }));
    }
    return classSubjectsCache;
  }

  return {
    mock: false,

    // Elenco classi-materia [{ id, classId, className, subjectName, label, ... }]; null = non disponibile per il ruolo
    getClassSubjects: async () => {
      try { return await classSubjects(); }
      catch (e) { if (e.status === 404 || e.status === 403) return null; throw e; }
    },
    // Studenti della classe della classe-materia (sono gli unici assegnabili all'esame)
    getStudentsOfClassSubject: async (classSubjectId) => {
      const cs = (await classSubjects().catch(() => [])).find(c => c.id === classSubjectId);
      return get("/institution/students", cs ? { classId: cs.classId } : undefined);
    },

    listExams: () => get("/institution/exams"),
    getExam: (id) => get(`/institution/exams/${id}`),
    createExam: (dto) => post("/institution/exams", dto),
    // L'aggiornamento non accetta gli studenti (si assegnano con updateExamStudents)
    updateExam: (id, { assignedStudents, ...dto }) => put(`/institution/exams/${id}`, dto),
    updateExamStudents: (id, dto) => put(`/institution/exams/${id}/students`, dto),
    publishExam: (id) => post(`/institution/exams/${id}/publish`),
    closeExam: (id) => post(`/institution/exams/${id}/close`),
    cancelExam: (id) => post(`/institution/exams/${id}/cancel`),
    deleteExam: (id) => del(`/institution/exams/${id}`),

    getAssigned: () => get("/institution/exams/assigned"),
    startExam: (examId) => post(`/institution/exams/${examId}/start`),
    // Attenzione: da qui in poi l'id è quello della CONSEGNA (submissionId)
    getSession: (submissionId) => get(`/institution/submissions/${submissionId}/session`),
    saveAnswers: (submissionId, dto) => put(`/institution/submissions/${submissionId}/answers`, dto),
    submitExam: (submissionId, dto) => post(`/institution/submissions/${submissionId}/submit`, dto),
    // Consegna a tempo scaduto: nessun body, il server usa le risposte già salvate
    submitExpired: (submissionId) => post(`/institution/submissions/${submissionId}/submit-expired`),
    reportViolation: (submissionId, dto) => post(`/institution/submissions/${submissionId}/violations`, dto),
    getResult: (submissionId) => get(`/institution/submissions/${submissionId}/result`),

    listSubmissions: (examId, status) => get(`/institution/exams/${examId}/submissions`, status === undefined || status === null || status === "" ? undefined : { status }),
    getReview: (submissionId) => get(`/institution/submissions/${submissionId}/review`),
    reviewSubmission: (submissionId, dto) => put(`/institution/submissions/${submissionId}/review`, dto),
    finalizeSubmission: (submissionId, dto) => post(`/institution/submissions/${submissionId}/finalize`, dto),
  };
}

// La modalità si decide una volta sola, al primo controllo riuscito (o fallito con 404).
let mode = null; // "real" | "mock"
let pending = null;

async function resolveMode(token, role) {
  if (mode) return mode;
  if (!pending) {
    const real = realApi(token);
    const probe = role === "student" ? real.getAssigned() : real.listExams();
    pending = probe
      .then(() => { mode = "real"; })
      .catch((e) => { mode = e.status === 404 ? "mock" : "real"; }) // altri errori (401, 403, rete) sono veri e si mostrano
      .finally(() => { pending = null; });
  }
  await pending;
  return mode;
}

// Per le prove: forza una modalità (null = rileva di nuovo)
export function forceExamMode(next) { mode = next; pending = null; }
export function currentExamMode() { return mode; }

// Funzioni che la modalità di prova non ha: comportamento equivalente
const MOCK_FALLBACKS = {
  getSession: () => async () => { throw Object.assign(new Error("Ripresa non disponibile in modalità prova."), { status: 404 }); },
  // revisione 0: si consegnano le risposte già salvate, senza applicarne di nuove
  submitExpired: (mock) => (submissionId) => mock.submitExam(submissionId, { clientRevision: 0, answers: [] }),
  reportViolation: () => async () => null,
};

export function createExamApi(token, user) {
  const role = user?.role?.toLowerCase();
  const real = realApi(token);
  const mock = createMockExamApi(user);

  const api = { ready: () => resolveMode(token, role), isMock: () => mode === "mock" };
  for (const name of Object.keys(real)) {
    if (name === "mock") continue;
    api[name] = async (...args) => {
      await resolveMode(token, role);
      if (mode !== "mock") return real[name](...args);
      const fn = mock[name] || (MOCK_FALLBACKS[name] ? MOCK_FALLBACKS[name](mock) : null);
      if (!fn) throw new Error("Funzione non disponibile in modalità prova.");
      return fn(...args);
    };
  }
  return api;
}
