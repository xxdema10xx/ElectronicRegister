import axiosClient from "../../src/api/axiosClient";
import { session } from "../../src/api/session";
import {
  DELIMITER_MESSAGE, HOUR_WARNING_LABELS, NO_LESSONS_MESSAGE, calendarErrorMessage, confirmImport, confirmLabel, conflictMessage,
  getCalendarSettings, getMonthStatus, getPeriod, issueMessage, issueParts, monthRange, precheckCalendarCsv, previewImport, publishPeriod, readFileText,
} from "../../src/api/calendarImportService";
import { HOURS_WARNING_LABELS } from "../../src/api/calendarService";

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
    expect(calendarErrorMessage({ code: "FileTooLarge" })).toBe("File troppo grande per il server: dividilo per classe o salvalo in CSV.");
    expect(calendarErrorMessage({ status: 413 })).toMatch(/dividilo per classe/);
    expect(calendarErrorMessage({ status: 409, code: "calendar.teaching-configuration-required" })).toMatch(/chiedi all'amministrazione/);
    expect(calendarErrorMessage({ status: 404 })).toMatch(/non è attivo/);
    expect(calendarErrorMessage({ status: 409, code: "calendar.altro" })).toBe("Operazione non consentita (calendar.altro).");
    expect(calendarErrorMessage({ message: "Boom" })).toBe("Boom");
    expect(calendarErrorMessage(null)).toBe("Errore imprevisto.");
  });

  test("problemi delle righe", () => {
    expect(issueMessage({ code: "InvalidValue", field: "Date" })).toBe("Data: valore non valido");
    expect(issueMessage({ code: "calendar.invalid-time", field: "StartsAt" })).toBe("Ora non valida: usa HH:mm");
    expect(issueMessage({ code: "IgnoredColumn", field: "2" }, ["Data", "Classe", "Note"])).toBe("Colonna non riconosciuta, verrà ignorata: \"Note\"");
    // codice sconosciuto: mai grezzo da solo, né il testo generico del server ("Riferimenti o intervallo non validi.")
    expect(issueMessage({ code: "Sconosciuto", message: "Testo del server" })).toBe("Valore non accettato (Sconosciuto)");
    expect(issueMessage({ code: "Sconosciuto" })).toBe("Valore non accettato (Sconosciuto)");
  });

  test("conflitti come numero o come nome", () => {
    expect(conflictMessage({ kind: 1 })).toBe("Sovrapposta a un'altra lezione della classe");
    expect(conflictMessage({ kind: "TeacherOverlap" })).toBe("Il docente ha già un'altra lezione");
    expect(conflictMessage({ kind: 99 })).toBe("Conflitto di orario");
  });
});

describe("codici del lettore dei file", () => {
  test("EmptyFile, MalformedCsv, InvalidHeader tradotti; col tab InvalidHeader diventa il messaggio sul separatore", () => {
    expect(calendarErrorMessage({ status: 400, code: "EmptyFile", message: "EmptyFile" })).toBe("Il file è vuoto.");
    expect(calendarErrorMessage({ status: 400, code: "MalformedCsv", message: "MalformedCsv" })).toBe("Il CSV non è leggibile: controlla virgolette e separatori.");
    expect(calendarErrorMessage({ status: 400, code: "InvalidHeader" })).toBe("Intestazioni non riconosciute: usa il modello.");
    expect(calendarErrorMessage({ status: 400, code: "InvalidHeader" }, { tabSeparated: true })).toBe(DELIMITER_MESSAGE);
  });

  test("tutti i codici del lettore hanno un messaggio, mai il codice grezzo", () => {
    ["ColumnCountMismatch", "InvalidDelimiter", "InvalidParsingOptions", "RecordTooLong", "CellTooLong", "TooManyColumns", "TooManyRows",
      "MalformedWorkbook", "UnsupportedWorkbook", "HostileWorkbook", "ZipLimitExceeded", "XmlLimitExceeded", "TooManySharedStrings",
      "FormulaNotAllowed", "ExternalReferenceNotAllowed", "WorksheetSelectionRequired", "UnsupportedCellType", "AmbiguousCalendarCell",
      "DelimiterNotDetected", "AmbiguousDelimiter", "InvalidEncoding", "UnsupportedEncoding", "UnsupportedFormat", "FileTooLarge"]
      .forEach(code => {
        const text = calendarErrorMessage({ status: 400, code, message: code });
        expect(text).not.toContain(code);
        expect(text).not.toContain("Il server ha rifiutato");
      });
  });

  test("codice sconosciuto: testo generico con il codice tra parentesi, mai il codice da solo", () => {
    expect(calendarErrorMessage({ status: 400, code: "NuovoCodice", message: "NuovoCodice" })).toBe("Il server ha rifiutato l'operazione. (NuovoCodice)");
  });
});

