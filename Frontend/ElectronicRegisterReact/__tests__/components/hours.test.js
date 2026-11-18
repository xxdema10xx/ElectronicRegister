// Monte ore: report della classe (admin e staff) e "Le mie ore" del docente.
import { act, fireEvent, screen } from "@testing-library/react-native";
import ClassHoursReport from "../../src/components/ClassHoursReport";
import TeacherHours from "../../src/components/TeacherHours";
import * as calendarService from "../../src/api/calendarService";
import * as lessonService from "../../src/api/lessonService";
import { formatMinutes, getTeacherHours, rangeError, schoolYear } from "../../src/api/calendarService";
import { flush, renderAs } from "../helpers/render";

jest.mock("../../src/api/calendarService", () => ({
  ...jest.requireActual("../../src/api/calendarService"),
  getClassHours: jest.fn(), getPlanNames: jest.fn(), getLessonModule: jest.fn(),
}));
jest.mock("../../src/api/lessonService", () => ({ ...jest.requireActual("../../src/api/lessonService"), getAllLessons: jest.fn() }));

describe("formato e intervalli", () => {
  test("minuti → ore e minuti, senza arrotondare", () => {
    expect(formatMinutes(750)).toBe("12 h 30 min");
    expect(formatMinutes(599)).toBe("9 h 59 min");
    expect(formatMinutes(45)).toBe("45 min");
    expect(formatMinutes(600)).toBe("10 h");
    expect(formatMinutes(0)).toBe("0 min");
  });

  test("anno scolastico e limite di 366 giorni", () => {
    expect(schoolYear(new Date(2026, 10, 18))).toEqual({ from: "2026-09-01", to: "2027-08-31" });
    expect(schoolYear(new Date(2027, 1, 1))).toEqual({ from: "2026-09-01", to: "2027-08-31" });
    expect(rangeError({ from: "2026-09-01", to: "2027-08-31" })).toBeNull();
    expect(rangeError({ from: "2026-09-01", to: "2027-09-05" })).toMatch(/366/);
    expect(rangeError({ from: "2026-09-02", to: "2026-09-01" })).toMatch(/successiva/);
  });
});

