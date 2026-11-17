// Servizi dell'anagrafica e del registro: URL, corpo delle richieste, paginazione e trasformazioni.
import axiosClient from "../../src/api/axiosClient";
import { session } from "../../src/api/session";
import * as students from "../../src/api/studentService";
import * as teachers from "../../src/api/teacherService";
import * as subjects from "../../src/api/subjectService";
import * as grades from "../../src/api/gradeService";
import * as users from "../../src/api/userService";
import * as classGrades from "../../src/api/classGradeService";

const ok = (data) => Promise.resolve({ data });
const notFound = () => Promise.reject(Object.assign(new Error("404"), { status: 404 }));
const H = { headers: { Authorization: "Bearer tok" } };

beforeEach(() => {
  session.clear();
  for (const m of ["get", "post", "put", "delete"]) jest.spyOn(axiosClient, m).mockReturnValue(ok("id"));
});
afterEach(() => jest.restoreAllMocks());

describe("studenti", () => {
  test("elenco, filtro per classe e ricerca per cognome", async () => {
    await students.getStudents("", "tok");
    expect(axiosClient.get).toHaveBeenLastCalledWith("/institution/students", { ...H, params: undefined });
    await students.getStudents("", "tok", "c1");
    expect(axiosClient.get).toHaveBeenLastCalledWith("/institution/students", { ...H, params: { classId: "c1" } });
    await students.getStudents("De Luca", "tok", "c1");
    expect(axiosClient.get).toHaveBeenLastCalledWith("/institution/students/bylastname/De%20Luca", { ...H, params: undefined });
  });

  test("creazione, modifica (nomi senza spazi) ed eliminazione", async () => {
    await students.createStudent({ firstName: " Mario ", lastName: "Rossi " }, "tok", "c1");
    expect(axiosClient.post).toHaveBeenCalledWith("/institution/students", { firstName: "Mario", lastName: "Rossi" }, { ...H, params: { classId: "c1" } });
    await students.updateStudent("s1", { firstName: "M", lastName: "R" }, "tok");
    expect(axiosClient.put).toHaveBeenCalledWith("/institution/students/s1", { firstName: "M", lastName: "R" }, H);
    await students.deleteStudent("s1", "tok");
    expect(axiosClient.delete).toHaveBeenCalledWith("/institution/students/s1", H);
  });
});

describe("docenti", () => {
  test("CRUD", async () => {
    await teachers.getTeachers("tok");
    expect(axiosClient.get).toHaveBeenCalledWith("/institution/teachers", H);
    await teachers.createTeacher({ firstName: " Anna", lastName: "Neri " }, "tok");
    expect(axiosClient.post).toHaveBeenCalledWith("/institution/teachers", { firstName: "Anna", lastName: "Neri" }, H);
    await teachers.updateTeacher("t1", { firstName: "A", lastName: "N" }, "tok");
    expect(axiosClient.put).toHaveBeenCalledWith("/institution/teachers/t1", { firstName: "A", lastName: "N" }, H);
    await teachers.deleteTeacher("t1", "tok");
    expect(axiosClient.delete).toHaveBeenCalledWith("/institution/teachers/t1", H);
  });
});

describe("materie", () => {
  test("ricerca per nome esatto: una materia, oppure nessuna se 404", async () => {
    axiosClient.get.mockReturnValueOnce(ok({ id: "m1", name: "Reti" }));
    await expect(subjects.getSubjects("Reti", "tok")).resolves.toEqual([{ id: "m1", name: "Reti" }]);
    axiosClient.get.mockReturnValueOnce(notFound());
    await expect(subjects.getSubjects("Nessuna", "tok")).resolves.toEqual([]);
  });

  test("altri errori della ricerca si propagano", async () => {
    axiosClient.get.mockReturnValueOnce(Promise.reject(Object.assign(new Error("boom"), { status: 500 })));
    await expect(subjects.getSubjects("x", "tok")).rejects.toThrow("boom");
  });

  test("CRUD", async () => {
    axiosClient.get.mockReturnValueOnce(ok([{ id: "m1" }]));
    await expect(subjects.getSubjects(undefined, "tok")).resolves.toEqual([{ id: "m1" }]);
    await subjects.createSubject({ name: " Reti " }, "tok");
    expect(axiosClient.post).toHaveBeenCalledWith("/institution/subjects", { name: "Reti" }, H);
    await subjects.updateSubject("m1", { name: "Basi di dati" }, "tok");
    expect(axiosClient.put).toHaveBeenCalledWith("/institution/subjects/m1", { name: "Basi di dati" }, H);
    await subjects.deleteSubject("m1", "tok");
    expect(axiosClient.delete).toHaveBeenCalledWith("/institution/subjects/m1", H);
  });
});

