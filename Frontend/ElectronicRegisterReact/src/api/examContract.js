// src/api/examContract.js
// Contratto con il backend per gli esami (ExamController): enum, date e conversioni tra i DTO del
// server e i dati usati dall'interfaccia (editor, svolgimento, correzione).

// ── Enum: il backend li invia come numeri (indice), ma si accettano anche i nomi ─────────────
export const EXAM_STATUS = ["draft", "published", "closed", "cancelled"];
export const SUBMISSION_STATUS = ["in_progress", "graded", "pending_review", "failed_auto", "finalized"];
export const QUESTION_TYPE = ["single", "multiple", "open"];
export const SUBMIT_REASON = ["manual", "time_expired", "violations"];

const norm = (s) => String(s).replace(/[_\s-]/g, "").toLowerCase();

// numero o nome → chiave in minuscolo (null se assente)
export function fromEnum(value, names) {
  if (value === null || value === undefined) return null;
  if (typeof value === "number") return names[value] ?? null;
  const i = names.findIndex(n => norm(n) === norm(value));
  return i >= 0 ? names[i] : null;
}
export const toEnum = (key, names) => Math.max(0, names.indexOf(key));

export const EXAM_STATUS_LABEL = { draft: "Bozza", published: "Pubblicato", closed: "Chiuso", cancelled: "Annullato" };
export const SUBMISSION_STATUS_LABEL = {
  in_progress: "In corso", graded: "Valutato", pending_review: "In correzione", failed_auto: "Non superato", finalized: "Valutato",
};

