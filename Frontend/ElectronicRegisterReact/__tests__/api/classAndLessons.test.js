// Catalogo classi (biennio → area → corso → classe), classi del docente e orario delle lezioni.
import axiosClient from "../../src/api/axiosClient";
import { session } from "../../src/api/session";
import {
  getActiveBienni, getAllClasses, getClassDetail, getClasses, getMyClasses, getStudentClassSubjects, getStudyAreas, getStudyPaths,
} from "../../src/api/classService";
import {
  CLASS_LIST_FORBIDDEN_MESSAGE, DAYS, classListErrorMessage, currentWeek, formatWeekRange, getClassesForPicker, getSchedule, lessonLabels, parseYmd, shiftWeek, toLesson, weekOf,
} from "../../src/api/lessonService";

// Server finto: path → dati (o funzione dei params)
function serve(routes) {
  jest.spyOn(axiosClient, "get").mockImplementation((path, { params } = {}) => {
    const r = routes[path];
    if (r === undefined) return Promise.reject(Object.assign(new Error(`404 ${path}`), { status: 404 }));
    return Promise.resolve({ data: typeof r === "function" ? r(params || {}) : r });
  });
}

beforeEach(() => session.clear());
afterEach(() => jest.restoreAllMocks());

describe("catalogo classi", () => {
  test("bienni: anni ricavati dalle date, dal più recente; paginazione { items }", async () => {
    serve({ "/institution/biennia": { items: [{ id: "b1", startYear: "2024-09-01", endYear: "2026-07-31" }, { id: "b2", startYear: "2025-09-01", endYear: "2027-07-31" }] } });
    const list = await getActiveBienni("tok");
    expect(list.map(b => [b.id, b.startYear, b.endYear])).toEqual([["b2", "2025", "2027"], ["b1", "2024", "2026"]]);
  });

  test("getAll legge tutte le pagine", async () => {
    const page = (n, off) => Array.from({ length: n }, (_, i) => ({ id: `c${off + i}`, name: `C${off + i}` }));
    serve({ "/institution/classes": (p) => (p.pageNumber === 1 ? page(100, 0) : page(2, 100)) });
    await expect(getAllClasses("tok")).resolves.toHaveLength(102);
  });

  test("aree del biennio: unione dei collegamenti con le aree", async () => {
    serve({
      "/institution/biennium-study-areas": [{ id: "ba1", bienniumId: "b1", studyAreaId: "a1" }, { id: "ba2", bienniumId: "b2", studyAreaId: "a2" }, { id: "ba3", bienniumId: "b1", studyAreaId: "zz" }],
      "/institution/study-areas": [{ id: "a1", name: "ICT", description: "Informatica" }, { id: "a2", name: "Turismo" }],
    });
    await expect(getStudyAreas("b1", "tok")).resolves.toEqual([
      { id: "ba1", studyAreaId: "a1", name: "ICT", description: "Informatica" },
      { id: "ba3", studyAreaId: "zz", name: "—", description: undefined },
    ]);
  });

  test("corsi dell'area", async () => {
    serve({
      "/institution/biennium-study-paths": [{ id: "bp1", bienniumStudyAreaId: "ba1", studyPathId: "p1" }, { id: "bp2", bienniumStudyAreaId: "other", studyPathId: "p1" }],
      "/institution/study-paths": [{ id: "p1", name: "Software developer" }],
    });
    await expect(getStudyPaths("ba1", "tok")).resolves.toEqual([{ id: "bp1", studyPathId: "p1", name: "Software developer", description: undefined }]);
  });

  test("classi del corso: filtro bienniumStudyPathId", async () => {
    serve({ "/institution/classes": (p) => [{ id: "c1", name: "SW-25A", pathParam: p.bienniumStudyPathId }] });
    await expect(getClasses("bp1", "tok")).resolves.toEqual([{ id: "c1", name: "SW-25A", pathParam: "bp1" }]);
  });

  test("dettaglio classe: studenti e materie con docente", async () => {
    serve({
      "/institution/students": (p) => (p.classId === "c1" ? [{ id: "s1", firstName: "Mario", lastName: "Rossi" }] : []),
      "/institution/class-subjects": [{ id: "cs1", subjectId: "m1", subjectName: "Reti", teacherId: "t1", teacherFirstName: "Anna", teacherLastName: "Neri" }],
    });
    await expect(getClassDetail("c1", "SW-25A", "tok")).resolves.toEqual({
      id: "c1", name: "SW-25A", students: [{ id: "s1", firstName: "Mario", lastName: "Rossi" }],
      subjects: [{ classSubjectId: "cs1", subjectId: "m1", subjectName: "Reti", teacherId: "t1", teacherFirstName: "Anna", teacherLastName: "Neri" }],
    });
  });

  test("materie della classe di uno studente, senza doppioni e in ordine", async () => {
    serve({
      "/institution/class-subjects": [
        { classId: "c1", subjectId: "m2", subjectName: "Reti" }, { classId: "c1", subjectId: "m1", subjectName: "Basi di dati" },
        { classId: "c1", subjectId: "m2", subjectName: "Reti" }, { classId: "c2", subjectId: "m3", subjectName: "Inglese" },
      ],
      "/institution/students": (p) => (p.classId === "c1" ? [{ id: "s1" }] : [{ id: "s9" }]),
    });
    await expect(getStudentClassSubjects("s1", "tok")).resolves.toEqual([{ id: "m1", name: "Basi di dati" }, { id: "m2", name: "Reti" }]);
    await expect(getStudentClassSubjects("nessuno", "tok")).resolves.toEqual([]);
  });

  test("classi del docente: solo le sue materie, studenti per classe, ordine per nome", async () => {
    serve({
      "/institution/class-subjects": [
        { id: "cs1", classId: "c2", className: "SW-25B", teacherId: "t1", subjectId: "m1", subjectName: "Reti" },
        { id: "cs2", classId: "c1", className: "SW-25A", teacherId: "t1", subjectId: "m2", subjectName: "Java" },
        { id: "cs3", classId: "c1", className: "SW-25A", teacherId: "t2", subjectId: "m3", subjectName: "Inglese" },
      ],
      "/institution/students": (p) => (p.classId === "c1" ? [{ id: "s1" }, { id: "s2" }] : [{ id: "s3" }]),
    });
    const list = await getMyClasses("t1", "tok");
    expect(list.map(k => k.name)).toEqual(["SW-25A", "SW-25B"]);
    expect(list[0].subjects.map(x => x.subjectName)).toEqual(["Java"]);
    expect(list[0].students).toHaveLength(2);
  });

  test("docente senza profilo: nessuna classe", async () => {
    await expect(getMyClasses(null, "tok")).resolves.toEqual([]);
  });
});