describe("messaggi di riga per codice", () => {
  test("righe rifiutate: un messaggio per ciascun codice, non il testo generico del server", () => {
    const generic = "Riferimenti o intervallo non validi.";
    const row = (code) => issueMessage({ field: "Row", code, message: generic });
    expect(row("calendar.row-outside-month")).toBe("Data fuori dal mese selezionato");
    expect(row("calendar.teacher-unavailable")).toBe("Docente non trovato: usa l'ID del docente");
    expect(row("calendar.module-unavailable")).toBe("Modulo non trovato nel piano formativo della classe");
    expect(row("calendar.ambiguous-module")).toBe("Più moduli con questo nome");
    expect(row("calendar.reason-required")).toMatch(/non è il titolare.*«Sostituisci docente»/);
    expect(row("calendar.class-unavailable")).toBe("Classe non trovata");
  });

  test("testo che inizia con = + - @: spiegazione della protezione dalle formule", () => {
    expect(issueMessage({ field: "Description", code: "InvalidValue" })).toBe("Descrizione: il testo non può iniziare con = + - @ (protezione dalle formule di Excel)");
    expect(issueMessage({ field: "Room", code: "InvalidValue" })).toBe("Aula: il testo non può iniziare con = + - @ (protezione dalle formule di Excel)");
    expect(issueMessage({ field: "Date", code: "InvalidValue" })).toBe("Data: valore non valido"); // date e ore restano generiche
  });

  test("issueParts separa il codice sconosciuto (da mostrare piccolo)", () => {
    expect(issueParts({ field: "Row", code: "calendar.nuovo" })).toEqual({ text: "Valore non accettato", code: "calendar.nuovo" });
    expect(issueParts({ field: "Row", code: "calendar.row-outside-month" })).toEqual({ text: "Data fuori dal mese selezionato", code: null });
  });
});

describe("controlli del CSV prima dell'invio", () => {
  const H = "Data;Ora inizio;Ora fine;Classe;Modulo;Docente;Aula;Descrizione";
  test("file vuoto o con la sola intestazione (anche con righe ;;; di Excel)", () => {
    expect(precheckCalendarCsv("")).toMatchObject({ ok: false, message: "Il file è vuoto." });
    expect(precheckCalendarCsv("﻿\r\n  \r\n")).toMatchObject({ ok: false, message: "Il file è vuoto." });
    expect(precheckCalendarCsv(`${H}\r\n`)).toMatchObject({ ok: false, message: NO_LESSONS_MESSAGE });
    expect(precheckCalendarCsv(`﻿${H}\r\n;;;;;;;\r\n\r\n`)).toMatchObject({ ok: false, message: "Il file non contiene lezioni." });
  });

  test("intestazioni: alias del backend senza distinzione di maiuscole, con BOM, ; oppure ,", () => {
    expect(precheckCalendarCsv(`﻿${H}\n2026-12-01;09:00;11:00;1A;M1;t1;;`)).toMatchObject({ ok: true, unknown: [] });
    expect(precheckCalendarCsv("DATE,startsat,ENDSAT,class,Module,TEACHER\n2026-12-01,09:00,11:00,1A,M1,t1")).toMatchObject({ ok: true });
    expect(precheckCalendarCsv("OraInizio;OraFine;Data;Classe;Modulo;Docente;Note\nx;x;x;x;x;x;x")).toMatchObject({ ok: true, unknown: ["Note"] });
  });

  test("colonne obbligatorie mancanti: elenco di mancanti e sconosciute, non si invia", () => {
    const r = precheckCalendarCsv("Giorno;Ora inizio;Ora fine;Classe;Materia;Docente\n2026-12-01;09:00;11:00;1A;M1;t1");
    expect(r.ok).toBe(false);
    expect(r.missing).toEqual(["Date", "Module"]);
    expect(r.message).toBe("Mancano le colonne obbligatorie: Data, Modulo. Colonne non riconosciute: Giorno, Materia. Usa le intestazioni del modello.");
  });

  test("file separato dal tab", () => {
    expect(precheckCalendarCsv("Data\tOra inizio\tOra fine\tClasse\tModulo\tDocente\nx\tx\tx\tx\tx\tx")).toMatchObject({ ok: false, tabSeparated: true, message: DELIMITER_MESSAGE });
  });

  test("lettura del file: File del browser; se non si legge, null (decide il backend)", async () => {
    expect(await readFileText({ name: "a.csv", file: { text: async () => "abc" } })).toBe("abc");
    expect(await readFileText({ name: "a.csv", file: { text: async () => { throw new Error("no"); } } })).toBeNull();
    expect(await readFileText({ name: "a.csv" })).toBeNull();
  });
});

test("pulsante di conferma: 0 senza numeri, 1 al singolare, N al plurale", () => {
  expect(confirmLabel(0)).toBe("Nessuna lezione da creare");
  expect(confirmLabel(undefined)).toBe("Nessuna lezione da creare");
  expect(confirmLabel(1)).toBe("Crea 1 lezione in bozza");
  expect(confirmLabel(6)).toBe("Crea 6 lezioni in bozza");
});

test("avvisi del monte ore: una sola mappa per anteprima e sezione Monte ore", () => {
  expect(HOUR_WARNING_LABELS).toBe(HOURS_WARNING_LABELS);
  expect(issueMessage({ code: "calendar.under-scheduled" })).toBe(HOURS_WARNING_LABELS["calendar.under-scheduled"]);
  expect(issueMessage({ code: "calendar.over-scheduled" })).toBe(HOURS_WARNING_LABELS["calendar.over-scheduled"]);
  expect(HOURS_WARNING_LABELS["calendar.under-scheduled"]).toBe("Ore sotto il previsto");
});
