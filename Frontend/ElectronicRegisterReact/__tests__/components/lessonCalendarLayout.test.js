// Orario settimanale: colonne che riempiono il riquadro quando c'è spazio, giorni in colonna quando è stretto.
// Nessun giorno resta nascosto dietro uno scorrimento orizzontale senza segnale.
import { fireEvent, render, screen } from "@testing-library/react-native";
import LessonCalendar, { MIN_COL_W, calendarLayout } from "../../src/components/LessonCalendar";

const lessons = [1, 2, 3, 4, 5].map(day => ({ id: `l${day}`, day, start: "09:00", end: "11:00", title: `Lezione ${day}`, teacherName: "", className: "", room: "", status: "Scheduled" }));
const week = { from: "2026-11-30", to: "2026-12-06" };

test("calendarLayout: colonne solo se tutti i giorni stanno ad almeno 150 px", () => {
  expect(MIN_COL_W).toBe(150);
  expect(calendarLayout(1200, 5)).toBe("columns");
  expect(calendarLayout(5 * 150 + 4 * 8, 5)).toBe("columns");
  expect(calendarLayout(5 * 150 + 4 * 8 - 1, 5)).toBe("stacked");
  expect(calendarLayout(380, 5)).toBe("stacked");
  expect(calendarLayout(1100, 7)).toBe("columns");
});

test("riquadro largo: colonne flessibili, nessuno ScrollView orizzontale, tutti i giorni visibili", () => {
  render(<LessonCalendar lessons={lessons} week={week} />);
  fireEvent(screen.getByTestId(/lesson-calendar-/), "layout", { nativeEvent: { layout: { width: 1100 } } });
  const root = screen.getByTestId("lesson-calendar-columns");
  expect(root.props.style).toEqual(expect.objectContaining({ flexDirection: "row" }));
  expect(screen.UNSAFE_queryAllByProps({ horizontal: true })).toHaveLength(0);
  ["Lun 30/11", "Mar 01/12", "Mer 02/12", "Gio 03/12", "Ven 04/12"].forEach(d => expect(screen.getByText(d)).toBeTruthy());
});

test("riquadro stretto (telefono): giorni uno sotto l'altro, anche giovedì e venerdì", () => {
  render(<LessonCalendar lessons={lessons} week={week} />);
  fireEvent(screen.getByTestId(/lesson-calendar-/), "layout", { nativeEvent: { layout: { width: 360 } } });
  const root = screen.getByTestId("lesson-calendar-stacked");
  expect(root.props.style).toEqual(expect.objectContaining({ flexDirection: "column" }));
  expect(screen.getByText("Gio 03/12")).toBeTruthy();
  expect(screen.getByText("Lezione 5")).toBeTruthy();
});
