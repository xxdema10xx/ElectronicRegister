// Pagina Calendario di admin e staff: vista Lezioni (bozze e pubblicate, annullate a richiesta, comandi).
import { act, fireEvent, screen } from "@testing-library/react-native";
import CalendarScreen from "../../src/screens/CalendarScreen";
import * as lessonService from "../../src/api/lessonService";
import * as calendarService from "../../src/api/calendarService";
import * as teacherService from "../../src/api/teacherService";
import { flush, renderAs } from "../helpers/render";

jest.mock("../../src/api/lessonService", () => ({ ...jest.requireActual("../../src/api/lessonService"), getSchedule: jest.fn(), getClassesForPicker: jest.fn() }));
jest.mock("../../src/api/calendarService", () => ({
  ...jest.requireActual("../../src/api/calendarService"),
  getLesson: jest.fn(), getLessonHistory: jest.fn(), cancelLesson: jest.fn(), saveCalendarSettings: jest.fn(),
}));
jest.mock("../../src/api/teacherService");
jest.mock("../../src/screens/CalendarImportScreen", () => require("../helpers/placeholder")("CalendarImport"));

const detail = { id: "L1", classId: "c1", teacherId: "t1", originalTeacherId: "t1", date: "2026-11-19", startsAt: "09:00:00", endsAt: "11:00:00",
  publication: "Draft", status: "Scheduled", revision: 2, moduleName: "Reti", className: "SW-25A" };

beforeEach(() => {
  lessonService.getClassesForPicker.mockResolvedValue([{ id: "c1", name: "SW-25A" }, { id: "c2", name: "SW-25B" }]);
  lessonService.getSchedule.mockResolvedValue([{ id: "L1", date: "2026-11-19", day: 4, start: "09:00", end: "11:00", title: "Reti", teacherName: "Anna Neri", className: "", room: "", status: "Scheduled", publication: "Draft" }]);
  teacherService.getTeachers.mockResolvedValue([{ id: "t1", firstName: "Anna", lastName: "Neri" }]);
  calendarService.getLesson.mockResolvedValue(detail);
  calendarService.getLessonHistory.mockResolvedValue([]);
  calendarService.cancelLesson.mockResolvedValue({ revision: 3 });
});

test("lezioni della classe: bozze e pubblicate, Bozza visibile, annullate a richiesta", async () => {
  renderAs("admin", <CalendarScreen role="admin" />);
  await flush();
  expect(lessonService.getSchedule).toHaveBeenLastCalledWith(expect.objectContaining({ role: "admin", classId: "c1", publication: null, includeCancelled: false }), "tok");
  expect(screen.getByText("Bozza")).toBeTruthy();
  fireEvent.press(screen.getByText("Mostra annullate"));
  await flush();
  expect(lessonService.getSchedule).toHaveBeenLastCalledWith(expect.objectContaining({ includeCancelled: true }), "tok");
});

test("staff: dal dettaglio annulla e poi rilegge lezione e settimana", async () => {
  renderAs("staff", <CalendarScreen role="staff" />);
  await flush();
  const weekCalls = lessonService.getSchedule.mock.calls.length;
  await act(async () => { fireEvent.press(screen.getByLabelText(/Dettaglio lezione: Reti/)); });
  await flush();
  expect(screen.getByText("Storico modifiche")).toBeTruthy();
  await act(async () => { fireEvent.press(screen.getByLabelText("Annulla lezione")); });
  fireEvent.changeText(screen.getByLabelText("Motivo (obbligatorio, resta nello storico)"), "Sciopero");
  fireEvent.press(screen.getByText(/Confermo: la lezione verrà annullata/));
  await act(async () => { fireEvent.press(screen.getAllByText("Annulla lezione").at(-1)); });
  await flush();
  expect(calendarService.cancelLesson).toHaveBeenCalledWith("L1", expect.objectContaining({ expectedRevision: 2, reason: "Sciopero" }), "tok");
  expect(calendarService.getLesson).toHaveBeenCalledTimes(2);
  expect(lessonService.getSchedule.mock.calls.length).toBeGreaterThan(weekCalls);
});

