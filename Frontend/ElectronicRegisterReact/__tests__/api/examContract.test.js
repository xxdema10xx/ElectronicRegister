// Contratto esami: enum, date UTC ⇄ locali, nomi, domande, bozza ⇄ DTO, risposte ed esito per lo studente.
import {
  EXAM_STATUS, QUESTION_TYPE, SUBMISSION_STATUS, SUBMISSION_STATUS_LABEL, answersToDto, availableFromLabel, detailToDraft, draftAsExam, draftToDto,
  examDisplayName, examNameWithDate, formatDate, formatDateTime, fromEnum, fromLocalInput, isAnswered, parseUtc,
  questionFromDto, questionToDto, resultView, studentQuestionFromDto, toEnum, toLocalInput,
} from "../../src/api/examContract";

describe("enum", () => {
  test("da numero o da nome (maiuscole, trattini e spazi ignorati)", () => {
    expect(fromEnum(1, EXAM_STATUS)).toBe("published");
    expect(fromEnum("Published", EXAM_STATUS)).toBe("published");
    expect(fromEnum("PendingReview", SUBMISSION_STATUS)).toBe("pending_review");
    expect(fromEnum("failed-auto", SUBMISSION_STATUS)).toBe("failed_auto");
  });

  test("valori assenti o sconosciuti → null", () => {
    expect(fromEnum(null, EXAM_STATUS)).toBeNull();
    expect(fromEnum(undefined, EXAM_STATUS)).toBeNull();
    expect(fromEnum(9, EXAM_STATUS)).toBeNull();
    expect(fromEnum("boh", EXAM_STATUS)).toBeNull();
  });

  test("toEnum: indice, 0 se sconosciuto", () => {
    expect(toEnum("open", QUESTION_TYPE)).toBe(2);
    expect(toEnum("x", QUESTION_TYPE)).toBe(0);
  });
});

describe("date", () => {
  test("parseUtc aggiunge la Z se il server la omette", () => {
    expect(parseUtc("2026-11-16T08:00:00").toISOString()).toBe("2026-11-16T08:00:00.000Z");
    expect(parseUtc("2026-11-16T08:00:00Z").toISOString()).toBe("2026-11-16T08:00:00.000Z");
    expect(parseUtc("2026-11-16T09:00:00+01:00").toISOString()).toBe("2026-11-16T08:00:00.000Z");
    expect(parseUtc(null)).toBeNull();
  });

  test("UTC del server → campo locale (Europe/Rome) e ritorno", () => {
    expect(toLocalInput("2026-11-16T08:00:00Z")).toBe("2026-11-16T09:00");
    expect(toLocalInput("2026-07-01T08:00:00Z")).toBe("2026-07-01T10:00"); // ora legale
    expect(fromLocalInput("2026-11-16T09:00")).toBe("2026-11-16T08:00:00.000Z");
    expect(toLocalInput("non una data")).toBe("");
    expect(fromLocalInput("")).toBeNull();
    expect(fromLocalInput("xx")).toBeNull();
  });

  test("formati di visualizzazione", () => {
    expect(formatDate("2026-11-16T23:30:00Z")).toBe("17/11/2026");
    expect(formatDateTime("2026-11-16T08:05:00Z")).toMatch(/16\/11\/2026.*09:05/);
    expect(formatDate(null)).toBe("");
    expect(formatDateTime("x")).toBe("");
  });
});