describe("voti", () => {
  test("pagina: solo i filtri impostati", async () => {
    await grades.getGradesPage({ pageNumber: 2, pageSize: 20, subjectId: "", studentId: "s1", date: null }, "tok");
    expect(axiosClient.get).toHaveBeenCalledWith("/institution/grades/paged", { ...H, params: { pageNumber: 2, pageSize: 20, studentId: "s1" } });
  });

  test("filtri", async () => {
    await grades.getGradeFilters("tok");
    expect(axiosClient.get).toHaveBeenCalledWith("/institution/grades/filters", H);
  });

  test("il voto accetta la virgola decimale", async () => {
    await grades.createGrade({ studentId: "s1", subjectId: "m1", value: "7,5", date: "2026-11-16" }, "tok");
    expect(axiosClient.post).toHaveBeenCalledWith("/institution/grades", { studentId: "s1", subjectId: "m1", value: 7.5, date: "2026-11-16" }, H);
    await grades.updateGrade("g1", { subjectId: "m1", value: 8, date: "2026-11-17" }, "tok");
    expect(axiosClient.put).toHaveBeenCalledWith("/institution/grades/g1", { subjectId: "m1", value: 8, date: "2026-11-17" }, H);
    await grades.deleteGrade("g1", "tok");
    expect(axiosClient.delete).toHaveBeenCalledWith("/institution/grades/g1", H);
  });

  test("voti di uno studente (docente): dal più recente", async () => {
    axiosClient.get.mockReturnValue(ok({ items: [{ id: 1, date: "2026-01-01" }, { id: 2, date: "2026-03-01" }, { id: 3, date: "2026-02-01" }] }));
    const list = await classGrades.getGradesForStudent("s1", "tok");
    expect(list.map(g => g.id)).toEqual([2, 3, 1]);
    expect(axiosClient.get).toHaveBeenCalledWith("/institution/grades/paged", { ...H, params: { pageNumber: 1, pageSize: 100, studentId: "s1" } });
  });

  test("voti di uno studente: risposta senza items → elenco vuoto", async () => {
    axiosClient.get.mockReturnValue(ok(null));
    await expect(classGrades.getGradesForStudent("s1", "tok")).resolves.toEqual([]);
  });

  test("aggiunta/modifica/eliminazione dalla classe passano dal registro", async () => {
    await classGrades.addClassGrade({ studentId: "s1", subjectId: "m1", value: "6", date: "d" }, "tok");
    expect(axiosClient.post).toHaveBeenCalledWith("/institution/grades", { studentId: "s1", subjectId: "m1", value: 6, date: "d" }, H);
    await classGrades.updateClassGrade("g1", { subjectId: "m1", value: "9", date: "d" }, "tok");
    expect(axiosClient.put).toHaveBeenCalledWith("/institution/grades/g1", { subjectId: "m1", value: 9, date: "d" }, H);
    await classGrades.deleteClassGrade("g1", "tok");
    expect(axiosClient.delete).toHaveBeenCalledWith("/institution/grades/g1", H);
  });
});

describe("membri dell'istituto", () => {
  test("legge tutte le pagine finché una non è piena", async () => {
    const page = (n, from) => Array.from({ length: n }, (_, i) => ({ membershipId: `m${from + i}` }));
    axiosClient.get.mockReturnValueOnce(ok(page(100, 0))).mockReturnValueOnce(ok(page(3, 100)));
    const list = await users.getMembers("tok", { status: "active", search: "  mario  " });
    expect(list).toHaveLength(103);
    expect(axiosClient.get).toHaveBeenNthCalledWith(2, "/institution/admin/members",
      { ...H, params: { status: "active", search: "mario", pageNumber: 2, pageSize: 100 } });
  });

  test("getUsers (dashboard) = tutti i membri", async () => {
    axiosClient.get.mockReturnValueOnce(ok([{ membershipId: "m1" }]));
    await expect(users.getUsers("ignorato", "tok")).resolves.toEqual([{ membershipId: "m1" }]);
  });

  test("mutazione: expectedRevision = revision del membro letto subito prima, ruolo solo per Add/RemoveRole", async () => {
    axiosClient.get.mockReturnValue(ok({ membershipId: "m1", roles: ["Student"], revision: 5 }));
    axiosClient.post.mockReturnValue(ok({ status: "active", roles: ["Teacher"] }));
    await users.mutateMember("m1", { action: "AddRole", role: "Teacher", reason: "  incarico  " }, "tok");
    expect(axiosClient.get).toHaveBeenCalledWith("/institution/admin/members/m1", H);
    expect(axiosClient.get).not.toHaveBeenCalledWith("/institution/admin/audit", expect.anything());
    const body = axiosClient.post.mock.calls[0][1];
    expect(axiosClient.post.mock.calls[0][0]).toBe("/institution/admin/members/m1/mutations");
    expect(body).toMatchObject({ expectedRevision: 5, action: "AddRole", role: "Teacher", reason: "incarico" });
    expect(typeof body.eventId).toBe("string");

    axiosClient.get.mockReturnValue(ok({ membershipId: "m1", revision: 0 }));
    await users.mutateMember("m1", { action: "Suspend", reason: "r" }, "tok");
    const suspend = axiosClient.post.mock.calls[1][1];
    expect(suspend.expectedRevision).toBe(0);
    expect(suspend).not.toHaveProperty("role");
  });

  test("mutazione con la revisione del dettaglio già letto: nessuna rilettura", async () => {
    axiosClient.post.mockReturnValue(ok({ status: "active", roles: [] }));
    await users.mutateMember("m1", { action: "Suspend", reason: "r", expectedRevision: 9 }, "tok");
    expect(axiosClient.get).not.toHaveBeenCalled();
    expect(axiosClient.post.mock.calls[0][1].expectedRevision).toBe(9);
  });

  test("mutazione senza revisione nel DTO: errore chiaro, nessuna POST", async () => {
    axiosClient.get.mockReturnValue(ok({ membershipId: "m1" }));
    await expect(users.mutateMember("m1", { action: "Suspend", reason: "r" }, "tok")).rejects.toThrow(/revisione del membro/);
    expect(axiosClient.post).not.toHaveBeenCalled();
  });
});
