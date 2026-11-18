// Calendario (admin): anteprima del file, conferma delle lezioni in bozza, pubblicazione del mese.
import * as DocumentPicker from "expo-document-picker";
import { act, fireEvent, screen, waitFor } from "@testing-library/react-native";
import CalendarImportScreen from "../../src/screens/CalendarImportScreen";
import * as cal from "../../src/api/calendarImportService";
import * as lessonService from "../../src/api/lessonService";
import * as teacherService from "../../src/api/teacherService";
import { flush, pressDialog, renderAs } from "../helpers/render";

jest.mock("../../src/api/calendarImportService", () => ({
  ...jest.requireActual("../../src/api/calendarImportService"),
  getCalendarSettings: jest.fn(), previewImport: jest.fn(), confirmImport: jest.fn(), getMonthStatus: jest.fn(), publishPeriod: jest.fn(),
}));
jest.mock("../../src/api/lessonService", () => ({ ...jest.requireActual("../../src/api/lessonService"), getClassesForPicker: jest.fn() }));
jest.mock("../../src/api/calendarService", () => ({ ...jest.requireActual("../../src/api/calendarService"), saveCalendarSettings: jest.fn() }));
const calendarService = require("../../src/api/calendarService");
jest.mock("../../src/api/teacherService");

const file = { name: "orario.xlsx", uri: "file:///orario.xlsx", size: 1000 };

beforeEach(() => {
  cal.getCalendarSettings.mockResolvedValue({ timeZone: "Europe/Rome" });
  lessonService.getClassesForPicker.mockResolvedValue([{ id: "c1", name: "SW-25A" }, { id: "c2", name: "SW-25B" }]);
  teacherService.getTeachers.mockResolvedValue([{ id: "t-guid", firstName: "Anna", lastName: "Neri" }]);
  cal.getMonthStatus.mockResolvedValue({ period: { revision: 3, approval: null }, drafts: 12, published: 0 });
  DocumentPicker.getDocumentAsync.mockResolvedValue({ canceled: false, assets: [file] });
});

async function chooseClassAndFile() {
  fireEvent.press(await screen.findByText("SW-25A"));
  await act(async () => { fireEvent.press(screen.getByText("Scegli file")); });
}

test("calendario non configurato: modulo di configurazione, prima volta con expectedRevision -1", async () => {
  cal.getCalendarSettings.mockResolvedValueOnce(null).mockResolvedValue({ timeZoneId: "Europe/Rome", approvalLeadDays: 7, revision: 0 });
  renderAs("staff", <CalendarImportScreen />);
  // Primo render del file (caricamento dei moduli): sulle macchine lente supera l'attesa predefinita di 1 s
  expect((await screen.findAllByText("Configura il calendario", {}, { timeout: 5000 })).length).toBe(2); // titolo e pulsante
  expect(screen.getByText(/Il fuso orario non si può più cambiare/)).toBeTruthy();
  expect(screen.getByLabelText("Fuso orario (es. Europe/Rome)").props.value).toBe("Europe/Rome");
  // il fuso proposto va confermato
  fireEvent.changeText(screen.getByLabelText("Giorni di anticipo per l'approvazione del mese (0–366)"), "7");
  fireEvent.changeText(screen.getByLabelText("Motivo (obbligatorio, resta nello storico)"), "Prima configurazione");
  await act(async () => { fireEvent.press(screen.getAllByText("Configura il calendario").at(-1)); });
  expect(calendarService.saveCalendarSettings).not.toHaveBeenCalled();
  expect(screen.getByText("Conferma il fuso orario.")).toBeTruthy();
  fireEvent.press(screen.getByText("Confermo il fuso orario Europe/Rome"));
  await act(async () => { fireEvent.press(screen.getAllByText("Configura il calendario").at(-1)); });
  expect(calendarService.saveCalendarSettings).toHaveBeenCalledWith(
    { eventId: expect.any(String), expectedRevision: -1, timeZoneId: "Europe/Rome", approvalLeadDays: 7, reason: "Prima configurazione" }, "tok");
  await flush();
  expect(cal.getCalendarSettings).toHaveBeenCalledTimes(2); // riletto dopo il salvataggio
  expect(screen.queryByText("Configura il calendario")).toBeNull();
});

test("calendario non raggiungibile (403): messaggio chiaro, niente modulo", async () => {
  cal.getCalendarSettings.mockRejectedValue(Object.assign(new Error("x"), { status: 403 }));
  renderAs("staff", <CalendarImportScreen />);
  expect(await screen.findByText("Calendario non disponibile")).toBeTruthy();
  expect(screen.getByText(/Non hai i permessi/)).toBeTruthy();
});