// ── Date ────────────────────────────────────────────────────────────────────────────────────
// Il server salva UTC ma può inviare le date senza il suffisso "Z": senza, JS le leggerebbe come locali.
export function parseUtc(value) {
  if (!value) return null;
  const s = String(value);
  return new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(s) ? s : `${s}Z`);
}
const pad = (n) => String(n).padStart(2, "0");
// Date → "AAAA-MM-GGTHH:mm" nel fuso locale (valore dei campi data/ora)
export function toLocalInput(value) {
  const d = value instanceof Date ? value : parseUtc(value);
  if (!d || isNaN(d)) return "";
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
// "AAAA-MM-GGTHH:mm" locale → ISO UTC da inviare
export function fromLocalInput(s) {
  if (!s) return null;
  const d = new Date(s);
  return isNaN(d) ? null : d.toISOString();
}
export function formatDateTime(value) {
  const d = value instanceof Date ? value : parseUtc(value);
  if (!d || isNaN(d)) return "";
  return d.toLocaleString("it-IT", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
}
// "GG/MM/AAAA" ("" se la data manca)
export function formatDate(value) {
  const d = value instanceof Date ? value : parseUtc(value);
  if (!d || isNaN(d)) return "";
  return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()}`;
}

// ── Nome dell'esame ─────────────────────────────────────────────────────────────────────────
// Gli esami non hanno un titolo: si riconoscono da materia e classe. classSubjects è l'elenco delle
// classi-materia (docenti e amministratori); senza, resta la materia, che il server manda in exam.name.
// TODO(API): AssignedExamDto ed ExamForStudentDto non hanno className, quindi allo studente solo la materia.
export function examNameParts(exam, classSubjects) {
  const cs = classSubjects?.find(c => c.id === exam?.classSubjectId);
  return { subject: (cs?.subjectName || exam?.name || "").trim(), className: (cs?.className || "").trim() };
}
// "Programmazione · SW-25A"; senza classe "Programmazione"; senza materia "Esame del 09/11/2026". Mai vuoto.
export function examDisplayName(exam, classSubjects) {
  const { subject, className } = examNameParts(exam, classSubjects);
  if (subject) return className ? `${subject} · ${className}` : subject;
  const date = formatDate(exam?.availableFrom);
  return date ? `Esame del ${date}` : "Esame";
}
// "Programmazione · SW-25A — 09/11/2026": per distinguere più esami della stessa classe e materia
export function examNameWithDate(exam, classSubjects) {
  const name = examDisplayName(exam, classSubjects);
  const date = formatDate(exam?.availableFrom);
  return date && !name.endsWith(date) ? `${name} — ${date}` : name;
}
export function availableFromLabel(exam) {
  const date = formatDate(exam?.availableFrom);
  return date ? `Disponibile dal ${date}` : "";
}
// Una bozza ha le date in formato locale: la si porta alla forma di un esame per le funzioni qui sopra
export const draftAsExam = (d) => ({ classSubjectId: d.classSubjectId, availableFrom: fromLocalInput(d.availableFrom) });

// ── Domande: forma dell'editor ⇄ forma del server ───────────────────────────────────────────
// Editor: { id, text, type: "single"|"multiple"|"open", options: [{id, text}], correct: [optionId], points, required }
export function questionFromDto(q) {
  const options = [...(q.options || [])].sort((a, b) => a.position - b.position);
  return {
    id: q.id,
    text: q.text,
    type: fromEnum(q.type, QUESTION_TYPE) || "single",
    options: options.map(o => ({ id: o.id, text: o.text })),
    correct: options.filter(o => o.isCorrect).map(o => o.id),
    points: String(q.points ?? 0),
    required: !!q.required,
  };
}

export function questionToDto(q, position) {
  const closed = q.type !== "open";
  return {
    position,
    text: q.text.trim(),
    type: toEnum(q.type, QUESTION_TYPE),
    points: Number(q.points) || 0,
    required: !!q.required,
    options: closed
      ? q.options.filter(o => o.text.trim()).map((o, i) => ({ position: i, text: o.text.trim(), isCorrect: q.correct.includes(o.id) }))
      : [],
  };
}

// Domanda vista dallo studente (senza risposte giuste) → forma dello svolgimento
export function studentQuestionFromDto(q) {
  const options = [...(q.options || [])].sort((a, b) => a.position - b.position);
  return {
    id: q.id,
    text: q.text,
    type: fromEnum(q.type, QUESTION_TYPE) || "single",
    points: Number(q.points) || 0,
    required: !!q.required,
    options: options.map(o => ({ id: o.id, text: o.text })),
  };
}

// ── Esame: bozza dell'editor ⇄ DTO ──────────────────────────────────────────────────────────
// Bozza: { examId?, step, description, classSubjectId, availableFrom, availableUntil (locali),
//          durationMinutes, passingPercent, retakeOfExamId, students: [{studentId, extraTimeMinutes}], questions }
export function draftToDto(d) {
  return {
    classSubjectId: d.classSubjectId,
    retakeOfExamId: d.retakeOfExamId || null,
    // Niente "name": il server lo ignora ([JsonIgnore]) e usa il nome della materia
    description: d.description?.trim() || null,
    availableFrom: fromLocalInput(d.availableFrom),
    availableUntil: fromLocalInput(d.availableUntil),
    durationMinutes: Number(d.durationMinutes) || 0,
    passingPercent: Number(d.passingPercent) || 0,
    assignedStudents: d.students.map(s => ({ studentId: s.studentId, extraTimeMinutes: Number(s.extraTimeMinutes) || 0 })),
    questions: d.questions.map((q, i) => questionToDto(q, i)),
  };
}

export function detailToDraft(detail) {
  return {
    examId: detail.id,
    step: 1,
    description: detail.description || "",
    classSubjectId: detail.classSubjectId,
    availableFrom: toLocalInput(detail.availableFrom),
    availableUntil: toLocalInput(detail.availableUntil),
    durationMinutes: String(detail.durationMinutes ?? ""),
    passingPercent: String(detail.passingPercent ?? 60),
    retakeOfExamId: detail.retakeOfExamId || "",
    students: (detail.assignedStudents || []).map(s => ({
      studentId: s.studentId, firstName: s.firstName, lastName: s.lastName, extraTimeMinutes: s.extraTimeMinutes || 0,
    })),
    questions: [...(detail.questions || [])].sort((a, b) => a.position - b.position).map(questionFromDto),
  };
}

// ── Risposte dello studente ─────────────────────────────────────────────────────────────────
// Svolgimento: { [questionId]: optionId | [optionId] | testo }  →  AnswerInputDto[]
export function answersToDto(questions, answers) {
  return questions
    .filter(q => answers[q.id] !== undefined && answers[q.id] !== "" && !(Array.isArray(answers[q.id]) && answers[q.id].length === 0))
    .map(q => {
      const a = answers[q.id];
      return q.type === "open"
        ? { questionId: q.id, openText: String(a), selectedOptionIds: [] }
        : { questionId: q.id, openText: null, selectedOptionIds: Array.isArray(a) ? a : [a] };
    });
}

export const isAnswered = (q, a) => q.type === "open" ? !!a?.trim() : Array.isArray(a) ? a.length > 0 : !!a;

// Esito di una consegna per lo studente: il voto si mostra solo se il server lo ha rilasciato
export function resultView(result) {
  const status = fromEnum(result?.status, SUBMISSION_STATUS);
  const base = { status, totalScore: result?.totalScore ?? null, maxScore: result?.maxScore ?? null, grade: result?.grade ?? null, passed: result?.passed ?? null };
  if (status === "pending_review") return { ...base, title: "In correzione", detail: "L'insegnante sta correggendo le risposte aperte.", showGrade: false };
  if (status === "failed_auto") return { ...base, title: "Esame non superato", detail: "Il punteggio massimo raggiungibile è inferiore alla soglia di sufficienza. L'insegnante definirà la valutazione.", showGrade: false };
  if (status === "graded" || status === "finalized") {
    // Senza l'esito del server non si afferma nulla: mai "superato" per mancanza di dati
    if (base.passed === null) return { ...base, title: "Esito non disponibile", detail: null, showGrade: false };
    return { ...base, title: base.passed ? "Esame superato" : "Esame non superato", detail: null, showGrade: base.grade !== null };
  }
  return { ...base, title: "Consegnato", detail: null, showGrade: false };
}
