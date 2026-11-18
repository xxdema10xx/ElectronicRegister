// Annulla, sposta, sostituisci (admin e staff): payload esatti, motivo obbligatorio, eventId stabile nei
// retry e nuovo dopo una modifica, rilettura dopo la receipt e dopo un 409, niente comandi sulle svolte.
import { act, fireEvent, screen } from "@testing-library/react-native";
import LessonActions, { CHANGED_MESSAGE } from "../../src/components/LessonActions";
import * as calendarService from "../../src/api/calendarService";
import { rescheduleError, validReason } from "../../src/api/calendarService";
import { renderAs } from "../helpers/render";

jest.mock("../../src/api/calendarService", () => ({
  ...jest.requireActual("../../src/api/calendarService"),
  cancelLesson: jest.fn(), rescheduleLesson: jest.fn(), substituteLesson: jest.fn(),
}));

const lesson = { id: "L1", teacherId: "t1", originalTeacherId: "t1", date: "2026-11-19", startsAt: "09:00:00", endsAt: "11:00:00", publication: "Published", status: "Scheduled", revision: 7 };
const teachers = [{ id: "t1", firstName: "Anna", lastName: "Neri" }, { id: "t2", firstName: "Luca", lastName: "Bianchi" }, { id: "t3", firstName: "Sara", lastName: "Verdi" }];

beforeEach(() => {
  calendarService.cancelLesson.mockResolvedValue({ eventId: "x", resourceId: "L1", revision: 8 });
  calendarService.rescheduleLesson.mockResolvedValue({ eventId: "x", resourceId: "L1", revision: 8 });
  calendarService.substituteLesson.mockResolvedValue({ eventId: "x", resourceId: "L1", revision: 8 });
});

const type = (label, text) => fireEvent.changeText(screen.getByLabelText(label), text);
const REASON = "Motivo (obbligatorio, resta nello storico)";
const press = async (text) => { await act(async () => { fireEvent.press(screen.getAllByText(text).at(-1)); }); };

test("validazioni di base", () => {
  expect(validReason("  ")).toBe(false);
  expect(validReason("x".repeat(501))).toBe(false);
  expect(validReason("Aula occupata")).toBe(true);
  expect(rescheduleError({ date: "2026-11-31", startsAt: "09:00", endsAt: "10:00" })).toMatch(/Data/);
  expect(rescheduleError({ date: "2026-11-20", startsAt: "9:00", endsAt: "10:00" })).toMatch(/inizio/);
  expect(rescheduleError({ date: "2026-11-20", startsAt: "10:00", endsAt: "10:00" })).toMatch(/dopo/);
  expect(rescheduleError({ date: "2026-11-20", startsAt: "09:00", endsAt: "10:30" })).toBeNull();
});

test("nessun comando su lezioni svolte o annullate", () => {
  const { unmount } = renderAs("admin", <LessonActions lesson={{ ...lesson, status: "Completed" }} teachers={teachers} />);
  expect(screen.queryByText("Annulla lezione")).toBeNull();
  unmount();
  renderAs("admin", <LessonActions lesson={{ ...lesson, status: "Cancelled" }} teachers={teachers} />);
  expect(screen.queryByText("Sposta")).toBeNull();
});

test("annulla: motivo e conferma obbligatori, payload esatto, rilettura dopo la receipt", async () => {
  const onChanged = jest.fn();
  renderAs("staff", <LessonActions lesson={lesson} teachers={teachers} onChanged={onChanged} />);
  await press("Annulla lezione");
  await press("Annulla lezione");
  expect(calendarService.cancelLesson).not.toHaveBeenCalled();
  expect(screen.getByText("Indica il motivo dell'annullamento.")).toBeTruthy();
  type(REASON, "  Docente malato  ");
  expect(screen.getByText(`${"  Docente malato  ".length}/500`)).toBeTruthy(); // conta tutto il testo scritto
  await press("Annulla lezione");
  expect(screen.getByText("Conferma l'annullamento.")).toBeTruthy();
  fireEvent.press(screen.getByText(/Confermo: la lezione verrà annullata/));
  await press("Annulla lezione");
  expect(calendarService.cancelLesson).toHaveBeenCalledWith("L1", { eventId: expect.any(String), expectedRevision: 7, reason: "Docente malato" }, "tok");
  expect(Object.keys(calendarService.cancelLesson.mock.calls[0][1]).sort()).toEqual(["eventId", "expectedRevision", "reason"]);
  expect(onChanged).toHaveBeenCalledWith("ok");
  expect(screen.getByText("Lezione annullata.")).toBeTruthy();
});

