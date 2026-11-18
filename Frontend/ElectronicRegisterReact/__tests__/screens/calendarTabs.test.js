// Pagina Calendario: i campi dell'import non si svuotano cambiando scheda (Lezioni/Importa/Monte ore/Impostazioni).
// Caso segnalato sullo staging: dopo un giro tra le schede mese, classi e motivazione erano tornati vuoti,
// perché la vista Importa veniva smontata. Regole:
//  - cambiare scheda non azzera mese, classi, motivazione, file (né l'anteprima);
//  - cambiare mese o classi azzera solo l'anteprima;
//  - un nuovo file azzera anteprima e conferma degli avvisi.
import * as DocumentPicker from "expo-document-picker";
import { act, fireEvent, screen } from "@testing-library/react-native";
import CalendarScreen from "../../src/screens/CalendarScreen";
import * as cal from "../../src/api/calendarImportService";
import * as lessonService from "../../src/api/lessonService";
import * as teacherService from "../../src/api/teacherService";
import * as calendarService from "../../src/api/calendarService";
import { flush, renderAs } from "../helpers/render";

jest.mock("../../src/api/calendarImportService", () => ({
  ...jest.requireActual("../../src/api/calendarImportService"),
  getCalendarSettings: jest.fn(), previewImport: jest.fn(), confirmImport: jest.fn(), getMonthStatus: jest.fn(), publishPeriod: jest.fn(),
}));
jest.mock("../../src/api/lessonService", () => ({ ...jest.requireActual("../../src/api/lessonService"), getSchedule: jest.fn(), getClassesForPicker: jest.fn() }));
jest.mock("../../src/api/calendarService", () => ({ ...jest.requireActual("../../src/api/calendarService"), getClassHours: jest.fn(), getPlanNames: jest.fn() }));
jest.mock("../../src/api/teacherService");

const xlsx = (name) => ({ name, uri: `file:///${name}`, size: 1000 });
const preview = {
  id: "p1", revision: 0, digest: "d", expiresAtUtc: new Date().toISOString(), canConfirm: true, replaced: 0, columns: [],
  rows: [{ rowNumber: 2, issues: [{ code: "IgnoredColumn", field: "Note", warning: true }] }], hours: [],
};

beforeEach(() => {
  cal.getCalendarSettings.mockResolvedValue({ timeZoneId: "Europe/Rome", approvalLeadDays: 7, revision: 0 });
  cal.getMonthStatus.mockResolvedValue({ period: null, drafts: 0, published: 0 });
  cal.previewImport.mockResolvedValue(preview);
  lessonService.getClassesForPicker.mockResolvedValue([{ id: "c1", name: "SW-25A" }, { id: "c2", name: "SW-25B" }]);
  lessonService.getSchedule.mockResolvedValue([]);
  teacherService.getTeachers.mockResolvedValue([]);
  calendarService.getClassHours.mockResolvedValue({ modules: [], units: [], months: [] });
  calendarService.getPlanNames.mockResolvedValue({ modules: {}, units: {}, order: {} });
  DocumentPicker.getDocumentAsync.mockResolvedValue({ canceled: false, assets: [xlsx("orario.xlsx")] });
});

const reasonInput = () => screen.getByLabelText("Motivazione (obbligatoria, resta nello storico)");
const checked = (label) => screen.getByRole("checkbox", { name: label }).props.accessibilityState.checked;

async function openImportAndFill() {
  renderAs("admin", <CalendarScreen role="admin" />);
  await flush();
  fireEvent.press(screen.getByText("Importa"));
  await flush();
  fireEvent.press(screen.getByText("SW-25B"));
  fireEvent.changeText(reasonInput(), "Orario di prova");
  await act(async () => { fireEvent.press(screen.getByText("Scegli file")); });
  await flush();
}

const TABS = ["Lezioni", "Importa", "Monte ore", "Impostazioni"];
async function goTo(tab) {
  fireEvent.press(screen.getAllByRole("tab")[TABS.indexOf(tab)]);
  await flush();
}

test("cambiare scheda (Lezioni, Monte ore, Impostazioni) non azzera classi, motivazione, file e anteprima", async () => {
  await openImportAndFill();
  await act(async () => { fireEvent.press(screen.getByText("Genera anteprima")); });
  expect(screen.getByText("3. Anteprima")).toBeTruthy();
  for (const tab of ["Lezioni", "Monte ore", "Impostazioni"]) {
    await goTo(tab);
    await goTo("Importa");
    expect(reasonInput().props.value).toBe("Orario di prova");
    expect(checked("SW-25B")).toBe(true);
    expect(checked("SW-25A")).toBe(false);
    expect(screen.getByText("orario.xlsx")).toBeTruthy();
    expect(screen.getByText("3. Anteprima")).toBeTruthy();
  }
  expect(cal.getCalendarSettings).toHaveBeenCalled();
});

test("cambiare classi azzera solo l'anteprima, non file e motivazione", async () => {
  await openImportAndFill();
  await act(async () => { fireEvent.press(screen.getByText("Genera anteprima")); });
  expect(screen.getByText("3. Anteprima")).toBeTruthy();
  fireEvent.press(screen.getByText("SW-25A"));
  await flush();
  expect(screen.queryByText("3. Anteprima")).toBeNull();
  expect(screen.getByText("orario.xlsx")).toBeTruthy();
  expect(reasonInput().props.value).toBe("Orario di prova");
});

test("cambiare mese azzera solo l'anteprima, non file, classi e motivazione", async () => {
  await openImportAndFill();
  await act(async () => { fireEvent.press(screen.getByText("Genera anteprima")); });
  const current = screen.getByText(/^(Gennaio|Febbraio|Marzo|Aprile|Maggio|Giugno|Luglio|Agosto|Settembre|Ottobre|Novembre|Dicembre) \d{4}$/).props.children;
  fireEvent.press(screen.getByText(current));
  const other = screen.getAllByText(/^(Gennaio|Febbraio|Marzo|Aprile|Maggio|Giugno|Luglio|Agosto|Settembre|Ottobre|Novembre|Dicembre) \d{4}$/).map(n => n.props.children).find(n => n !== current);
  fireEvent.press(screen.getAllByText(other).at(-1));
  await flush();
  expect(screen.queryByText("3. Anteprima")).toBeNull();
  expect(screen.getByText("orario.xlsx")).toBeTruthy();
  expect(checked("SW-25B")).toBe(true);
  expect(reasonInput().props.value).toBe("Orario di prova");
});

test("un nuovo file azzera anteprima e conferma degli avvisi", async () => {
  await openImportAndFill();
  await act(async () => { fireEvent.press(screen.getByText("Genera anteprima")); });
  fireEvent.press(screen.getByText("Ho letto gli avvisi e voglio procedere"));
  expect(checked("Ho letto gli avvisi e voglio procedere")).toBe(true);
  DocumentPicker.getDocumentAsync.mockResolvedValueOnce({ canceled: false, assets: [xlsx("nuovo.xlsx")] });
  await act(async () => { fireEvent.press(screen.getByText("Cambia file")); });
  expect(screen.queryByText("3. Anteprima")).toBeNull();
  expect(screen.getByText("nuovo.xlsx")).toBeTruthy();
  expect(reasonInput().props.value).toBe("Orario di prova");
  await act(async () => { fireEvent.press(screen.getByText("Genera anteprima")); });
  expect(checked("Ho letto gli avvisi e voglio procedere")).toBe(false);
});
