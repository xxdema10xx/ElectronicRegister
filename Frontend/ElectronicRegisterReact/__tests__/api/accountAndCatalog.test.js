// Account creati dall'istituto e scritture del catalogo (classi, aree, corsi, materie della classe).
import axiosClient from "../../src/api/axiosClient";
import { session } from "../../src/api/session";
import { accountErrorMessage, accountFormErrors, createAccount, normalizeFiscalCode } from "../../src/api/accountService";
import {
  addClassSubject, changeClassSubjectTeacher, createBiennium, createClass, createStudyArea, createStudyPath, deleteClass,
  getClassDetail, linkStudyArea, linkStudyPath, removeClassSubject,
} from "../../src/api/classService";

const ok = (data) => Promise.resolve({ data });
const H = { headers: { Authorization: "Bearer tok" } };

beforeEach(() => {
  session.clear();
  for (const m of ["get", "post", "put", "delete"]) jest.spyOn(axiosClient, m).mockReturnValue(ok("new-id"));
});
afterEach(() => jest.restoreAllMocks());

describe("account dell'istituto", () => {
  const valid = { firstName: "Anna Maria", lastName: "De Luca", fiscalCode: "RSSMRA80A01H501U", roles: ["Student"], reason: "Iscrizione" };

  test("modulo valido", () => {
    expect(accountFormErrors(valid)).toEqual({});
    expect(accountFormErrors({ ...valid, fiscalCode: "rss mra 80a01 h501u" })).toEqual({}); // spazi e minuscole tollerati
    expect(normalizeFiscalCode(" rssmra80a01h501u ")).toBe("RSSMRA80A01H501U");
  });

  test("campi obbligatori, limiti e combinazione di ruoli non ammessa", () => {
    const e = accountFormErrors({ firstName: " ", lastName: "x".repeat(101), fiscalCode: "ABC", roles: [], reason: "" });
    expect(Object.keys(e).sort()).toEqual(["firstName", "fiscalCode", "lastName", "reason", "roles"]);
    expect(accountFormErrors({ ...valid, roles: ["Student", "Teacher"] }).roles).toMatch(/Studente e docente/);
    expect(accountFormErrors({ ...valid, reason: "x".repeat(501) }).reason).toMatch(/500/);
  });

  test("richiesta: senza email (la genera il server), codice fiscale normalizzato, id facoltativi a null", async () => {
    axiosClient.post.mockReturnValue(ok({ email: "anna.maria.de.luca@its.example", temporaryPassword: "AbC-123" }));
    const res = await createAccount({ ...valid, eventId: "ev1", fiscalCode: "rssmra80a01h501u", firstName: " Anna Maria " }, "tok");
    expect(res.temporaryPassword).toBe("AbC-123");
    const [path, body, cfg] = axiosClient.post.mock.calls[0];
    expect(path).toBe("/institution/accounts");
    expect(body).toEqual({ eventId: "ev1", firstName: "Anna Maria", lastName: "De Luca", fiscalCode: "RSSMRA80A01H501U",
      roles: ["Student"], classId: null, studentId: null, teacherId: null, reason: "Iscrizione" });
    expect(body).not.toHaveProperty("email");
    expect(cfg).toEqual(H);
  });

  test.each([
    [{ status: 404 }, /non è attiva/],
    [{ status: 422 }, /non coincidono con quelli della scheda/],
    [{ status: 409, problem: "Indirizzo email non disponibile" }, /^Indirizzo email non disponibile$/],
    [{ status: 409, problem: "Persona già nell'istituto" }, /^Persona già nell'istituto$/],
    [{ status: 409 }, /^Impossibile creare l'account: conflitto con dati esistenti\.$/],
    [{ status: 409, code: "ConcurrentChange", problem: "Conflitto sulla membership istituzionale" }, /^Modifica concorrente: ricarica e riprova\.$/],
    [{ status: 503 }, /occupato/],
    [{ status: 400, message: "Codice fiscale non valido." }, /Codice fiscale non valido/],
  ])("errore %o", (e, msg) => {
    expect(accountErrorMessage(e)).toMatch(msg);
  });
});

describe("catalogo e assegnazioni", () => {
  test("biennio con le date", async () => {
    await createBiennium({ startDate: "2026-09-01", endDate: "2028-07-31" }, "tok");
    expect(axiosClient.post).toHaveBeenCalledWith("/institution/biennia", { startYear: "2026-09-01", endYear: "2028-07-31" }, H);
  });

  test("area e corso: creazione (descrizione dell'area sempre presente) e collegamento", async () => {
    await createStudyArea({ name: " ICT ", description: undefined }, "tok");
    expect(axiosClient.post).toHaveBeenLastCalledWith("/institution/study-areas", { name: "ICT", description: "" }, H);
    await createStudyPath({ name: "Software", description: "  " }, "tok");
    expect(axiosClient.post).toHaveBeenLastCalledWith("/institution/study-paths", { name: "Software", description: null }, H);
    await linkStudyArea("b1", "a1", "tok");
    expect(axiosClient.post).toHaveBeenLastCalledWith("/institution/biennium-study-areas", { bienniumId: "b1", studyAreaId: "a1" }, H);
    await linkStudyPath("ba1", "p1", "tok");
    expect(axiosClient.post).toHaveBeenLastCalledWith("/institution/biennium-study-paths", { bienniumStudyAreaId: "ba1", studyPathId: "p1" }, H);
  });

  test("classe: creazione ed eliminazione", async () => {
    await createClass({ name: " SW-25A ", bienniumStudyPathId: "bp1" }, "tok");
    expect(axiosClient.post).toHaveBeenLastCalledWith("/institution/classes", { name: "SW-25A", bienniumStudyPathId: "bp1" }, H);
    await deleteClass("c1", "tok");
    expect(axiosClient.delete).toHaveBeenCalledWith("/institution/classes/c1", H);
  });

  test("materia della classe: aggiunta, cambio docente con If-Match, rimozione", async () => {
    await addClassSubject({ classId: "c1", subjectId: "m1", teacherId: "t1" }, "tok");
    expect(axiosClient.post).toHaveBeenLastCalledWith("/institution/class-subjects", { classId: "c1", subjectId: "m1", teacherId: "t1" }, H);
    await changeClassSubjectTeacher("cs1", "t2", 4, "tok");
    expect(axiosClient.put).toHaveBeenCalledWith("/institution/class-subjects/cs1", { teacherId: "t2" },
      { headers: { Authorization: "Bearer tok", "If-Match": "\"4\"" } });
    await removeClassSubject("cs1", "tok");
    expect(axiosClient.delete).toHaveBeenCalledWith("/institution/class-subjects/cs1", H);
  });

  test("il dettaglio della classe porta la revisione delle assegnazioni (serve per If-Match)", async () => {
    axiosClient.get.mockImplementation((path) => ok(path.includes("class-subjects")
      ? [{ id: "cs1", subjectId: "m1", subjectName: "Reti", teacherId: "t1", revision: 7 }] : []));
    const d = await getClassDetail("c1", "SW", "tok");
    expect(d.subjects[0]).toMatchObject({ classSubjectId: "cs1", revision: 7 });
  });
});
