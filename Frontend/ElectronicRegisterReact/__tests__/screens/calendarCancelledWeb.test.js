// Calendario su web: "Mostra annullate" nell'URL (?annullate=1) come la settimana, così una ricarica
// o un link condiviso lo mantengono. Il browser è simulato prima di caricare i moduli (isWeb si decide al caricamento).
let current = { pathname: "/calendario", search: "" };
global.window.location = current;
global.window.history = {
  state: null,
  pushState(_s, _t, url) { this.replaceState(_s, _t, url); },
  replaceState(_s, _t, url) { const [p, q = ""] = url.split("?"); current.pathname = p; current.search = q ? `?${q}` : ""; },
};
global.window.addEventListener = () => {};
global.window.removeEventListener = () => {};
require("react-native").Platform.OS = "web";

const tl = require("@testing-library/react-native");
const screen = new Proxy({}, { get: (_, key) => tl.screen[key] });
const { fireEvent } = tl;
const CalendarScreen = require("../../src/screens/CalendarScreen").default;
const lessonService = require("../../src/api/lessonService");
const teacherService = require("../../src/api/teacherService");
const { flush, renderAs } = require("../helpers/render");

jest.mock("../../src/api/lessonService", () => ({ ...jest.requireActual("../../src/api/lessonService"), getSchedule: jest.fn(), getClassesForPicker: jest.fn() }));
jest.mock("../../src/api/teacherService");
jest.mock("../../src/screens/CalendarImportScreen", () => require("../helpers/placeholder")("CalendarImport"));

const params = () => new URLSearchParams(current.search);

beforeEach(() => {
  lessonService.getClassesForPicker.mockResolvedValue([{ id: "c1", name: "SW-25A" }]);
  lessonService.getSchedule.mockResolvedValue([]);
  teacherService.getTeachers.mockResolvedValue([]);
});

test("la spunta si scrive nell'URL e si toglie quando si disattiva", async () => {
  current.search = "";
  renderAs("admin", <CalendarScreen role="admin" />);
  await flush();
  expect(params().get("annullate")).toBeNull();
  fireEvent.press(screen.getByText("Mostra annullate"));
  await flush();
  expect(params().get("annullate")).toBe("1");
  expect(lessonService.getSchedule.mock.calls.at(-1)[0].includeCancelled).toBe(true);
  fireEvent.press(screen.getByText("Mostra annullate"));
  await flush();
  expect(params().get("annullate")).toBeNull();
});

test("ricaricando con ?annullate=1 la spunta è attiva e la settimana resta", async () => {
  current.search = "?annullate=1&settimana=2026-12-07&classe=c1";
  renderAs("admin", <CalendarScreen role="admin" />);
  await flush();
  expect(lessonService.getSchedule.mock.calls.at(-1)[0]).toEqual(expect.objectContaining({ includeCancelled: true, from: "2026-12-07", classId: "c1" }));
  expect(screen.getByRole("checkbox", { name: "Mostra annullate" }).props.accessibilityState.checked).toBe(true);
  expect(params().get("annullate")).toBe("1");
  expect(params().get("settimana")).toBe("2026-12-07");
});
