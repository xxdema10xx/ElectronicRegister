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
  getLesson: jest.fn(), getLessonHistory: jest.fn(), cancelLesson: jest.fn(),
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

test("vista Importa", async () => {
  renderAs("admin", <CalendarScreen role="admin" />);
  await flush();
  fireEvent.press(screen.getByText("Importa"));
  expect(screen.getByText("SCREEN CalendarImport")).toBeTruthy();
});