test("classi secondo il ruolo: l'admin dal catalogo, lo staff dalle classi-materie", async () => {
  renderAs("admin", <CalendarImportScreen />);
  expect(await screen.findByText("SW-25A", {}, { timeout: 5000 })).toBeTruthy();
  expect(lessonService.getClassesForPicker).toHaveBeenLastCalledWith("tok", "admin");
  renderAs("staff", <CalendarImportScreen />);
  await flush();
  expect(lessonService.getClassesForPicker).toHaveBeenLastCalledWith("tok", "staff");
});

test("403 sull'elenco delle classi: errore chiaro al posto di \"Nessuna classe disponibile\"", async () => {
  lessonService.getClassesForPicker.mockRejectedValue(Object.assign(new Error("Non hai i permessi"), { status: 403 }));
  renderAs("staff", <CalendarImportScreen />);
  expect(await screen.findByText(lessonService.CLASS_LIST_FORBIDDEN_MESSAGE, {}, { timeout: 5000 })).toBeTruthy();
  expect(screen.queryByText("Nessuna classe disponibile.")).toBeNull();
  expect(screen.getByRole("button", { name: "Genera anteprima" }).props.accessibilityState.disabled).toBe(true);
});

test("ID dei docenti visibili a richiesta", async () => {
  renderAs("admin", <CalendarImportScreen />);
  await flush();
  fireEvent.press(screen.getByText("Mostra ID dei docenti"));
  expect(screen.getByText("t-guid")).toBeTruthy();
  expect(screen.getByText("Neri Anna")).toBeTruthy();
});

test("file non supportato o troppo grande", async () => {
  renderAs("admin", <CalendarImportScreen />);
  await flush();
  DocumentPicker.getDocumentAsync.mockResolvedValueOnce({ canceled: false, assets: [{ name: "orario.pdf" }] });
  await act(async () => { fireEvent.press(screen.getByText("Scegli file")); });
  expect(await screen.findByText("Scegli un file .xlsx o .csv.")).toBeTruthy();
  await pressDialog("OK");
  // Nginx in produzione accetta 1 MB (EXPO_PUBLIC_CALENDAR_MAX_UPLOAD_MB): il client avvisa prima dell'invio
  expect(screen.getByText(/al massimo 1 MB e 2000 righe/)).toBeTruthy();
  DocumentPicker.getDocumentAsync.mockResolvedValueOnce({ canceled: false, assets: [{ name: "x.csv", size: 2 * 1024 * 1024 }] });
  await act(async () => { fireEvent.press(screen.getByText("Scegli file")); });
  expect(await screen.findByText("Il file può essere al massimo di 1 MB. File troppo grande per il server: dividilo per classe o salvalo in CSV.")).toBeTruthy();
  await pressDialog("OK");
  // su web la dimensione può arrivare solo dal File
  DocumentPicker.getDocumentAsync.mockResolvedValueOnce({ canceled: false, assets: [{ name: "y.csv", file: { size: 1024 * 1024 + 1 } }] });
  await act(async () => { fireEvent.press(screen.getByText("Scegli file")); });
  expect(await screen.findByText("Il file può essere al massimo di 1 MB. File troppo grande per il server: dividilo per classe o salvalo in CSV.")).toBeTruthy();
  await pressDialog("OK");
  DocumentPicker.getDocumentAsync.mockResolvedValueOnce({ canceled: false, assets: [{ name: "ok.csv", size: 1024 * 1024 }] });
  await act(async () => { fireEvent.press(screen.getByText("Scegli file")); });
  expect(screen.queryByText("File troppo grande")).toBeNull();
});

test("anteprima: serve la motivazione", async () => {
  renderAs("admin", <CalendarImportScreen />);
  await chooseClassAndFile();
  expect(screen.getByText("orario.xlsx")).toBeTruthy();
  fireEvent.press(screen.getByText("Genera anteprima"));
  expect(await screen.findByText(/Indica una motivazione/)).toBeTruthy();
  expect(cal.previewImport).not.toHaveBeenCalled();
});

