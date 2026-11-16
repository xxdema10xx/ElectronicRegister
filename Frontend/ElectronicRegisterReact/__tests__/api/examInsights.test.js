// "Chi deve recuperare", consegne da correggere, candidati al recupero e libretto dello studente.
import {
  formatRetakeCount, getExamInsights, getPendingReviewCount, getRetakeCandidates, getRetakeGroups, getStudentBooklet,
  invalidateExamInsights, relevantForSubmissions, retakeSummary,
} from "../../src/api/examInsights";

let n = 0;
// API finta: consegne e dettaglio per esame; cacheKey unica per test (la cache è a livello di modulo)
function fakeApi({ subs = {}, details = {}, results = {} } = {}) {
  const key = `test-${++n}`;
  return {
    cacheKey: () => key,
    listSubmissions: jest.fn(async (id) => { if (subs[id] instanceof Error) throw subs[id]; return subs[id] || []; }),
    getExam: jest.fn(async (id) => { if (details[id] instanceof Error) throw details[id]; return details[id] || { assignedStudents: [] }; }),
    getResult: jest.fn(async (id) => results[id]),
  };
}

const sub = (studentId, status, extra = {}) => ({ studentId, status, studentFirstName: `N${studentId}`, studentLastName: `C${studentId}`, ...extra });

describe("riepilogo", () => {
  test("formatRetakeCount", () => {
    expect(formatRetakeCount(0, 0)).toBe("Nessuno da recuperare");
    expect(formatRetakeCount(7, 0)).toBe("7 da recuperare");
    expect(formatRetakeCount(3, 1)).toBe("3 da recuperare (1 assente)");
    expect(formatRetakeCount(3, 2)).toBe("3 da recuperare (2 assenti)");
  });

  test("solo esami pubblicati o chiusi", () => {
    const exams = [{ id: 1, status: 0 }, { id: 2, status: 1 }, { id: 3, status: "closed" }, { id: 4, status: 3 }];
    expect(relevantForSubmissions(exams).map(e => e.id)).toEqual([2, 3]);
  });
});

describe("chi deve recuperare", () => {
  test("insufficienti e non superabili; assenti solo negli esami chiusi", async () => {
    const api = fakeApi({
      subs: {
        pub: [sub("a", "graded", { passed: false }), sub("b", "graded", { passed: true }), sub("c", "in_progress"), sub("d", "pending_review")],
        closed: [sub("e", "failed_auto"), sub("f", "in_progress")],
      },
      details: { closed: { status: 2, assignedStudents: [{ studentId: "e" }, { studentId: "f" }, { studentId: "g", firstName: "Ng", lastName: "Cg" }] } },
    });
    const { groups, failed } = await getRetakeGroups(api, [{ id: "pub", status: 1 }, { id: "closed", status: 2 }, { id: "draft", status: 0 }]);
    expect(failed).toBe(0);
    expect(groups.map(g => [g.studentId, g.exams.map(e => e.reason), g.absentOnly])).toEqual([
      ["a", ["failed"], false],
      ["e", ["failed"], false],
      ["f", ["missing"], true],
      ["g", ["missing"], true],
    ]);
    expect(retakeSummary(groups)).toBe("4 da recuperare (2 assenti)");
    expect(api.getExam).toHaveBeenCalledTimes(1); // dettaglio solo per l'esame chiuso
  });

  test("consegne non leggibili: l'esame si conta come fallito, non come 'nessuno'", async () => {
    const api = fakeApi({ subs: { x: new Error("rete") } });
    await expect(getRetakeGroups(api, [{ id: "x", status: 1 }])).resolves.toEqual({ groups: [], failed: 1 });
  });

  test("consegne da correggere", async () => {
    const api = fakeApi({ subs: { a: [sub("1", "pending_review"), sub("2", 2), sub("3", "graded")], b: [sub("4", "PendingReview")] } });
    await expect(getPendingReviewCount(api, [{ id: "a", status: 1 }, { id: "b", status: 2 }])).resolves.toBe(3);
  });

  test("insights per la dashboard in una sola serie di chiamate", async () => {
    const api = fakeApi({ subs: { a: [sub("1", "pending_review"), sub("2", "failed_auto")] } });
    const res = await getExamInsights(api, [{ id: "a", status: 1 }]);
    expect(res).toMatchObject({ pendingReviewCount: 1, failed: 0, retakeSummary: "1 da recuperare" });
    expect(api.listSubmissions).toHaveBeenCalledTimes(1);
  });

  test("cache per sessione e invalidazione per esame", async () => {
    const api = fakeApi({ subs: { a: [] } });
    const exams = [{ id: "a", status: 1 }];
    await getPendingReviewCount(api, exams);
    await getPendingReviewCount(api, exams);
    expect(api.listSubmissions).toHaveBeenCalledTimes(1);
    invalidateExamInsights("a");
    await getPendingReviewCount(api, exams);
    expect(api.listSubmissions).toHaveBeenCalledTimes(2);
    invalidateExamInsights();
    await getPendingReviewCount(api, exams);
    expect(api.listSubmissions).toHaveBeenCalledTimes(3);
  });

  test("un errore non resta in cache: si riprova", async () => {
    const api = fakeApi();
    api.listSubmissions.mockRejectedValueOnce(new Error("x")).mockResolvedValueOnce([sub("1", "pending_review")]);
    const exams = [{ id: "z", status: 1 }];
    await expect(getPendingReviewCount(api, exams)).resolves.toBe(0);
    await expect(getPendingReviewCount(api, exams)).resolves.toBe(1);
  });
});