test("sposta: avviso se pubblicata, orari HH:mm:ss, eventId uguale nel retry e nuovo dopo una modifica", async () => {
  const onChanged = jest.fn();
  calendarService.rescheduleLesson
    .mockRejectedValueOnce(Object.assign(new Error("rete"), { message: "Connessione assente." }))
    .mockRejectedValueOnce(Object.assign(new Error("rete"), { message: "Connessione assente." }))
    .mockResolvedValueOnce({ revision: 8 });
  renderAs("admin", <LessonActions lesson={lesson} teachers={teachers} onChanged={onChanged} />);
  await press("Sposta");
  expect(screen.getByText(/sarà subito visibile a docente e studenti/)).toBeTruthy();
  type("Data (AAAA-MM-GG)", "2026-11-20");
  type("Ora inizio (HH:mm)", "10:00");
  type("Ora fine (HH:mm)", "12:00");
  type(REASON, "Aula occupata");
  await press("Sposta");
  await press("Sposta");
  const [first, second] = calendarService.rescheduleLesson.mock.calls.map(c => c[1]);
  expect(first).toEqual({ eventId: first.eventId, expectedRevision: 7, date: "2026-11-20", startsAt: "10:00", endsAt: "12:00", reason: "Aula occupata" });
  expect(second.eventId).toBe(first.eventId); // stesso payload: stesso eventId
  type("Ora fine (HH:mm)", "12:30");
  await press("Sposta");
  const third = calendarService.rescheduleLesson.mock.calls[2][1];
  expect(third.eventId).not.toBe(first.eventId); // dati cambiati: nuovo eventId
  expect(onChanged).toHaveBeenLastCalledWith("ok");
});

test("sposta: fine prima dell'inizio non parte", async () => {
  renderAs("admin", <LessonActions lesson={lesson} teachers={teachers} />);
  await press("Sposta");
  type("Ora fine (HH:mm)", "08:00");
  type(REASON, "x");
  await press("Sposta");
  expect(calendarService.rescheduleLesson).not.toHaveBeenCalled();
  expect(screen.getAllByText("L'ora di fine deve essere dopo l'ora di inizio.").length).toBeGreaterThan(0);
});

test("sostituisci: nomi dei docenti escluso l'attuale; 409 → rilettura e avviso", async () => {
  const onChanged = jest.fn();
  calendarService.substituteLesson.mockRejectedValueOnce(Object.assign(new Error("x"), { status: 409, code: "calendar.revision-conflict" }));
  renderAs("admin", <LessonActions lesson={lesson} teachers={teachers} onChanged={onChanged} />);
  await press("Sostituisci docente");
  fireEvent.press(screen.getByText("Scegli il docente"));
  expect(screen.queryByText("Neri Anna")).toBeNull();
  expect(screen.getByText("Bianchi Luca")).toBeTruthy();
  fireEvent.press(screen.getByText("Verdi Sara"));
  type(REASON, "Supplenza per malattia");
  await press("Sostituisci");
  expect(calendarService.substituteLesson).toHaveBeenCalledWith("L1", { eventId: expect.any(String), expectedRevision: 7, teacherId: "t3", reason: "Supplenza per malattia" }, "tok");
  expect(onChanged).toHaveBeenCalledWith("stale");
  expect(screen.getByText(CHANGED_MESSAGE)).toBeTruthy();
});

test("errore calendar.* tradotto", async () => {
  calendarService.rescheduleLesson.mockRejectedValueOnce(Object.assign(new Error("x"), { status: 409, code: "calendar.overlap" }));
  renderAs("admin", <LessonActions lesson={lesson} teachers={teachers} />);
  await press("Sposta");
  type(REASON, "Laboratorio");
  await press("Sposta");
  expect(screen.getByText("Nel nuovo orario la classe o il docente hanno già un'altra lezione.")).toBeTruthy();
});