test("anteprima con errori: righe da correggere, conferma bloccata", async () => {
  cal.previewImport.mockResolvedValue({
    id: "p1", expiresAtUtc: new Date().toISOString(), canConfirm: false, columns: ["Data"], replaced: 0,
    rows: [
      { rowNumber: 2, issues: [{ code: "InvalidValue", field: "Date" }], conflicts: [] },
      { rowNumber: 3, issues: [], conflicts: [{ kind: 2 }], lesson: { date: "2026-12-01", startsAt: "09:00:00", endsAt: "11:00:00" }, className: "SW-25A" },
    ],
  });
  renderAs("admin", <CalendarImportScreen />);
  await chooseClassAndFile();
  fireEvent.changeText(screen.getByPlaceholderText(/Es\. Orario di/), "Orario di dicembre");
  await act(async () => { fireEvent.press(screen.getByText("Genera anteprima")); });
  expect(cal.previewImport).toHaveBeenCalledWith(expect.objectContaining({ file, classIds: ["c1"], mode: "AppendDraft", reason: "Orario di dicembre" }), "tok");
  ["Righe da correggere nel file", "Riga 2", "✕ Data: valore non valido", "✕ Il docente ha già un'altra lezione", "01/12/2026 09:00–11:00 · SW-25A"]
    .forEach(t => expect(screen.getByText(t)).toBeTruthy());
  expect(screen.getByText(/Correggi le righe con errori/)).toBeTruthy();
  // nessuna riga pronta: il pulsante non mostra numeri
  fireEvent.press(screen.getByText("Nessuna lezione da creare"));
  expect(cal.confirmImport).not.toHaveBeenCalled();
});

test("anteprima con avvisi: conferma solo dopo la spunta; poi lezioni in bozza", async () => {
  const preview = {
    id: "p1", revision: 1, digest: "d", expiresAtUtc: new Date().toISOString(), canConfirm: true, replaced: 0, columns: [],
    rows: [{ rowNumber: 2, issues: [{ code: "IgnoredColumn", field: "Note", warning: true }] }, { rowNumber: 3, issues: [{ code: "IgnoredColumn", field: "Note", warning: true }] }],
    hours: [{ classId: "c1", moduleId: "mod1", plannedMinutes: 600, publishedMinutes: 0, draftMinutesAfterImport: 660 }],
  };
  cal.previewImport.mockResolvedValue(preview);
  cal.confirmImport.mockResolvedValue({ lessonIds: ["l1", "l2"] });
  renderAs("admin", <CalendarImportScreen />);
  await chooseClassAndFile();
  fireEvent.changeText(screen.getByPlaceholderText(/Es\. Orario di/), "Orario");
  await act(async () => { fireEvent.press(screen.getByText("Genera anteprima")); });
  expect(screen.getByText("⚠ Colonna non riconosciuta, verrà ignorata")).toBeTruthy(); // avviso comune, una volta sola
  expect(screen.getByText("Ore oltre il previsto: 11 h su 10 h (+1 h)")).toBeTruthy();
  fireEvent.press(screen.getByText("Crea 2 lezioni in bozza"));
  expect(cal.confirmImport).not.toHaveBeenCalled();
  fireEvent.press(screen.getByText("Ho letto gli avvisi e voglio procedere"));
  await act(async () => { fireEvent.press(screen.getByText("Crea 2 lezioni in bozza")); });
  expect(cal.confirmImport).toHaveBeenCalledWith(preview, { acknowledgeWarnings: true, reason: "Orario" }, "tok");
  expect(await screen.findByText(/2 lezioni create in bozza/)).toBeTruthy();
});

test("pubblicazione del mese per classe, con conferma", async () => {
  cal.publishPeriod.mockResolvedValue({});
  renderAs("admin", <CalendarImportScreen />);
  fireEvent.press(await screen.findByText("SW-25A"));
  expect(await screen.findByText(/12 in bozza · 0 pubblicate/)).toBeTruthy();
  fireEvent.press(screen.getByText("Pubblica"));
  expect(await screen.findByText(/Indica una motivazione nel campo/)).toBeTruthy();
  await pressDialog("OK");
  fireEvent.changeText(screen.getByPlaceholderText(/Es\. Orario di/), "Pubblicazione");
  fireEvent.press(screen.getByText("Pubblica"));
  expect(await screen.findByText(/Le 12 lezioni in bozza di SW-25A/)).toBeTruthy();
  await pressDialog("Pubblica");
  await waitFor(() => expect(cal.publishPeriod).toHaveBeenCalledWith("c1", expect.any(Number), expect.any(Number), 3, "Pubblicazione", "tok"));
  expect(await screen.findByText("Calendario pubblicato")).toBeTruthy();
});

