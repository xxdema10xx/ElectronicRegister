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
jest.mock("../../src/api/lessonService");
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
  expect((await screen.findAllByText("Configura il calendario")).length).toBe(2); // titolo e pulsante
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
  fireEvent.press(screen.getByText("Crea 2 lezioni in bozza"));
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
  expect(screen.getByText("Ore oltre il previsto: 11h su 10h (+1h)")).toBeTruthy();
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
