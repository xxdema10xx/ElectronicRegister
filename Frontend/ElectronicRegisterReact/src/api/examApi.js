// src/api/examApi.js
// Esami e consegne (docs/API.md §22–23). Nelle schermate si usa sempre l'oggetto restituito da createExamApi().
import axiosClient, { authHeader } from "./axiosClient";

export function createExamApi(token) {
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
    // Chiave della cache di examInsights: le schermate hanno oggetti api diversi ma la stessa sessione
    cacheKey: () => token || "anon",

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