describe("candidati al recupero (step Studenti)", () => {
  test("esame chiuso con consegne: proposti insufficienti e assenti", async () => {
    const api = fakeApi({
      subs: { o: [sub("a", "graded", { passed: false }), sub("b", "graded", { passed: true }), sub("c", "pending_review")] },
      details: { o: { status: 2, assignedStudents: [{ studentId: "a" }, { studentId: "b" }, { studentId: "c" }, { studentId: "d" }] } },
    });
    const { closed, hasSubmissions, byStudent } = await getRetakeCandidates(api, "o");
    expect([closed, hasSubmissions]).toEqual([true, true]);
    expect(Object.fromEntries(byStudent)).toEqual({
      a: { reason: "failed", proposed: true }, b: { reason: "passed", proposed: false },
      c: { reason: "pending", proposed: false }, d: { reason: "missing", proposed: true },
    });
  });

  test("esame non ancora chiuso: nessuno proposto", async () => {
    const api = fakeApi({
      subs: { o: [sub("a", "failed_auto")] },
      details: { o: { status: 1, assignedStudents: [{ studentId: "a" }, { studentId: "b" }] } },
    });
    const { closed, byStudent } = await getRetakeCandidates(api, "o");
    expect(closed).toBe(false);
    expect(byStudent.get("a")).toEqual({ reason: "failed", proposed: false });
    expect(byStudent.has("b")).toBe(false);
  });
});

describe("libretto dello studente", () => {
  test("una riga per esame consegnato, dal più recente; esito letto solo se corretto", async () => {
    const api = fakeApi({ results: { s1: { status: 1, passed: true, grade: 9 } } });
    const assigned = [
      { id: "e1", submissionStatus: "graded", submissionId: "s1", submittedAt: "2026-11-10T10:00:00Z" },
      { id: "e2", submissionStatus: "pending_review", submissionId: "s2", submittedAt: "2026-11-12T10:00:00Z" },
      { id: "e3", submissionStatus: null, availableFrom: "2026-11-20T10:00:00Z" },
    ];
    const rows = await getStudentBooklet(api, assigned);
    expect(rows.map(r => r.item.id)).toEqual(["e2", "e1"]);
    expect(rows[1].r).toMatchObject({ title: "Esame superato", grade: 9, showGrade: true });
    expect(rows[0].r.title).toBe("In correzione");
    expect(api.getResult).toHaveBeenCalledTimes(1);
  });

  test("esito non leggibile: si mostra lo stato senza voto", async () => {
    const api = fakeApi();
    api.getResult.mockRejectedValue(new Error("x"));
    const [row] = await getStudentBooklet(api, [{ id: "e1", submissionStatus: "graded", submissionId: "s9" }]);
    expect(row.r).toMatchObject({ title: "Esito non disponibile", showGrade: false });
  });
});
