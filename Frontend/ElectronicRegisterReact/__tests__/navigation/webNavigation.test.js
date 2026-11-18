// Navigazione su web: URL veri, indietro/avanti del browser, guardia sulle modifiche non salvate,
// menu a tendina su finestra stretta. Il browser è simulato (location, history, eventi).

// ── Browser finto, prima di caricare i moduli dell'app (isWeb si decide al caricamento) ──────────
const listeners = {};
const on = (type, fn) => { (listeners[type] ||= new Set()).add(fn); };
const off = (type, fn) => { listeners[type]?.delete(fn); };
const emit = (type, event = {}) => { [...(listeners[type] || [])].forEach(fn => fn(event)); };

const entries = [{ url: "/", state: null }];
let index = 0;
const setLocation = () => {
  const [pathname, search = ""] = entries[index].url.split("?");
  window.location.pathname = pathname;
  window.location.search = search ? `?${search}` : "";
};
function browserAt(url) { entries.splice(0, entries.length, { url, state: null }); index = 0; setLocation(); }

global.window.location = { pathname: "/", search: "" };
global.window.history = {
  get state() { return entries[index].state; },
  pushState(state, _t, url) { entries.splice(index + 1); entries.push({ url, state }); index++; setLocation(); },
  replaceState(state, _t, url) { entries[index] = { url, state }; setLocation(); },
  back() { if (index > 0) { index--; setLocation(); emit("popstate", {}); } },
};
global.window.addEventListener = on;
global.window.removeEventListener = off;
global.document = { addEventListener: on, removeEventListener: off };
require("react-native").Platform.OS = "web";

const tl = require("@testing-library/react-native");
const { act, fireEvent, render, renderHook } = tl;
// "screen" si aggiorna a ogni render (binding ESM): con require va letto dal modulo ogni volta
const screen = new Proxy({}, { get: (_, key) => tl.screen[key] });
const routes = require("../../src/navigation/examRoutes");
const AppNav = require("../../src/navigation/AppNav").default;
const { AuthContext } = require("../../src/hooks/useAuth");
const { USERS } = require("../helpers/render");

jest.mock("../../src/screens/DashboardScreen", () => require("../helpers/placeholder")("dashboard"));
jest.mock("../../src/screens/StudentsScreen", () => require("../helpers/placeholder")("Students"));
jest.mock("../../src/screens/UsersScreen", () => require("../helpers/placeholder")("Users"));
jest.mock("../../src/screens/ExamsScreen", () => require("../helpers/placeholder")("Exams"));
jest.mock("../../src/screens/ProfileScreen", () => require("../helpers/placeholder")("Profile"));
jest.mock("../../src/screens/ClassesScreen", () => require("../helpers/placeholder")("Classes"));
jest.mock("../../src/screens/TeachersScreen", () => require("../helpers/placeholder")("Teachers"));
jest.mock("../../src/screens/SubjectsScreen", () => require("../helpers/placeholder")("Subjects"));
jest.mock("../../src/screens/CalendarImportScreen", () => require("../helpers/placeholder")("CalendarImport"));
jest.mock("../../src/data/studentExams", () => ({ useStudentExams: () => ({ unseenCount: 0 }) }));
let mockWidth = 1280;
jest.mock("react-native/Libraries/Utilities/useWindowDimensions", () => ({
  __esModule: true, default: () => ({ width: mockWidth, height: 800, scale: 1, fontScale: 1 }),
}));

afterAll(() => { require("react-native").Platform.OS = "ios"; delete global.document; });
afterEach(() => { routes.setLeaveGuard(null); routes.setPageHeader(null); mockWidth = 1280; });
const url = () => window.location.pathname + window.location.search;

describe("rotte degli esami nel browser", () => {
  test("all'apertura la rotta si legge dall'URL", () => {
    browserAt("/esami/e1/modifica?step=2");
    const { result } = renderHook(() => routes.useExamRoute());
    expect(result.current.route).toEqual({ name: "edit", id: "e1", step: 2 });
  });

  test("push aggiunge una voce di cronologia; 'indietro' del browser torna alla lista", () => {
    browserAt("/esami?tab=attivi");
    const { result } = renderHook(() => routes.useExamRoute());
    act(() => result.current.push({ name: "view", id: "e7" }));
    expect(url()).toBe("/esami/e7");
    expect(entries).toHaveLength(2);
    act(() => window.history.back());
    expect(result.current.route).toEqual({ name: "list", tab: "active" });
  });

  test("back() dell'app usa la cronologia se la pagina è stata aperta da qui, altrimenti sostituisce", () => {
    browserAt("/esami/e7");
    const { result } = renderHook(() => routes.useExamRoute());
    act(() => result.current.back({ name: "list", tab: "drafts" })); // aperta direttamente (ricarica): niente cronologia
    expect(url()).toBe("/esami?tab=bozze");
    expect(entries).toHaveLength(1);
  });

  test("modifiche non salvate: 'indietro' rimette la pagina e chiede; confermando torna indietro davvero", () => {
    browserAt("/esami");
    const { result } = renderHook(() => routes.useExamRoute());
    act(() => result.current.push({ name: "new" }));
    const guard = jest.fn();
    routes.setLeaveGuard(guard);
    act(() => window.history.back());
    expect(url()).toBe("/esami/nuovo"); // pagina rimessa
    expect(result.current.route).toEqual({ name: "new" });
    routes.setLeaveGuard(null);
    act(() => guard.mock.calls[0][0]()); // "Esci senza salvare"
    expect(result.current.route).toEqual({ name: "list", tab: null });
  });

  test("href veri per i link e avviso del browser alla chiusura", () => {
    expect(routes.examHref({ name: "view", id: "e1" })).toBe("/esami/e1");
    const { unmount } = renderHook(({ active }) => routes.useBeforeUnload(active), { initialProps: { active: true } });
    const e = { preventDefault: jest.fn() };
    emit("beforeunload", e);
    expect(e.preventDefault).toHaveBeenCalled();
    unmount();
    expect(listeners.beforeunload.size).toBe(0);
  });
});