describe("nome dell'esame", () => {
  const cs = [{ id: "cs1", subjectName: "Programmazione", className: "SW-25A" }];

  test("materia · classe dalle classi-materia", () => {
    expect(examDisplayName({ classSubjectId: "cs1", name: "ignorato" }, cs)).toBe("Programmazione · SW-25A");
  });

  test("senza classi-materia: la materia mandata dal server", () => {
    expect(examDisplayName({ classSubjectId: "cs1", name: "Reti" })).toBe("Reti");
  });

  test("senza materia: la data, oppure 'Esame'", () => {
    expect(examDisplayName({ availableFrom: "2026-11-09T08:00:00Z" })).toBe("Esame del 09/11/2026");
    expect(examDisplayName({})).toBe("Esame");
  });

  test("con data per distinguere esami omonimi, senza ripeterla", () => {
    expect(examNameWithDate({ classSubjectId: "cs1", availableFrom: "2026-11-09T08:00:00Z" }, cs)).toBe("Programmazione · SW-25A — 09/11/2026");
    expect(examNameWithDate({ availableFrom: "2026-11-09T08:00:00Z" })).toBe("Esame del 09/11/2026");
  });

  test("etichetta di disponibilità e bozza come esame", () => {
    expect(availableFromLabel({ availableFrom: "2026-11-09T08:00:00Z" })).toBe("Disponibile dal 09/11/2026");
    expect(availableFromLabel({})).toBe("");
    expect(draftAsExam({ classSubjectId: "cs1", availableFrom: "2026-11-09T09:00" })).toEqual({ classSubjectId: "cs1", availableFrom: "2026-11-09T08:00:00.000Z" });
  });
});

describe("domande", () => {
  const dto = {
    id: "q1", text: "Metodo per creare?", type: 0, points: 2, required: true,
    options: [{ id: "o2", text: "GET", position: 1, isCorrect: false }, { id: "o1", text: "POST", position: 0, isCorrect: true }],
  };

  test("dal server all'editor: opzioni in ordine e risposte giuste", () => {
    expect(questionFromDto(dto)).toEqual({
      id: "q1", text: "Metodo per creare?", type: "single",
      options: [{ id: "o1", text: "POST" }, { id: "o2", text: "GET" }], correct: ["o1"], points: "2", required: true,
    });
  });

  test("tipo sconosciuto → singola; punti mancanti → '0'", () => {
    expect(questionFromDto({ id: "q", text: "t", type: 99, options: [] })).toMatchObject({ type: "single", points: "0", required: false });
  });

  test("dall'editor al server: testo pulito, opzioni vuote scartate", () => {
    const q = { text: "  Quale?  ", type: "multiple", points: "3", required: 0,
      options: [{ id: "a", text: " A " }, { id: "b", text: "  " }, { id: "c", text: "C" }], correct: ["a", "c"] };
    expect(questionToDto(q, 4)).toEqual({
      position: 4, text: "Quale?", type: 1, points: 3, required: false,
      options: [{ position: 0, text: "A", isCorrect: true }, { position: 1, text: "C", isCorrect: true }],
    });
  });

  test("domanda aperta: nessuna opzione", () => {
    expect(questionToDto({ text: "Spiega", type: "open", points: "", options: [{ id: "x", text: "y" }], correct: [] }, 0))
      .toMatchObject({ type: 2, points: 0, options: [] });
  });

  test("vista dello studente: senza risposte giuste", () => {
    const q = studentQuestionFromDto(dto);
    expect(q).toEqual({ id: "q1", text: "Metodo per creare?", type: "single", points: 2, required: true, options: [{ id: "o1", text: "POST" }, { id: "o2", text: "GET" }] });
    expect(q.options[0]).not.toHaveProperty("isCorrect");
  });
});