describe("monte ore della classe", () => {
  const report = {
    classId: "c1", versionId: "v1", plannedMinutes: 1800, scheduledMinutes: 1830, deliveredMinutes: 599,
    modules: [
      { moduleId: "m1", plannedMinutes: 1200, scheduledMinutes: 1230, deliveredMinutes: 599, remainingToScheduleMinutes: 0, remainingToDeliverMinutes: 601, scheduledExcessMinutes: 30, deliveredExcessMinutes: 0,
        warnings: ["calendar.over-scheduled", "calendar.delivery-incomplete"] },
      { moduleId: "m2", plannedMinutes: 600, scheduledMinutes: 600, deliveredMinutes: 0, remainingToScheduleMinutes: 0, remainingToDeliverMinutes: 600, scheduledExcessMinutes: 0, deliveredExcessMinutes: 0,
        warnings: ["calendar.delivery-incomplete"] },
    ],
    units: [{ unitId: "u1", plannedMinutes: 1800, scheduledMinutes: 1830, deliveredMinutes: 599 }],
    months: [{ year: 2026, month: 12, scheduledMinutes: 900, deliveredMinutes: 0 }, { year: 2026, month: 11, scheduledMinutes: 930, deliveredMinutes: 599 }],
  };

  test("tabella per modulo con nomi, avvisi tradotti, eccedenza, totali, unità e mesi", async () => {
    calendarService.getClassHours.mockResolvedValue(report);
    calendarService.getPlanNames.mockResolvedValue({ modules: { m1: "M1 · Basi di dati" }, units: { u1: "UF1 · Dati" } });
    renderAs("admin", <ClassHoursReport classId="c1" />);
    await flush();
    expect(calendarService.getPlanNames).toHaveBeenCalledWith("v1", "tok");
    expect(screen.getByText("M1 · Basi di dati")).toBeTruthy();
    expect(screen.getByText("Modulo")).toBeTruthy(); // nome non trovato
    expect(screen.getByText("Ore oltre il previsto")).toBeTruthy();
    expect(screen.getAllByText("Ore ancora da svolgere")).toHaveLength(2);
    expect(screen.getByText("+30 min")).toBeTruthy();
    expect(screen.getByText("Programmate in eccesso")).toBeTruthy();
    expect(screen.getAllByText("9 h 59 min").length).toBeGreaterThan(0); // nessun arrotondamento
    expect(screen.getByText("UF1 · Dati")).toBeTruthy();
    const monthLabels = screen.getAllByText(/^(novembre|dicembre) 2026$/).map(n => n.props.children);
    expect(monthLabels).toEqual(["novembre 2026", "dicembre 2026"]);
    expect(screen.queryByText(/calendar\./)).toBeNull();
  });

  test("409 piano formativo mancante: messaggio, non una tabella vuota", async () => {
    calendarService.getClassHours.mockRejectedValue(Object.assign(new Error("x"), { status: 409, code: "calendar.teaching-configuration-required" }));
    renderAs("staff", <ClassHoursReport classId="c1" />);
    await flush();
    expect(screen.getByText(/non ha un piano formativo pubblicato: chiedi all'amministrazione/)).toBeTruthy();
    expect(screen.queryByText("Moduli")).toBeNull();
  });
});

describe("le mie ore (docente)", () => {
  const lessons = [
    { id: "a1", classId: "c1", moduleId: "m1", className: "SW-25A", moduleName: "Reti", status: "Completed", durationMinutes: 120, deliveredMinutes: 110 },
    { id: "a2", classId: "c1", moduleId: "m1", className: "SW-25A", moduleName: "Reti", status: "Scheduled", durationMinutes: 120 },
    { id: "a3", classId: "c1", moduleId: "m1", className: "SW-25A", moduleName: "Reti", status: "Cancelled", durationMinutes: 120, deliveredMinutes: 999 },
    { id: "b1", classId: "c2", moduleId: "m1", className: "SW-25B", moduleName: "Reti", status: "Scheduled", startUtc: "2026-11-16T08:00:00Z", endUtc: "2026-11-16T09:30:00Z" },
    { id: "c1", classId: "c1", moduleId: "m2", className: "SW-25A", moduleName: "Java", status: "Scheduled", durationMinutes: 60, deliveredMinutes: 60 },
  ];

  test("raggruppa per classe + modulo, esclude annullate, svolte solo dalle Completed, una chiamata module per modulo", async () => {
    const loadModule = jest.fn((id) => Promise.resolve({ plannedMinutes: id === "c1" ? 3000 : 6000 }));
    const rows = await getTeacherHours({ from: "2026-09-01", to: "2027-08-31" }, "tok", { loadLessons: async () => lessons, loadModule });
    expect(rows.map(r => [r.className, r.moduleName, r.scheduledMinutes, r.deliveredMinutes, r.plannedMinutes])).toEqual([
      ["SW-25A", "Java", 60, 0, 3000],
      ["SW-25A", "Reti", 240, 110, 6000],
      ["SW-25B", "Reti", 90, 0, 6000],
    ]);
    expect(loadModule).toHaveBeenCalledTimes(2); // m1 e m2, non una per classe
  });

  test("pagina: legge tutte le pagine dell'intervallo, nessun residuo né avviso; nuovo intervallo", async () => {
    lessonService.getAllLessons.mockResolvedValue(lessons);
    calendarService.getLessonModule.mockResolvedValue({ plannedMinutes: 6000 });
    renderAs("teacher", <TeacherHours />);
    await flush();
    const year = schoolYear();
    expect(lessonService.getAllLessons).toHaveBeenCalledWith("/institution/calendar/teachers/me", year, "tok");
    expect(screen.getByText(/non il totale del modulo/)).toBeTruthy();
    expect(screen.getByText("SW-25A · Reti")).toBeTruthy();
    expect(screen.getAllByText("Previste dal modulo")).toHaveLength(3);
    expect(screen.queryByText(/Da programmare|Da svolgere|oltre il previsto|residuo da/i)).toBeNull();
    expect(calendarService.getLessonModule).toHaveBeenCalledTimes(2);
    fireEvent.changeText(screen.getByLabelText("Dal (AAAA-MM-GG)"), "2026-11-01");
    fireEvent.changeText(screen.getByLabelText("Al (AAAA-MM-GG)"), "2026-11-30");
    await act(async () => { fireEvent.press(screen.getByText("Aggiorna")); });
    expect(lessonService.getAllLessons).toHaveBeenLastCalledWith("/institution/calendar/teachers/me", { from: "2026-11-01", to: "2026-11-30" }, "tok");
    expect(calendarService.getLessonModule).toHaveBeenCalledTimes(2); // ore previste già in cache
  });
});
