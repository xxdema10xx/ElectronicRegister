// Dettaglio della lezione: descrizione e stati per tutti; storico con i motivi solo per admin e staff.
import { act, fireEvent, screen } from "@testing-library/react-native";
import LessonDetail from "../../src/components/LessonDetail";
import * as calendarService from "../../src/api/calendarService";
import * as teacherService from "../../src/api/teacherService";
import { describeHistory, formatLongDate } from "../../src/api/calendarService";
import { flush, renderAs } from "../helpers/render";

jest.mock("../../src/api/calendarService", () => ({
  ...jest.requireActual("../../src/api/calendarService"),
  getLesson: jest.fn(), getLessonHistory: jest.fn(),
}));
jest.mock("../../src/api/teacherService");

const lesson = {
  id: "L1", classId: "c1", moduleId: "m1", teacherId: "t2", originalTeacherId: "t1", date: "2026-11-19", startsAt: "09:00:00", endsAt: "11:00:00",
  publication: "Draft", status: "Scheduled", revision: 4, room: "Lab 1", description: "Introduzione a SQL", className: "SW-25A", moduleName: "Basi di dati", teacherName: "Luca Bianchi",
};
const teachers = [{ id: "t1", firstName: "Anna", lastName: "Neri" }, { id: "t2", firstName: "Luca", lastName: "Bianchi" }];

const reschedule = {
  eventId: "e1", operation: "LessonRescheduled", actorUserId: "u-secret", actorMembershipId: "m-secret", revisionBefore: 1, revisionAfter: 2,
  beforeJson: JSON.stringify({ local_date: "2026-11-18T00:00:00", starts_at: "08:00:00", ends_at: "10:00:00", teacher_id: "t1" }),
  stateJson: JSON.stringify({ Date: "2026-11-19", StartsAt: "09:00:00", EndsAt: "11:00:00", TeacherId: "t1" }),
  reason: "Aula occupata", occurredAtUtc: "2026-11-10T08:30:00",
};

beforeEach(() => {
  calendarService.getLesson.mockResolvedValue(lesson);
  calendarService.getLessonHistory.mockResolvedValue([reschedule]);
  teacherService.getTeachers.mockResolvedValue(teachers);
});

describe("storico: testo delle voci", () => {
  test("spostamento: da … a … ricavato dai due JSON, nessun ID tecnico", () => {
    const h = describeHistory(reschedule);
    expect(h.operation).toBe("Spostamento");
    expect(h.change).toBe("da 18/11/2026 08:00–10:00 a 19/11/2026 09:00–11:00");
    expect(h.reason).toBe("Aula occupata");
    expect(JSON.stringify(h)).not.toMatch(/secret/);
  });

  test("JSON incompleto o non valido: solo il motivo", () => {
    expect(describeHistory({ ...reschedule, beforeJson: "{non json", stateJson: "{}" }).change).toBeNull();
    expect(describeHistory({ ...reschedule, beforeJson: null }).change).toBeNull();
    expect(describeHistory({ ...reschedule, stateJson: JSON.stringify({ Date: "2026-11-19" }) }).change).toBeNull();
  });

  test("sostituzione con i nomi dei docenti; operazioni tradotte", () => {
    const name = (id) => ({ t1: "Anna Neri", t2: "Luca Bianchi" }[id] || null);
    const sub = { ...reschedule, operation: "TeacherSubstituted", beforeJson: JSON.stringify({ teacher_id: "t1" }), stateJson: JSON.stringify({ TeacherId: "t2" }) };
    expect(describeHistory(sub, name)).toMatchObject({ operation: "Sostituzione del docente", change: "da Anna Neri a Luca Bianchi" });
    expect(describeHistory({ ...sub, operation: "LessonCancelled" }).operation).toBe("Annullamento");
    expect(describeHistory({ ...sub, operation: "LessonPublished" }).operation).toBe("Pubblicazione");
    expect(describeHistory({ ...sub, operation: "Sconosciuta" }).operation).toBe("Modifica");
    expect(formatLongDate("2026-11-19")).toBe("giovedì 19 novembre 2026");
  });
});

describe("scheda della lezione", () => {
  test("admin: dati, descrizione, supplenza con titolare, bozza e storico con motivo", async () => {
    renderAs("admin", <LessonDetail lessonId="L1" role="admin" onClose={jest.fn()} />);
    await flush();
    ["giovedì 19 novembre 2026", "09:00–11:00", "SW-25A", "Introduzione a SQL", "Lab 1", "Luca Bianchi", "Anna Neri", "Supplenza", "Bozza", "Programmata · Bozza"]
      .forEach(t => expect(screen.getAllByText(t).length).toBeGreaterThan(0));
    expect(screen.getByText("Storico modifiche")).toBeTruthy();
    expect(screen.getByText("Motivo: Aula occupata")).toBeTruthy();
    expect(screen.getByText("da 18/11/2026 08:00–10:00 a 19/11/2026 09:00–11:00")).toBeTruthy();
    expect(calendarService.getLessonHistory).toHaveBeenCalledWith("L1", 1, "tok");
  });

  test("docente e studente: descrizione sì, storico e motivi no", async () => {
    for (const role of ["teacher", "student"]) {
      calendarService.getLessonHistory.mockClear();
      const { unmount } = renderAs(role, <LessonDetail lessonId="L1" role={role} onClose={jest.fn()} />);
      await flush();
      expect(screen.getByText("Introduzione a SQL")).toBeTruthy();
      expect(screen.queryByText("Storico modifiche")).toBeNull();
      expect(screen.queryByText(/Motivo/)).toBeNull();
      expect(screen.queryByText("Bozza")).toBeNull();
      expect(calendarService.getLessonHistory).not.toHaveBeenCalled();
      expect(teacherService.getTeachers).not.toHaveBeenCalled();
      unmount();
    }
  });

  test("storico paginato 20 alla volta", async () => {
    const page = (n, k) => Array.from({ length: n }, (_, i) => ({ ...reschedule, eventId: `${k}-${i}` }));
    calendarService.getLessonHistory.mockResolvedValueOnce(page(20, "a")).mockResolvedValueOnce(page(3, "b"));
    renderAs("staff", <LessonDetail lessonId="L1" role="staff" onClose={jest.fn()} />);
    await flush();
    expect(screen.getAllByText("Motivo: Aula occupata")).toHaveLength(20);
    await act(async () => { fireEvent.press(screen.getByText("Mostra altre modifiche")); });
    expect(calendarService.getLessonHistory).toHaveBeenLastCalledWith("L1", 2, "tok");
    expect(screen.getAllByText("Motivo: Aula occupata")).toHaveLength(23);
    expect(screen.queryByText("Mostra altre modifiche")).toBeNull();
  });

  test("lezione annullata e svolta", async () => {
    calendarService.getLesson.mockResolvedValueOnce({ ...lesson, status: "Cancelled", teacherId: "t1", publication: "Published" });
    const { unmount } = renderAs("student", <LessonDetail lessonId="L1" role="student" onClose={jest.fn()} />);
    await flush();
    expect(screen.getAllByText("Annullata").length).toBeGreaterThan(0);
    unmount();
    calendarService.getLesson.mockResolvedValueOnce({ ...lesson, status: "Completed", teacherId: "t1", deliveredMinutes: 120 });
    renderAs("teacher", <LessonDetail lessonId="L1" role="teacher" onClose={jest.fn()} />);
    await flush();
    expect(screen.getAllByText("Svolta").length).toBeGreaterThan(0);
    expect(screen.getByText("120")).toBeTruthy();
  });
});
