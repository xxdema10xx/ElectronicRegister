// Regole dell'editor esami: contenuto della bozza, problemi delle domande, livelli di validazione.
import {
  MULTIPLE_MIN_CORRECT, draftHasContent, newQuestion, newTestDraft, passingPointsLabel, questionIssues, serverIssue, summarize,
  validateAll, validateDetails, validateForPublish, validateTestDraft,
} from "../../src/components/TestBuilder";

const singleOk = () => ({ id: "q1", text: "Quale?", type: "single", options: [{ id: "a", text: "A" }, { id: "b", text: "B" }], correct: ["a"], points: "2", required: false });

function validDraft() {
  return {
    ...newTestDraft(), classSubjectId: "cs1",
    availableFrom: "2026-11-16T09:00", availableUntil: "2026-11-16T11:00", durationMinutes: "60", passingPercent: "60",
    questions: [singleOk()], students: [{ studentId: "s1", extraTimeMinutes: 0 }],
  };
}

describe("bozza nuova", () => {
  test("valori predefiniti e una domanda singola con due opzioni vuote", () => {
    const d = newTestDraft();
    expect(d).toMatchObject({ step: 1, durationMinutes: "30", passingPercent: "60", students: [] });
    expect(d.questions).toHaveLength(1);
    expect(d.questions[0]).toMatchObject({ type: "single", points: "1", correct: [] });
    expect(d.questions[0].options).toHaveLength(2);
    expect(newQuestion().id).not.toBe(newQuestion().id);
  });

  test("draftHasContent: classe, durata e opzioni vuote non contano", () => {
    expect(draftHasContent(null)).toBe(false);
    expect(draftHasContent({ ...newTestDraft(), classSubjectId: "cs1", durationMinutes: "90" })).toBe(false);
    expect(draftHasContent({ ...newTestDraft(), description: "x" })).toBe(true);
    expect(draftHasContent({ ...newTestDraft(), availableFrom: "2026-11-16T09:00" })).toBe(true);
    const d = newTestDraft();
    d.questions[0].options[1].text = "B";
    expect(draftHasContent(d)).toBe(true);
  });

  test("summarize: domande chiuse/aperte e punti totali", () => {
    const d = { questions: [singleOk(), { ...singleOk(), type: "open", points: "3" }, { ...singleOk(), points: "x" }] };
    expect(summarize(d)).toEqual({ totalQuestions: 3, closedQuestions: 2, openQuestions: 1, totalPoints: 5 });
  });
});

describe("problemi di una domanda", () => {
  test("domanda corretta: nessun problema", () => {
    expect(questionIssues(singleOk())).toEqual({});
  });

  test("testo, opzioni, risposta giusta e punti mancanti", () => {
    const q = { ...singleOk(), text: " ", options: [{ id: "a", text: "A" }, { id: "b", text: "" }], correct: [], points: "0" };
    expect(Object.keys(questionIssues(q)).sort()).toEqual(["correct", "options", "points", "text"]);
    expect(questionIssues(q).correct).toMatch(/tocca il cerchio/);
  });

  test("risposta giusta su un'opzione vuota", () => {
    const q = { ...singleOk(), options: [{ id: "a", text: "A" }, { id: "b", text: "B" }, { id: "c", text: "" }], correct: ["c"] };
    expect(questionIssues(q).correct).toMatch(/opzione segnata come giusta è vuota/);
  });

  test(`multipla: almeno ${MULTIPLE_MIN_CORRECT} risposte giuste`, () => {
    expect(questionIssues({ ...singleOk(), type: "multiple", correct: ["a"] }).correct).toMatch(/almeno 2 risposte corrette/);
    expect(questionIssues({ ...singleOk(), type: "multiple", correct: ["a", "b"] })).toEqual({});
  });

  test("aperta: niente opzioni né risposta giusta", () => {
    expect(questionIssues({ ...singleOk(), type: "open", options: [], correct: [] })).toEqual({});
  });
});

describe("validazione dell'esame", () => {
  test("bozza completa: valida per salvare; per pubblicare servono studenti", () => {
    const d = validDraft();
    expect(validateTestDraft(d)).toBeNull();
    expect(validateForPublish(d)).toBeNull();
    expect(validateForPublish({ ...d, students: [] })).toBe("Assegna l'esame ad almeno uno studente.");
    expect(validateTestDraft({ ...d, students: [] })).toBeNull();
  });

  test("dettagli: classe, finestra e durata", () => {
    expect(validateDetails({ ...validDraft(), classSubjectId: "" })).toBe("Mancano la classe e la materia.");
    expect(validateDetails({ ...validDraft(), availableFrom: "" })).toBe("Manca l'inizio della disponibilità.");
    expect(validateDetails({ ...validDraft(), availableUntil: "2026-11-16T08:00" })).toBe("La fine della disponibilità deve venire dopo l'inizio.");
    expect(validateDetails({ ...validDraft(), durationMinutes: "5" })).toBe("La durata deve essere maggiore di 5 minuti.");
    expect(validateDetails({ ...validDraft(), durationMinutes: "121" })).toBe("La durata non può superare la finestra di disponibilità.");
  });

  test("soglia e domande sono dello step 2, con indice e id della domanda", () => {
    const d = { ...validDraft(), passingPercent: "100", questions: [singleOk(), { ...singleOk(), id: "q2", text: "" }] };
    const errors = validateAll(d);
    expect(errors.map(e => [e.step, e.field])).toEqual([[2, "passingPercent"], [2, "text"]]);
    expect(errors[1]).toMatchObject({ questionIndex: 1, questionId: "q2", message: "Domanda 2: Manca il testo della domanda." });
    expect(validateDetails(d)).toBeNull();
    expect(validateAll({ ...validDraft(), questions: [] }).map(e => e.field)).toEqual(["questions"]);
  });

  test("livello server: solo i campi che il server rifiuta", () => {
    const d = { ...validDraft(), students: [] };
    expect(serverIssue(d)).toBeNull();
    expect(validateAll(d, { level: "publish" }).map(e => e.field)).toEqual(["students"]);
    expect(validateAll(d, { level: "server" })).toEqual([]);
    expect(serverIssue({ ...d, availableUntil: "" })).toBe("Manca la fine della disponibilità.");
  });
});

describe("soglia in punti", () => {
  test.each([
    [20, "60", "= 12 punti su 20"],
    [7, "60", "= 4,2 punti su 7"],
    [0, "60", null],
    [10, "0", null],
    [10, "100", null],
  ])("%s punti al %s%% → %s", (total, pct, label) => {
    expect(passingPointsLabel(total, pct)).toBe(label);
  });
});