test("classi del selettore secondo il ruolo: admin e staff", async () => {
  renderAs("admin", <CalendarScreen role="admin" />);
  await flush();
  expect(lessonService.getClassesForPicker).toHaveBeenLastCalledWith("tok", "admin");
  renderAs("staff", <CalendarScreen role="staff" />);
  await flush();
  expect(lessonService.getClassesForPicker).toHaveBeenLastCalledWith("tok", "staff");
  expect(screen.getAllByText("SW-25A").length).toBeGreaterThan(0);
});

test("403 sull'elenco delle classi: errore chiaro, non un elenco vuoto in silenzio", async () => {
  lessonService.getClassesForPicker.mockRejectedValue(Object.assign(new Error("Non hai i permessi"), { status: 403 }));
  renderAs("staff", <CalendarScreen role="staff" />);
  await flush();
  expect(screen.getByText(lessonService.CLASS_LIST_FORBIDDEN_MESSAGE)).toBeTruthy();
  fireEvent.press(screen.getByText("Monte ore"));
  await flush();
  expect(screen.getByText(lessonService.CLASS_LIST_FORBIDDEN_MESSAGE)).toBeTruthy();
});

test("vista Importa", async () => {
  renderAs("admin", <CalendarScreen role="admin" />);
  await flush();
  fireEvent.press(screen.getByText("Importa"));
  expect(screen.getByText("SCREEN CalendarImport")).toBeTruthy();
});

test("impostazioni: modifica con la revisione letta", async () => {
  const cal = require("../../src/api/calendarImportService");
  jest.spyOn(cal, "getCalendarSettings").mockResolvedValue({ timeZoneId: "Europe/Rome", approvalLeadDays: 5, revision: 3 });
  const save = calendarService.saveCalendarSettings.mockResolvedValue({ revision: 4 });
  renderAs("staff", <CalendarScreen role="staff" />);
  await flush();
  fireEvent.press(screen.getByText("Impostazioni"));
  await flush();
  expect(screen.getByLabelText("Giorni di anticipo per l'approvazione del mese (0–366)").props.value).toBe("5");
  fireEvent.changeText(screen.getByLabelText("Giorni di anticipo per l'approvazione del mese (0–366)"), "10");
  fireEvent.changeText(screen.getByLabelText("Motivo (obbligatorio, resta nello storico)"), "Più tempo per approvare");
  await act(async () => { fireEvent.press(screen.getByText("Salva impostazioni")); });
  expect(save).toHaveBeenCalledWith({ eventId: expect.any(String), expectedRevision: 3, timeZoneId: "Europe/Rome", approvalLeadDays: 10, reason: "Più tempo per approvare" }, "tok");
  expect(screen.getByText("Impostazioni salvate.")).toBeTruthy();
});

test("monte ore: vista con la classe scelta", async () => {
  renderAs("admin", <CalendarScreen role="admin" />);
  await flush();
  fireEvent.press(screen.getByText("Monte ore"));
  await flush();
  expect(screen.getByText(/Contano tutti i docenti, titolari e supplenti/)).toBeTruthy();
});

test("«Mostra annullate» resta attivo cambiando settimana, classe e scheda", async () => {
  renderAs("admin", <CalendarScreen role="admin" />);
  await flush();
  fireEvent.press(screen.getByText("Mostra annullate"));
  await flush();
  const last = () => lessonService.getSchedule.mock.calls.at(-1)[0];
  expect(last().includeCancelled).toBe(true);
  const week = last().from;
  await act(async () => { fireEvent.press(screen.getByLabelText("Settimana successiva")); });
  await flush();
  expect(last().from).not.toBe(week);
  expect(last().includeCancelled).toBe(true);
  fireEvent.press(screen.getByText("SW-25A")); // apre la scelta della classe
  fireEvent.press(screen.getAllByText("SW-25B").at(-1));
  await flush();
  expect(last()).toEqual(expect.objectContaining({ classId: "c2", includeCancelled: true }));
  fireEvent.press(screen.getByText("Monte ore"));
  await flush();
  fireEvent.press(screen.getByText("Lezioni"));
  await flush();
  expect(last().includeCancelled).toBe(true);
});
