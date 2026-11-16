import axiosClient from "../../src/api/axiosClient";
import { session } from "../../src/api/session";
import {
  calendarErrorMessage, confirmImport, conflictMessage, getCalendarSettings, getMonthStatus, getPeriod, issueMessage, monthRange,
  previewImport, publishPeriod,
} from "../../src/api/calendarImportService";

const ok = (data) => Promise.resolve({ data });
const err = (props) => Promise.reject(Object.assign(new Error("x"), props));

beforeEach(() => {
  session.clear();
  jest.spyOn(axiosClient, "get");
  jest.spyOn(axiosClient, "post").mockReturnValue(ok({}));
});
afterEach(() => jest.restoreAllMocks());

test("monthRange: primo e ultimo giorno del mese (anche bisestile)", () => {
  expect(monthRange(2026, 11)).toEqual({ from: "2026-11-01", to: "2026-11-30" });
  expect(monthRange(2028, 2)).toEqual({ from: "2028-02-01", to: "2028-02-29" });
});

describe("impostazioni", () => {
  test("configurate", async () => {
    axiosClient.get.mockReturnValue(ok({ timeZone: "Europe/Rome" }));
    await expect(getCalendarSettings("tok")).resolves.toEqual({ timeZone: "Europe/Rome" });
  });

  test("da configurare → null", async () => {
    axiosClient.get.mockReturnValue(err({ status: 409, code: "calendar.settings-required" }));
    await expect(getCalendarSettings("tok")).resolves.toBeNull();
  });

  test("altri errori si propagano", async () => {
    axiosClient.get.mockReturnValue(err({ status: 500 }));
    await expect(getCalendarSettings("tok")).rejects.toThrow();
  });
});

describe("import", () => {
  test("anteprima: multipart con file e impostazioni, timeout lungo", async () => {
    // FormData di React Native (nei test c'è quello di Node, che non accetta l'oggetto { uri, name, type })
    const RealFormData = global.FormData;
    global.FormData = class { constructor() { this.parts = []; } append(fieldName, value) { this.parts.push({ fieldName, value }); } getParts() {
      return this.parts.map(p => (typeof p.value === "string" ? { fieldName: p.fieldName, string: p.value } : { fieldName: p.fieldName, ...p.value }));
    } };
    axiosClient.post.mockReturnValue(ok({ id: "p1" }));
    const file = { uri: "file:///x.xlsx", name: "orario.xlsx", mimeType: "application/xlsx" };
    try {
      await expect(previewImport({ file, year: 2026, month: 12, classIds: ["c1"], mode: "AppendDraft", reason: "  Dicembre " }, "tok"))
        .resolves.toEqual({ id: "p1" });
    } finally { global.FormData = RealFormData; }
    const [path, form, cfg] = axiosClient.post.mock.calls[0];
    expect(path).toBe("/institution/calendar/imports/preview");
    expect(cfg.headers["Content-Type"]).toBe("multipart/form-data");
    expect(cfg.headers.Authorization).toBe("Bearer tok");
    expect(cfg.timeout).toBe(60000);
    const parts = Object.fromEntries(form.getParts().map(p => [p.fieldName, p]));
    expect(parts.file).toMatchObject({ uri: "file:///x.xlsx", name: "orario.xlsx", type: "application/xlsx" });
    expect(JSON.parse(parts.settings.string)).toMatchObject({ year: 2026, month: 12, classIds: ["c1"], mode: "AppendDraft", reason: "Dicembre", mapping: null });
  });

  test("conferma: revisione e digest dell'anteprima", async () => {
    await confirmImport({ id: "p1", revision: 3, digest: "abc" }, { acknowledgeWarnings: 1, reason: " ok " }, "tok");
    const [path, body] = axiosClient.post.mock.calls[0];
    expect(path).toBe("/institution/calendar/imports/p1/confirm");
    expect(body).toMatchObject({ expectedRevision: 3, digest: "abc", acknowledgeWarnings: true, reason: "ok" });
  });
});

describe("mese della classe", () => {
  test("periodo: null se la classe non ha lezioni (404)", async () => {
    axiosClient.get.mockReturnValueOnce(ok({ revision: 2 })).mockReturnValueOnce(err({ status: 404 }));
    await expect(getPeriod("c1", 2026, 12, "tok")).resolves.toEqual({ revision: 2 });
    expect(axiosClient.get.mock.calls[0][0]).toBe("/institution/calendar/months/2026/12/classes/c1");
    await expect(getPeriod("c1", 2026, 12, "tok")).resolves.toBeNull();
  });

  test("pubblicazione", async () => {
    await publishPeriod("c1", 2026, 12, 4, " motivo ", "tok");
    expect(axiosClient.post.mock.calls[0][0]).toBe("/institution/calendar/months/2026/12/classes/c1/publish");
    expect(axiosClient.post.mock.calls[0][1]).toMatchObject({ expectedRevision: 4, reason: "motivo" });
  });

  test("stato del mese: bozze e pubblicate contate su tutte le pagine", async () => {
    axiosClient.get.mockImplementation((path, { params } = {}) => {
      if (path.includes("/months/")) return ok({ revision: 1 });
      if (params.publication === "Draft") return ok(params.page === 1 ? new Array(100).fill({}) : new Array(5).fill({}));
      return ok(new Array(3).fill({}));
    });
    await expect(getMonthStatus("c1", 2026, 12, "tok")).resolves.toEqual({ period: { revision: 1 }, drafts: 105, published: 3 });
  });
});

describe("messaggi", () => {
  test("codici noti, 404, 409 con codice e messaggio di ripiego", () => {
    expect(calendarErrorMessage({ code: "FileTooLarge" })).toMatch(/12 MB/);
    expect(calendarErrorMessage({ status: 404 })).toMatch(/non è attivo/);
    expect(calendarErrorMessage({ status: 409, code: "calendar.altro" })).toBe("Operazione non consentita (calendar.altro).");
    expect(calendarErrorMessage({ message: "Boom" })).toBe("Boom");
    expect(calendarErrorMessage(null)).toBe("Errore imprevisto.");
  });

  test("problemi delle righe", () => {
    expect(issueMessage({ code: "InvalidValue", field: "Date" })).toBe("Data: valore non valido");
    expect(issueMessage({ code: "calendar.invalid-time", field: "StartsAt" })).toBe("Ora non valida: usa HH:mm");
    expect(issueMessage({ code: "IgnoredColumn", field: "2" }, ["Data", "Classe", "Note"])).toBe("Colonna non riconosciuta, verrà ignorata: \"Note\"");
    expect(issueMessage({ code: "Sconosciuto", message: "Testo del server" })).toBe("Testo del server");
    expect(issueMessage({ code: "Sconosciuto" })).toBe("Sconosciuto");
  });

  test("conflitti come numero o come nome", () => {
    expect(conflictMessage({ kind: 1 })).toBe("Sovrapposta a un'altra lezione della classe");
    expect(conflictMessage({ kind: "TeacherOverlap" })).toBe("Il docente ha già un'altra lezione");
    expect(conflictMessage({ kind: 99 })).toBe("Conflitto di orario");
  });
});