describe("sezioni nell'URL (AppNav)", () => {
  const renderNav = (role = "admin") => render(
    <AuthContext.Provider value={{ token: "tok", user: USERS[role] }}>
      <AppNav user={USERS[role]} onLogout={jest.fn()} />
    </AuthContext.Provider>
  );

  test("ricaricando su /membri si riapre Membri", () => {
    browserAt("/membri");
    renderNav();
    expect(screen.getByText("SCREEN Users")).toBeTruthy();
  });

  test("una sezione non del ruolo nell'URL non si apre", () => {
    browserAt("/membri");
    renderNav("teacher");
    expect(screen.getByText("SCREEN dashboard")).toBeTruthy();
    expect(url()).toBe("/");
  });

  test("ogni sezione aggiunge una voce; indietro/avanti passano tra le sezioni", () => {
    browserAt("/");
    renderNav();
    fireEvent.press(screen.getByText("Studenti"));
    expect(url()).toBe("/studenti");
    fireEvent.press(screen.getByText("Materie"));
    expect(url()).toBe("/materie");
    act(() => window.history.back());
    expect(screen.getByText("SCREEN Students")).toBeTruthy();
  });

  test("indietro con modifiche non salvate: chiede prima di cambiare sezione", () => {
    browserAt("/");
    renderNav();
    fireEvent.press(screen.getByText("Professori"));
    const guard = jest.fn();
    routes.setLeaveGuard(guard);
    act(() => window.history.back());
    expect(screen.getByText("SCREEN Teachers")).toBeTruthy();
    expect(url()).toBe("/professori");
    routes.setLeaveGuard(null);
    act(() => guard.mock.calls[0][0]());
    expect(screen.getByText("SCREEN dashboard")).toBeTruthy();
  });

  test("finestra stretta: intestazione con menu a tendina invece delle schede in basso", () => {
    mockWidth = 420;
    browserAt("/");
    renderNav();
    expect(screen.queryByText("Membri")).toBeNull();
    expect(screen.getByText("Dashboard")).toBeTruthy();
    fireEvent.press(screen.getByLabelText("Menu"));
    expect(screen.getByText("Membri")).toBeTruthy();
    expect(screen.getByText("Esci")).toBeTruthy();
    fireEvent.press(screen.getByText("Membri"));
    expect(screen.getByText("SCREEN Users")).toBeTruthy();
    expect(screen.queryByText("Calendario")).toBeNull(); // il menu si chiude dopo la scelta
    expect(url()).toBe("/membri");

    fireEvent.press(screen.getByLabelText("Menu"));
    act(() => emit("keydown", { key: "Escape" }));
    expect(screen.queryByText("Calendario")).toBeNull();
  });

  test("finestra stretta: pagina interna con freccia per tornare alla sezione", () => {
    mockWidth = 420;
    browserAt("/");
    renderNav();
    const onBack = jest.fn();
    act(() => routes.setPageHeader({ title: "Nuovo esame", onBack }));
    expect(screen.getByText("Nuovo esame")).toBeTruthy();
    fireEvent.press(screen.getByLabelText("Torna a Electronic Register"));
    expect(onBack).toHaveBeenCalled();
  });
});

describe("settimana del calendario nell'URL", () => {
  test("la settimana si legge dall'URL, si aggiorna con replace e la corrente non si scrive", () => {
    const { useWeek } = require("../../src/hooks/useWeekSchedule");
    const { currentWeek } = require("../../src/api/lessonService");
    browserAt("/?settimana=2026-12-09");
    const { result } = renderHook(() => useWeek());
    expect(result.current.week).toEqual({ from: "2026-12-07", to: "2026-12-13" });
    expect(url()).toBe("/?settimana=2026-12-07"); // normalizzata al lunedì
    act(() => result.current.next());
    expect(url()).toBe("/?settimana=2026-12-14");
    expect(entries).toHaveLength(1); // nessuna nuova voce nella cronologia
    act(() => result.current.today());
    expect(result.current.week).toEqual(currentWeek());
    expect(url()).toBe("/");
  });

  test("data non valida nell'URL: settimana corrente", () => {
    const { useWeek } = require("../../src/hooks/useWeekSchedule");
    const { currentWeek } = require("../../src/api/lessonService");
    browserAt("/?settimana=2026-02-30");
    const { result } = renderHook(() => useWeek());
    expect(result.current.week).toEqual(currentWeek());
  });

  test("ricaricando la Home con la settimana nell'URL la query resta", () => {
    browserAt("/?settimana=2026-12-07");
    render(<AuthContext.Provider value={{ token: "t", user: USERS.student }}><AppNav user={USERS.student} onLogout={jest.fn()} /></AuthContext.Provider>);
    expect(url()).toBe("/?settimana=2026-12-07");
  });
});
