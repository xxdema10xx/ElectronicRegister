import axiosClient from "../../src/api/axiosClient";
import { session } from "../../src/api/session";
import { createExamApi } from "../../src/api/examApi";

const ok = (data) => Promise.resolve({ data });
const H = { Authorization: "Bearer tok" };

beforeEach(() => {
  session.clear();
  for (const m of ["get", "post", "put", "delete"]) jest.spyOn(axiosClient, m).mockReturnValue(ok({}));
});
afterEach(() => jest.restoreAllMocks());

describe("API esami", () => {
  test.each([
    ["listExams", [], "get", "/institution/exams"],
    ["getExam", ["e1"], "get", "/institution/exams/e1"],
    ["publishExam", ["e1"], "post", "/institution/exams/e1/publish"],
    ["closeExam", ["e1"], "post", "/institution/exams/e1/close"],
    ["cancelExam", ["e1"], "post", "/institution/exams/e1/cancel"],
    ["deleteExam", ["e1"], "delete", "/institution/exams/e1"],
    ["getAssigned", [], "get", "/institution/exams/assigned"],
    ["startExam", ["e1"], "post", "/institution/exams/e1/start"],
    ["getSession", ["sub1"], "get", "/institution/submissions/sub1/session"],
    ["submitExpired", ["sub1"], "post", "/institution/submissions/sub1/submit-expired"],
    ["getResult", ["sub1"], "get", "/institution/submissions/sub1/result"],
    ["getReview", ["sub1"], "get", "/institution/submissions/sub1/review"],
  ])("%s → %s %s", async (fn, args, method, path) => {
    await createExamApi("tok")[fn](...args);
    expect(axiosClient[method].mock.calls[0][0]).toBe(path);
    const cfg = axiosClient[method].mock.calls[0].at(-1);
    expect(cfg.headers).toEqual(H);
  });

  test.each([
    ["createExam", ["DTO"], "/institution/exams"],
    ["saveAnswers", ["sub1", "DTO"], "/institution/submissions/sub1/answers"],
    ["submitExam", ["sub1", "DTO"], "/institution/submissions/sub1/submit"],
    ["reportViolation", ["sub1", "DTO"], "/institution/submissions/sub1/violations"],
    ["reviewSubmission", ["sub1", "DTO"], "/institution/submissions/sub1/review"],
    ["finalizeSubmission", ["sub1", "DTO"], "/institution/submissions/sub1/finalize"],
    ["updateExamStudents", ["e1", "DTO"], "/institution/exams/e1/students"],
  ])("%s invia il corpo", async (fn, args, path) => {
    await createExamApi("tok")[fn](...args);
    const call = (axiosClient.post.mock.calls[0] || axiosClient.put.mock.calls[0]);
    expect(call[0]).toBe(path);
    expect(call[1]).toBe("DTO");
  });

  test("updateExam non invia gli studenti assegnati", async () => {
    await createExamApi("tok").updateExam("e1", { assignedStudents: [1], durationMinutes: 30 });
    expect(axiosClient.put).toHaveBeenCalledWith("/institution/exams/e1", { durationMinutes: 30 }, expect.anything());
  });

  test("consegne: filtro di stato solo se indicato", async () => {
    const api = createExamApi("tok");
    await api.listSubmissions("e1");
    expect(axiosClient.get.mock.calls[0][1].params).toBeUndefined();
    await api.listSubmissions("e1", 0);
    expect(axiosClient.get.mock.calls[1][1].params).toEqual({ status: 0 });
  });

  test("classi-materia: tutte le pagine, etichetta 'classe · materia', in cache", async () => {
    const page = (n) => Array.from({ length: n }, (_, i) => ({ id: `cs${i}`, classId: "c1", className: "SW-25A", subjectName: "Reti" }));
    axiosClient.get.mockReturnValueOnce(ok(page(100))).mockReturnValueOnce(ok([{ id: "x", className: "SW-25B" }]));
    const api = createExamApi("tok");
    const list = await api.getClassSubjects();
    expect(list).toHaveLength(101);
    expect(list[0].label).toBe("SW-25A · Reti");
    expect(list[100].label).toBe("SW-25B");
    await api.getClassSubjects();
    expect(axiosClient.get).toHaveBeenCalledTimes(2);
  });

  test("classi-materia vietate al ruolo (403) → null, senza errori", async () => {
    axiosClient.get.mockReturnValue(Promise.reject(Object.assign(new Error("403"), { status: 403 })));
    await expect(createExamApi("tok").getClassSubjects()).resolves.toBeNull();
  });

  test("altri errori delle classi-materia si propagano", async () => {
    axiosClient.get.mockReturnValue(Promise.reject(Object.assign(new Error("500"), { status: 500 })));
    await expect(createExamApi("tok").getClassSubjects()).rejects.toThrow("500");
  });

  test("studenti della classe della classe-materia", async () => {
    axiosClient.get.mockImplementation((path) => (path === "/institution/class-subjects"
      ? ok([{ id: "cs1", classId: "c7" }]) : ok([{ id: "s1" }])));
    const api = createExamApi("tok");
    await expect(api.getStudentsOfClassSubject("cs1")).resolves.toEqual([{ id: "s1" }]);
    expect(axiosClient.get).toHaveBeenLastCalledWith("/institution/students", { headers: H, params: { classId: "c7" } });
  });

  test("cacheKey: il token, oppure 'anon'", () => {
    expect(createExamApi("tok").cacheKey()).toBe("tok");
    expect(createExamApi(null).cacheKey()).toBe("anon");
  });
});
