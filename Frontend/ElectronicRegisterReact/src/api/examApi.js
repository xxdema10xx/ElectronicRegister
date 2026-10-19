// src/api/examApi.js
// Esami: chiamate a ExamController del backend. Se il server non espone /Exam (risponde 404 al primo
// controllo) si passa automaticamente alla modalità di prova (src/api/mockExamBackend.js), che ha le
// stesse funzioni e gli stessi dati. Nelle schermate si usa sempre l'oggetto restituito da createExamApi().
import axiosClient, { authHeader } from "./axiosClient";
import { createMockExamApi } from "./mockExamBackend";

const request = (token) => ({ headers: authHeader(token) });

function realApi(token) {
  const get = async (path, params) => (await axiosClient.get(path, { ...request(token), params })).data;
  const post = async (path, body) => (await axiosClient.post(path, body, request(token))).data;
  const put = async (path, body) => (await axiosClient.put(path, body, request(token))).data;
  const del = async (path) => (await axiosClient.delete(path, request(token))).data;

  return {
    mock: false,

    // Il backend non ha ancora un elenco delle classi-materia (ClassSubject): null = non disponibile.
    // Quando esisterà, atteso come [{ id, label }] oppure [{ id, className, subjectName }].
    getClassSubjects: async () => {
      try {
        const list = await get("/ClassSubject");
        return (Array.isArray(list) ? list : []).map(c => ({ ...c, label: c.label ?? [c.className, c.subjectName].filter(Boolean).join(" · ") }));
      } catch (e) {
        if (e.status === 404 || String(e.message).includes("404")) return null;
        throw e;
      }
    },
    // Non esiste un filtro per classe: si elencano tutti gli studenti e il server controlla l'appartenenza alla classe.
    getStudentsOfClassSubject: async () => get("/Student"),

    listExams: () => get("/Exam"),
    getExam: (id) => get(`/Exam/${id}`),
    createExam: (dto) => post("/Exam", dto),
    updateExam: (id, dto) => put(`/Exam/${id}`, dto),
    updateExamStudents: (id, dto) => put(`/Exam/${id}/students`, dto),
    publishExam: (id) => post(`/Exam/${id}/publish`),
    closeExam: (id) => post(`/Exam/${id}/close`),
    cancelExam: (id) => post(`/Exam/${id}/cancel`),
    deleteExam: (id) => del(`/Exam/${id}`),

    getAssigned: () => get("/Exam/assigned"),
    startExam: (examId) => post(`/Exam/${examId}/start`),
    // Attenzione: nelle rotte di salvataggio, consegna ed esito l'id è quello della CONSEGNA (submissionId)
    saveAnswers: (submissionId, dto) => post(`/Exam/${submissionId}/answers/save`, dto),
    submitExam: (submissionId, dto) => post(`/Exam/${submissionId}/submit`, dto),
    getResult: (submissionId) => get(`/Exam/submission/${submissionId}/result`),

    listSubmissions: (examId, status) => get(`/Exam/${examId}/submissions`, status === undefined || status === null || status === "" ? undefined : { status }),
    getReview: (submissionId) => get(`/Exam/submission/${submissionId}/review`),
    reviewSubmission: (submissionId, dto) => put(`/Exam/submission/${submissionId}/review`, dto),
    finalizeSubmission: (submissionId, dto) => post(`/Exam/submission/${submissionId}/finalize`, dto),
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

export function createExamApi(token, user) {
  const role = user?.role?.toLowerCase();
  const real = realApi(token);
  const mock = createMockExamApi(user);

  const api = { ready: () => resolveMode(token, role), isMock: () => mode === "mock" };
  for (const name of Object.keys(real)) {
    if (name === "mock") continue;
    api[name] = async (...args) => {
      await resolveMode(token, role);
      return (mode === "mock" ? mock : real)[name](...args);
    };
  }
  return api;
}