describe("bozza ⇄ DTO", () => {
  const draft = {
    description: "  Prova  ", classSubjectId: "cs1", retakeOfExamId: "",
    availableFrom: "2026-11-16T09:00", availableUntil: "2026-11-16T11:00",
    durationMinutes: "60", passingPercent: "60",
    students: [{ studentId: "s1", extraTimeMinutes: "10" }, { studentId: "s2", extraTimeMinutes: "" }],
    questions: [{ text: "Q", type: "open", points: "5", required: true, options: [], correct: [] }],
  };

  test("draftToDto", () => {
    expect(draftToDto(draft)).toEqual({
      classSubjectId: "cs1", retakeOfExamId: null, description: "Prova",
      availableFrom: "2026-11-16T08:00:00.000Z", availableUntil: "2026-11-16T10:00:00.000Z",
      durationMinutes: 60, passingPercent: 60,
      assignedStudents: [{ studentId: "s1", extraTimeMinutes: 10 }, { studentId: "s2", extraTimeMinutes: 0 }],
      questions: [{ position: 0, text: "Q", type: 2, points: 5, required: true, options: [] }],
    });
  });

  test("descrizione vuota → null", () => {
    expect(draftToDto({ ...draft, description: "   " }).description).toBeNull();
  });

  test("detailToDraft: dettaglio del server → bozza dell'editor", () => {
    const detail = {
      id: "e1", description: null, classSubjectId: "cs1", availableFrom: "2026-11-16T08:00:00Z", availableUntil: "2026-11-16T10:00:00Z",
      durationMinutes: 45, passingPercent: null, retakeOfExamId: null,
      assignedStudents: [{ studentId: "s1", firstName: "Mario", lastName: "Rossi", extraTimeMinutes: null }],
      questions: [{ id: "q2", position: 1, text: "B", type: 2, options: [] }, { id: "q1", position: 0, text: "A", type: 2, options: [] }],
    };
    const d = detailToDraft(detail);
    expect(d).toMatchObject({
      examId: "e1", step: 1, description: "", availableFrom: "2026-11-16T09:00", availableUntil: "2026-11-16T11:00",
      durationMinutes: "45", passingPercent: "60", retakeOfExamId: "",
      students: [{ studentId: "s1", firstName: "Mario", lastName: "Rossi", extraTimeMinutes: 0 }],
    });
    expect(d.questions.map(q => q.id)).toEqual(["q1", "q2"]);
  });
});

describe("risposte dello studente", () => {
  const questions = [
    { id: "a", type: "single" }, { id: "b", type: "multiple" }, { id: "c", type: "open" }, { id: "d", type: "single" }, { id: "e", type: "multiple" },
  ];

  test("solo le domande con risposta, nella forma del server", () => {
    expect(answersToDto(questions, { a: "o1", b: ["o2", "o3"], c: "testo", d: "", e: [] })).toEqual([
      { questionId: "a", openText: null, selectedOptionIds: ["o1"] },
      { questionId: "b", openText: null, selectedOptionIds: ["o2", "o3"] },
      { questionId: "c", openText: "testo", selectedOptionIds: [] },
    ]);
  });

  test("isAnswered", () => {
    expect(isAnswered({ type: "open" }, "  ")).toBe(false);
    expect(isAnswered({ type: "open" }, "ok")).toBe(true);
    expect(isAnswered({ type: "multiple" }, [])).toBe(false);
    expect(isAnswered({ type: "multiple" }, ["x"])).toBe(true);
    expect(isAnswered({ type: "single" }, undefined)).toBe(false);
    expect(isAnswered({ type: "single" }, "x")).toBe(true);
  });
});

describe("esito per lo studente", () => {
  test("in correzione: niente voto", () => {
    expect(resultView({ status: 2, grade: 8 })).toMatchObject({ status: "pending_review", title: "In correzione", showGrade: false });
  });

  test("FailedAuto non è un esito didattico: 'In attesa di valutazione', mai 'non superato'", () => {
    expect(resultView({ status: "FailedAuto" })).toMatchObject({ title: "In attesa di valutazione", showGrade: false });
    expect(resultView({ status: 3, passed: false, grade: 4 }).title).not.toMatch(/non superato/i);
    expect(SUBMISSION_STATUS_LABEL.failed_auto).toBe("Da valutare");
  });

  test("valutato: superato o no, voto se rilasciato", () => {
    expect(resultView({ status: 1, passed: true, grade: 8, totalScore: 16, maxScore: 20 }))
      .toMatchObject({ title: "Esame superato", showGrade: true, grade: 8, totalScore: 16, maxScore: 20 });
    expect(resultView({ status: 4, passed: false, grade: 4 })).toMatchObject({ title: "Esame non superato", showGrade: true });
    expect(resultView({ status: 1, passed: true, grade: null })).toMatchObject({ showGrade: false });
  });

  test("senza esito del server non si afferma nulla", () => {
    expect(resultView({ status: "graded" })).toMatchObject({ title: "Esito non disponibile", showGrade: false });
  });

  test("in corso o sconosciuto: 'Consegnato'", () => {
    expect(resultView({ status: 0 }).title).toBe("Consegnato");
    expect(resultView(null).title).toBe("Consegnato");
  });
});
