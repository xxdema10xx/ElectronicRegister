// Voti alla classe in blocco: classi-materia, studenti della classe, POST /grades/batch ed errori per riga.
import axiosClient from "../../src/api/axiosClient";
import { session } from "../../src/api/session";
import {
  BATCH_ROW_MESSAGES, batchRowErrors, createGradesBatch, getClassStudents, getGradeClassSubjects,
} from "../../src/api/gradeBatchService";

const ok = (data) => Promise.resolve({ data });
const H = { headers: { Authorization: "Bearer tok" } };

beforeEach(() => {
  session.clear();
  for (const m of ["get", "post"]) jest.spyOn(axiosClient, m).mockReturnValue(ok([]));
});
afterEach(() => jest.restoreAllMocks());

test("POST /institution/grades/batch con classSubjectId, descrizione ripulita e righe", async () => {
  axiosClient.post.mockReturnValue(ok({ eventId: "ev1", created: [{ studentId: "s1", gradeId: "g1" }] }));
  const res = await createGradesBatch({ eventId: "ev1", classSubjectId: "cs1", date: "2026-11-17", description: "  Verifica  ",
    grades: [{ studentId: "s1", value: 24 }] }, "tok");
  expect(axiosClient.post).toHaveBeenCalledWith("/institution/grades/batch",
    { eventId: "ev1", classSubjectId: "cs1", date: "2026-11-17", description: "Verifica", grades: [{ studentId: "s1", value: 24 }] }, H);
  expect(res.created).toHaveLength(1);
  await createGradesBatch({ eventId: "ev2", classSubjectId: "cs1", date: "d", description: " ", grades: [] }, "tok");
  expect(axiosClient.post.mock.calls[1][1]).not.toHaveProperty("description");
});

test("classi-materia: etichetta classe · materia, filtro per docente; 403 (segreteria) → null", async () => {
  axiosClient.get.mockReturnValueOnce(ok([
    { id: "cs2", classId: "c2", className: "SW-25B", subjectName: "Reti", teacherId: "t2" },
    { classSubjectId: "cs1", id: "cs1", classId: "c1", className: "SW-25A", subjectName: "Java", teacherId: "t1" },
  ]));
  await expect(getGradeClassSubjects("tok", { teacherId: "t1" })).resolves.toEqual([{ classSubjectId: "cs1", classId: "c1", label: "SW-25A · Java" }]);
  expect(axiosClient.get).toHaveBeenCalledWith("/institution/class-subjects", { ...H, params: { pageNumber: 1, pageSize: 100 } });
  // segreteria/admin: nessun filtro, tutto l'istituto
  axiosClient.get.mockReturnValueOnce(ok([
    { id: "cs2", classId: "c2", className: "SW-25B", subjectName: "Reti", teacherId: "t2" },
    { id: "cs1", classId: "c1", className: "SW-25A", subjectName: "Java", teacherId: "t1" },
  ]));
  await expect(getGradeClassSubjects("tok")).resolves.toEqual([
    { classSubjectId: "cs1", classId: "c1", label: "SW-25A · Java" }, { classSubjectId: "cs2", classId: "c2", label: "SW-25B · Reti" },
  ]);
  axiosClient.get.mockReturnValueOnce(Promise.reject(Object.assign(new Error("x"), { status: 403 })));
  await expect(getGradeClassSubjects("tok")).resolves.toBeNull();
});

test("studenti della classe per cognome", async () => {
  axiosClient.get.mockReturnValueOnce(ok([{ id: "b", firstName: "Anna", lastName: "Verdi" }, { id: "a", firstName: "Luca", lastName: "Bianchi" }]));
  const list = await getClassStudents("c1", "tok");
  expect(axiosClient.get).toHaveBeenCalledWith("/institution/students", { ...H, params: { classId: "c1" } });
  expect(list.map(s => s.id)).toEqual(["a", "b"]);
});

test("errori per riga del 400 GradeBatchInvalid: messaggi italiani, studente anche dall'index", () => {
  const error = { status: 400, code: "GradeBatchInvalid", errors: [
    { index: 0, studentId: "s1", code: "student_not_in_class", message: "x" },
    { index: 1, code: "invalid_value" },
    { index: 2, studentId: "s3", code: "duplicate_student" },
    { index: 3, studentId: "s4", code: "student_not_found" },
    { index: 4, studentId: "s5", code: "altro", message: "Motivo del server" },
  ] };
  const sent = [{ studentId: "s1" }, { studentId: "s2" }, { studentId: "s3" }, { studentId: "s4" }, { studentId: "s5" }];
  expect(batchRowErrors(error, sent)).toEqual({
    s1: BATCH_ROW_MESSAGES.student_not_in_class, s2: "Il voto deve essere un numero intero tra 0 e 30.",
    s3: BATCH_ROW_MESSAGES.duplicate_student, s4: BATCH_ROW_MESSAGES.student_not_found, s5: "Motivo del server",
  });
  expect(batchRowErrors({ status: 400, message: "Dati non validi." }, sent)).toBeNull(); // errori di forma
  expect(batchRowErrors({ status: 404 }, sent)).toBeNull();
});

test("axiosClient conserva gli errori per riga del 400 (non per i 5xx)", async () => {
  jest.restoreAllMocks();
  const fail = (status, data) => {
    axiosClient.defaults.adapter = (config) => Promise.reject(Object.assign(new Error("x"), { config, response: { status, data, headers: {}, config } }));
    return axiosClient.post("/institution/grades/batch", {}).catch(e => e);
  };
  const rows = [{ index: 0, studentId: "s1", code: "invalid_value" }];
  const e = await fail(400, { code: "GradeBatchInvalid", errors: rows });
  expect(e).toMatchObject({ status: 400, code: "GradeBatchInvalid", errors: rows });
  expect((await fail(500, { errors: rows })).errors).toBeUndefined();
  delete axiosClient.defaults.adapter;
});