describe("orario delle lezioni", () => {
  test("settimana corrente da lunedì a domenica", () => {
    expect(currentWeek(new Date(2026, 10, 18))).toEqual({ from: "2026-11-16", to: "2026-11-22" }); // mercoledì
    expect(currentWeek(new Date(2026, 10, 22))).toEqual({ from: "2026-11-16", to: "2026-11-22" }); // domenica
    expect(currentWeek(new Date(2026, 10, 16))).toEqual({ from: "2026-11-16", to: "2026-11-22" }); // lunedì
  });

  test("lezione del server → lezione dell'orario", () => {
    expect(toLesson({ id: 1, date: "2026-11-19", startsAt: "09:00:00", endsAt: "11:00:00", moduleName: "Reti", className: "SW", room: "A1" }, "Anna Neri"))
      .toEqual({ id: 1, date: "2026-11-19", day: 4, start: "09:00", end: "11:00", title: "Reti", teacherName: "Anna Neri", className: "SW", room: "A1",
        status: "Scheduled", publication: "Published", substitute: false, recovery: false });
    expect(toLesson({ id: 2, date: "2026-11-22", startsAt: "08:00", endsAt: "09:00" }).title).toBe("Lezione");
    expect(DAYS).toHaveLength(7);
  });

  test("percorso per ruolo; admin senza classe → nessuna lezione", async () => {
    serve({});
    await expect(getSchedule({ role: "admin" }, "tok")).resolves.toEqual([]);
    await expect(getSchedule({ role: "staff" }, "tok")).resolves.toEqual([]);
    expect(axiosClient.get).not.toHaveBeenCalled();
  });

  test("docente: tutte le pagine, ordinate per giorno e ora", async () => {
    const lessons = [
      { id: "b", date: "2026-11-17", startsAt: "10:00", endsAt: "11:00" },
      { id: "a", date: "2026-11-17", startsAt: "08:00", endsAt: "09:00" },
      { id: "c", date: "2026-11-16", startsAt: "14:00", endsAt: "15:00" },
    ];
    serve({ "/institution/calendar/teachers/me": (p) => (p.page === 1 ? lessons : []) });
    const out = await getSchedule({ role: "teacher", teacherName: "Anna" }, "tok");
    expect(out.map(l => l.id)).toEqual(["c", "a", "b"]);
    expect(out[0].teacherName).toBe("Anna");
    expect(axiosClient.get.mock.calls[0][1].params).toMatchObject({ page: 1, size: 100 });
  });

  test("admin: lezioni pubblicate della classe scelta", async () => {
    serve({ "/institution/calendar/classes/c1": [] });
    await getSchedule({ role: "admin", classId: "c1" }, "tok");
    expect(axiosClient.get.mock.calls[0][1].params).toMatchObject({ publication: "Published" });
  });

  test("studente: proprio orario", async () => {
    serve({ "/institution/calendar/students/me": [{ id: 1, date: "2026-11-16", startsAt: "08:00", endsAt: "09:00" }] });
    await expect(getSchedule({ role: "student" }, "tok")).resolves.toHaveLength(1);
  });

  test("weekOf: bordi di settimana, mese, anno e ora legale", () => {
    expect(weekOf("2026-11-22")).toEqual({ from: "2026-11-16", to: "2026-11-22" }); // domenica: resta nella sua settimana
    expect(weekOf("2026-11-23")).toEqual({ from: "2026-11-23", to: "2026-11-29" }); // lunedì
    expect(weekOf("2026-12-02")).toEqual({ from: "2026-11-30", to: "2026-12-06" }); // cambio mese
    expect(weekOf("2027-01-01")).toEqual({ from: "2026-12-28", to: "2027-01-03" }); // cambio anno
    expect(weekOf("2026-03-29")).toEqual({ from: "2026-03-23", to: "2026-03-29" }); // domenica dell'ora legale
    expect(weekOf("2026-10-25")).toEqual({ from: "2026-10-19", to: "2026-10-25" }); // domenica del ritorno all'ora solare
    expect(weekOf(new Date(2026, 10, 18, 23, 59))).toEqual({ from: "2026-11-16", to: "2026-11-22" });
  });

  test("shiftWeek attraversa ora legale e cambio d'anno senza perdere giorni", () => {
    expect(shiftWeek("2026-03-23", 1)).toEqual({ from: "2026-03-30", to: "2026-04-05" });
    expect(shiftWeek("2026-10-26", -1)).toEqual({ from: "2026-10-19", to: "2026-10-25" });
    expect(shiftWeek("2026-12-28", 1)).toEqual({ from: "2027-01-04", to: "2027-01-10" });
    expect(shiftWeek("2026-11-16", -52).from).toBe("2025-11-17");
  });

  test("intervallo mostrato e date non valide", () => {
    expect(formatWeekRange({ from: "2026-11-16", to: "2026-11-22" })).toBe("16–22 novembre 2026");
    expect(formatWeekRange({ from: "2026-11-30", to: "2026-12-06" })).toBe("30 novembre – 6 dicembre 2026");
    expect(formatWeekRange({ from: "2026-12-28", to: "2027-01-03" })).toBe("28 dicembre 2026 – 3 gennaio 2027");
    expect(parseYmd("2026-02-30")).toBeNull();
    expect(parseYmd("16/11/2026")).toBeNull();
  });

  test("etichette di stato: annullata, svolta, supplenza, recupero, bozza solo se richiesta", () => {
    const l = toLesson({ id: 1, date: "2026-11-16", startsAt: "08:00", endsAt: "09:00", status: "Cancelled", publication: "Draft",
      teacherId: "t2", originalTeacherId: "t1", recoveryOfLessonId: "x" });
    expect(lessonLabels(l).map(x => x.label)).toEqual(["Annullata", "Supplenza", "Recupero"]);
    expect(lessonLabels(l, { showDraft: true }).map(x => x.label)).toContain("Bozza");
    const done = toLesson({ id: 2, date: "2026-11-16", startsAt: "08:00", endsAt: "09:00", status: "Completed", teacherId: "t1", originalTeacherId: "t1" });
    expect(lessonLabels(done).map(x => x.label)).toEqual(["Svolta"]);
  });

  test("intervallo scelto, annullate incluse, tutte le pagine; gestione senza filtro di pubblicazione", async () => {
    const page1 = Array.from({ length: 100 }, (_, i) => ({ id: `l${i}`, date: "2026-12-01", startsAt: "08:00", endsAt: "09:00" }));
    serve({ "/institution/calendar/classes/c1": (p) => (p.page === 1 ? page1 : [{ id: "last", date: "2026-12-02", startsAt: "08:00", endsAt: "09:00" }]) });
    const out = await getSchedule({ role: "staff", classId: "c1", from: "2026-11-30", to: "2026-12-06", includeCancelled: true, publication: null }, "tok");
    expect(out).toHaveLength(101);
    const [first, second] = axiosClient.get.mock.calls.map(c => c[1].params);
    expect(first).toEqual({ from: "2026-11-30", to: "2026-12-06", includeCancelled: true, page: 1, size: 100 });
    expect(second.page).toBe(2);
  });

  test("classi per il selettore: solo id e nome, in ordine", async () => {
    serve({ "/institution/classes": [{ id: "2", name: "SW-25B", x: 1 }, { id: "1", name: "SW-25A" }] });
    await expect(getClassesForPicker("tok")).resolves.toEqual([{ id: "1", name: "SW-25A" }, { id: "2", name: "SW-25B" }]);
  });

  test("admin: le classi arrivano dal catalogo, non dalle classi-materie", async () => {
    serve({ "/institution/classes": [{ id: "1", name: "SW-25A" }] });
    await expect(getClassesForPicker("tok", "admin")).resolves.toEqual([{ id: "1", name: "SW-25A" }]);
    expect(axiosClient.get.mock.calls.map(c => c[0])).toEqual(["/institution/classes"]);
  });

  test("staff: classi distinte dalle classi-materie, ordinate per nome, senza chiamare il catalogo (403 per lo staff)", async () => {
    serve({
      "/institution/class-subjects": [
        { id: "cs1", classId: "c2", className: "SW-25B", subjectId: "s1" },
        { id: "cs2", classId: "c1", className: "SW-25A", subjectId: "s1" },
        { id: "cs3", classId: "c2", className: "SW-25B", subjectId: "s2" },
      ],
    });
    await expect(getClassesForPicker("tok", "staff")).resolves.toEqual([{ id: "c1", name: "SW-25A" }, { id: "c2", name: "SW-25B" }]);
    expect(axiosClient.get.mock.calls.map(c => c[0])).not.toContain("/institution/classes");
  });

  test("staff: un 403 sulle classi-materie arriva al chiamante, con un messaggio chiaro", async () => {
    jest.spyOn(axiosClient, "get").mockRejectedValue(Object.assign(new Error("Non hai i permessi per questa operazione (o la funzione non è attiva)."), { status: 403 }));
    const error = await getClassesForPicker("tok", "staff").catch(e => e);
    expect(error.status).toBe(403);
    expect(classListErrorMessage(error)).toBe(CLASS_LIST_FORBIDDEN_MESSAGE);
    expect(classListErrorMessage({ status: 500, message: "Errore sul server." })).toBe("Impossibile caricare l'elenco delle classi. Errore sul server.");
  });
});