test("stato del mese non leggibile: errore sulla riga della classe", async () => {
  cal.getMonthStatus.mockRejectedValue({ status: 404 });
  renderAs("admin", <CalendarImportScreen />);
  fireEvent.press(await screen.findByText("SW-25B"));
  expect(await screen.findByText(/non è attivo per questo istituto/)).toBeTruthy();
});

test("anteprima: 413 o connessione chiusa da Nginx → file troppo grande per il server", async () => {
  renderAs("admin", <CalendarImportScreen />);
  await chooseClassAndFile();
  fireEvent.changeText(screen.getByLabelText("Motivazione (obbligatoria, resta nello storico)"), "Orario");
  cal.previewImport.mockRejectedValueOnce(Object.assign(new Error("Payload Too Large"), { status: 413 }));
  await act(async () => { fireEvent.press(screen.getByText("Genera anteprima")); });
  expect(await screen.findByText("File troppo grande per il server: dividilo per classe o salvalo in CSV.")).toBeTruthy();
  await pressDialog("OK");
  cal.previewImport.mockRejectedValueOnce(Object.assign(new Error("Impossibile contattare il server."), { status: undefined }));
  await act(async () => { fireEvent.press(screen.getByText("Genera anteprima")); });
  expect(await screen.findByText(/File troppo grande per il server.*controlla la connessione/)).toBeTruthy();
});

test("anteprima: classe senza piano formativo pubblicato", async () => {
  renderAs("admin", <CalendarImportScreen />);
  await chooseClassAndFile();
  fireEvent.changeText(screen.getByLabelText("Motivazione (obbligatoria, resta nello storico)"), "Orario");
  cal.previewImport.mockRejectedValueOnce(Object.assign(new Error("x"), { status: 409, code: "calendar.teaching-configuration-required" }));
  await act(async () => { fireEvent.press(screen.getByText("Genera anteprima")); });
  expect(await screen.findByText(/non ha un piano formativo pubblicato: chiedi all'amministrazione/)).toBeTruthy();
});

test("riga con docente non titolare: messaggio comprensibile", async () => {
  cal.previewImport.mockResolvedValue({
    id: "p1", revision: 0, digest: "d", expiresAtUtc: "2026-11-20T10:00:00Z", canConfirm: false, replaced: 0, totalRows: 1, validRows: 0, invalidRows: 1,
    rows: [{ rowNumber: 2, lesson: null, teacherReference: "t9", issues: [{ code: "calendar.reason-required", field: "Teacher", warning: false }], conflicts: [] }], hours: [],
  });
  renderAs("admin", <CalendarImportScreen />);
  await chooseClassAndFile();
  fireEvent.changeText(screen.getByLabelText("Motivazione (obbligatoria, resta nello storico)"), "Orario");
  await act(async () => { fireEvent.press(screen.getByText("Genera anteprima")); });
  expect(await screen.findByText(/Il docente non è il titolare di questa materia nella classe: importa la lezione con il titolare e poi usa «Sostituisci docente»/)).toBeTruthy();
  expect(screen.queryByText(/Indica una motivazione/)).toBeNull();
});

// ── Correzioni dopo i test E2E sullo staging ─────────────────────────────────────────────
const csvAsset = (name, text) => ({ name, uri: `file:///${name}`, size: text.length, file: { size: text.length, text: async () => text } });
const VALID_CSV = "﻿Data;Ora inizio;Ora fine;Classe;Modulo;Docente\r\n2026-12-01;09:00;11:00;SW-25A;M1;t-guid\r\n";
const isDisabled = (label) => screen.getByText(label).parent?.parent?.props?.accessibilityState?.disabled
  ?? screen.getByRole("button", { name: label }).props.accessibilityState.disabled;

async function fillReason(text = "Orario") {
  fireEvent.changeText(screen.getByLabelText("Motivazione (obbligatoria, resta nello storico)"), text);
}

test("file rifiutato (.txt dopo un .csv valido): nessun file selezionato e anteprima disattivata", async () => {
  DocumentPicker.getDocumentAsync.mockResolvedValueOnce({ canceled: false, assets: [csvAsset("orario.csv", VALID_CSV)] });
  renderAs("admin", <CalendarImportScreen />);
  fireEvent.press(await screen.findByText("SW-25A"));
  await act(async () => { fireEvent.press(screen.getByText("Scegli file")); });
  expect(screen.getByText("orario.csv")).toBeTruthy();
  expect(screen.getByRole("button", { name: "Genera anteprima" }).props.accessibilityState.disabled).toBe(false);
  DocumentPicker.getDocumentAsync.mockResolvedValueOnce({ canceled: false, assets: [{ name: "note.txt", uri: "file:///note.txt", size: 10 }] });
  await act(async () => { fireEvent.press(screen.getByText("Cambia file")); });
  expect(await screen.findByText("Scegli un file .xlsx o .csv.")).toBeTruthy();
  await pressDialog("OK");
  expect(screen.queryByText("orario.csv")).toBeNull();
  expect(screen.getByText("Nessun file selezionato")).toBeTruthy();
  expect(screen.getByRole("button", { name: "Genera anteprima" }).props.accessibilityState.disabled).toBe(true);
});

test("file troppo grande dopo uno valido: anche l'anteprima già generata si azzera", async () => {
  cal.previewImport.mockResolvedValue({ id: "p1", revision: 0, digest: "d", expiresAtUtc: new Date().toISOString(), canConfirm: true, replaced: 0, rows: [{ rowNumber: 2, issues: [] }], hours: [] });
  renderAs("admin", <CalendarImportScreen />);
  await chooseClassAndFile();
  await fillReason();
  await act(async () => { fireEvent.press(screen.getByText("Genera anteprima")); });
  expect(screen.getByText("3. Anteprima")).toBeTruthy();
  DocumentPicker.getDocumentAsync.mockResolvedValueOnce({ canceled: false, assets: [{ name: "grande.csv", size: 5 * 1024 * 1024 }] });
  await act(async () => { fireEvent.press(screen.getByText("Cambia file")); });
  await pressDialog("OK");
  expect(screen.queryByText("3. Anteprima")).toBeNull();
  expect(screen.getByText("Nessun file selezionato")).toBeTruthy();
});

test("CSV con la sola intestazione: avviso sul client, nessuna anteprima consumata", async () => {
  DocumentPicker.getDocumentAsync.mockResolvedValue({ canceled: false, assets: [csvAsset("vuoto.csv", "﻿Data;Ora inizio;Ora fine;Classe;Modulo;Docente\r\n;;;;;\r\n")] });
  renderAs("admin", <CalendarImportScreen />);
  await chooseClassAndFile();
  await fillReason();
  await act(async () => { fireEvent.press(screen.getByText("Genera anteprima")); });
  expect(await screen.findByText("Il file non contiene lezioni.")).toBeTruthy();
  expect(cal.previewImport).not.toHaveBeenCalled();
});

test("CSV con intestazioni sbagliate: colonne mancanti e sconosciute elencate, nessun invio", async () => {
  DocumentPicker.getDocumentAsync.mockResolvedValue({ canceled: false, assets: [csvAsset("x.csv", "Giorno;Ora inizio;Ora fine;Classe;Modulo;Docente\n2026-12-01;09:00;10:00;A;M1;t")] });
  renderAs("admin", <CalendarImportScreen />);
  await chooseClassAndFile();
  await fillReason();
  await act(async () => { fireEvent.press(screen.getByText("Genera anteprima")); });
  expect(await screen.findByText(/Mancano le colonne obbligatorie: Data\. Colonne non riconosciute: Giorno\./)).toBeTruthy();
  expect(cal.previewImport).not.toHaveBeenCalled();
});

test("CSV valido: il controllo lascia passare l'invio; EmptyFile dal server è tradotto", async () => {
  DocumentPicker.getDocumentAsync.mockResolvedValue({ canceled: false, assets: [csvAsset("ok.csv", VALID_CSV)] });
  cal.previewImport.mockRejectedValueOnce(Object.assign(new Error("EmptyFile"), { status: 400, code: "EmptyFile" }));
  renderAs("admin", <CalendarImportScreen />);
  await chooseClassAndFile();
  await fillReason();
  await act(async () => { fireEvent.press(screen.getByText("Genera anteprima")); });
  expect(cal.previewImport).toHaveBeenCalledTimes(1);
  expect(await screen.findByText("Il file è vuoto.")).toBeTruthy();
  expect(screen.queryByText("EmptyFile")).toBeNull();
});

test("suggerimento della motivazione con il mese scelto, non un mese fisso", async () => {
  renderAs("admin", <CalendarImportScreen />);
  await chooseClassAndFile();
  const selected = screen.getByText(/^(Gennaio|Febbraio|Marzo|Aprile|Maggio|Giugno|Luglio|Agosto|Settembre|Ottobre|Novembre|Dicembre) \d{4}$/).props.children;
  const expected = `Orario di ${selected.toLowerCase()}`;
  expect(screen.getByPlaceholderText(`Es. ${expected}`)).toBeTruthy();
  fireEvent.press(screen.getByText("Genera anteprima"));
  expect(await screen.findByText(`Indica una motivazione (es. "${expected}").`)).toBeTruthy();
});

test("una riga pronta: «Crea 1 lezione»; avviso sotto il monte ore spiegato accanto alla spunta", async () => {
  cal.previewImport.mockResolvedValue({
    id: "p1", revision: 0, digest: "d", expiresAtUtc: new Date().toISOString(), canConfirm: true, replaced: 0, totalRows: 1, validRows: 1, invalidRows: 0,
    rows: [{ rowNumber: 2, issues: [], lesson: { moduleId: "m1" }, moduleName: "Basi di dati" }],
    hours: [{ classId: "c1", moduleId: "m1", plannedMinutes: 4800, publishedMinutes: 0, draftMinutesAfterImport: 120, warning: "calendar.under-scheduled" }],
  });
  renderAs("admin", <CalendarImportScreen />);
  await chooseClassAndFile();
  await fillReason();
  await act(async () => { fireEvent.press(screen.getByText("Genera anteprima")); });
  expect(screen.getByText("Ore sotto il previsto: 2 h su 80 h (-78 h)")).toBeTruthy();
  expect(screen.getByText(/In un import di un solo mese è normale che i moduli risultino sotto il monte ore/)).toBeTruthy();
  expect(screen.getByText("Crea 1 lezione in bozza")).toBeTruthy();
  expect(screen.getByRole("button", { name: "Crea 1 lezione in bozza" }).props.accessibilityState.disabled).toBe(true); // serve la spunta
  fireEvent.press(screen.getByText("Ho letto gli avvisi e voglio procedere"));
  expect(screen.getByRole("button", { name: "Crea 1 lezione in bozza" }).props.accessibilityState.disabled).toBe(false);
});

test("righe rifiutate: «Riga N» e messaggio per codice; il codice sconosciuto compare piccolo, mai da solo", async () => {
  const generic = "Riferimenti o intervallo non validi.";
  cal.previewImport.mockResolvedValue({
    id: "p1", revision: 0, digest: "d", expiresAtUtc: new Date().toISOString(), canConfirm: false, replaced: 0, totalRows: 4, validRows: 0, invalidRows: 4,
    rows: [
      { rowNumber: 2, issues: [{ field: "Row", code: "calendar.row-outside-month", message: generic }], conflicts: [], classReference: "SW-25A", moduleReference: "M1", teacherReference: "t-guid" },
      { rowNumber: 3, issues: [{ field: "Row", code: "calendar.module-unavailable", message: generic }], conflicts: [], moduleReference: "M9" },
      { rowNumber: 4, issues: [{ field: "Description", code: "InvalidValue", message: "Valore non valido." }], conflicts: [] },
      { rowNumber: 5, issues: [{ field: "Row", code: "calendar.nuovo-codice", message: generic }], conflicts: [] },
    ], hours: [],
  });
  renderAs("admin", <CalendarImportScreen />);
  await chooseClassAndFile();
  await fillReason();
  await act(async () => { fireEvent.press(screen.getByText("Genera anteprima")); });
  ["Riga 2", "Riga 3", "Riga 4", "Riga 5", "SW-25A · M1 · t-guid", "M9"].forEach(t => expect(screen.getByText(t)).toBeTruthy());
  expect(screen.getByText(/Data fuori dal mese selezionato/)).toBeTruthy();
  expect(screen.getByText(/Modulo non trovato nel piano formativo della classe/)).toBeTruthy();
  expect(screen.getByText(/Descrizione: il testo non può iniziare con = \+ - @ \(protezione dalle formule di Excel\)/)).toBeTruthy();
  expect(screen.getByText(/Valore non accettato/)).toBeTruthy();
  expect(screen.getByText(" (calendar.nuovo-codice)").props.style.fontSize).toBe(11);
  expect(screen.queryByText(new RegExp(generic.replace(".", "\\.")))).toBeNull();
  // nessuna data inventata per le righe senza lezione risolta
  expect(screen.queryByText(/\d{2}\/\d{2}\/\d{4}/)).toBeNull();
  expect(screen.getByText("Nessuna lezione da creare")).toBeTruthy();
});
