// Navigazione principale: sezioni per ruolo, cambio di sezione, guardia d'uscita, intestazione delle pagine interne.
import { act, fireEvent, render, screen, within } from "@testing-library/react-native";
import { SafeAreaProvider } from "react-native-safe-area-context";
import AppNav from "../../src/navigation/AppNav";
import { AuthContext } from "../../src/hooks/useAuth";
import { setLeaveGuard, setPageHeader } from "../../src/navigation/examRoutes";
import { USERS } from "../helpers/render";

// Ogni schermata è un segnaposto: qui conta solo quale si apre
jest.mock("../../src/screens/DashboardScreen", () => { const { Text } = require("react-native"); return ({ navigate }) => <Text onPress={() => navigate("exams")}>SCREEN dashboard</Text>; });
jest.mock("../../src/screens/GradesScreen", () => require("../helpers/placeholder")("Grades"));
jest.mock("../../src/screens/StudentsScreen", () => require("../helpers/placeholder")("Students"));
jest.mock("../../src/screens/MyClassesScreen", () => require("../helpers/placeholder")("MyClasses"));
jest.mock("../../src/screens/TeachersScreen", () => require("../helpers/placeholder")("Teachers"));
jest.mock("../../src/screens/SubjectsScreen", () => require("../helpers/placeholder")("Subjects"));
jest.mock("../../src/screens/UsersScreen", () => require("../helpers/placeholder")("Users"));
jest.mock("../../src/screens/ProfileScreen", () => require("../helpers/placeholder")("Profile"));
jest.mock("../../src/screens/ExamsScreen", () => require("../helpers/placeholder")("Exams"));
jest.mock("../../src/screens/ClassesScreen", () => require("../helpers/placeholder")("Classes"));
jest.mock("../../src/screens/StudentExamsScreen", () => require("../helpers/placeholder")("StudentExams"));
jest.mock("../../src/screens/CalendarImportScreen", () => require("../helpers/placeholder")("CalendarImport"));
jest.mock("../../src/data/studentExams", () => ({ useStudentExams: () => ({ unseenCount: 0 }) }));

// Larghezza della finestra: sidebar da 900 px in su (useLayout)
let mockWidth = 1280;
jest.mock("react-native/Libraries/Utilities/useWindowDimensions", () => ({
  __esModule: true, default: () => ({ width: mockWidth, height: 800, scale: 1, fontScale: 1 }),
}));
beforeEach(() => { mockWidth = 1280; });
afterEach(() => { setLeaveGuard(null); setPageHeader(null); });

function renderNav(role, onLogout = jest.fn()) {
  const user = { ...USERS[role], displayName: "Utente di prova" };
  render(
    <SafeAreaProvider initialMetrics={{ frame: { x: 0, y: 0, width: 1280, height: 800 }, insets: { top: 0, left: 0, right: 0, bottom: 0 } }}>
      <AuthContext.Provider value={{ token: "tok", user }}>
        <AppNav user={user} onLogout={onLogout} />
      </AuthContext.Provider>
    </SafeAreaProvider>
  );
  return { onLogout };
}

const labels = () => ["Home", "Voti", "Classi", "Studenti", "Professori", "Materie", "Esami", "Calendario", "Membri", "Profilo"]
  .filter(t => screen.queryByText(t) !== null);

describe("sidebar per ruolo (schermo largo)", () => {
  test.each([
    ["admin", ["Home", "Classi", "Studenti", "Professori", "Materie", "Esami", "Calendario", "Membri", "Profilo"]],
    ["staff", ["Home", "Voti", "Studenti", "Professori", "Esami", "Profilo"]],
    ["teacher", ["Home", "Classi", "Materie", "Esami", "Profilo"]],
    ["student", ["Home", "Voti", "Professori", "Materie", "Esami", "Profilo"]],
  ])("%s", (role, expected) => {
    renderNav(role);
    expect(labels()).toEqual(expected);
  });
});

test("cambio di sezione dalla sidebar e titolo nella barra", () => {
  renderNav("admin");
  expect(screen.getByText("SCREEN dashboard")).toBeTruthy();
  expect(screen.getByText("Dashboard")).toBeTruthy();
  fireEvent.press(screen.getByText("Membri"));
  expect(screen.getByText("SCREEN Users")).toBeTruthy();
  fireEvent.press(screen.getByText("Classi"));
  expect(screen.getByText("SCREEN Classes")).toBeTruthy();
});

test("Classi del docente ed esami dello studente: schermate dedicate", () => {
  renderNav("teacher");
  fireEvent.press(screen.getByText("Classi"));
  expect(screen.getByText("SCREEN MyClasses")).toBeTruthy();
});

test("lo studente vede i propri esami", () => {
  renderNav("student");
  fireEvent.press(screen.getByText("Esami"));
  expect(screen.getByText("SCREEN StudentExams")).toBeTruthy();
});

test("la home può aprire una sezione", () => {
  renderNav("teacher");
  fireEvent.press(screen.getByText("SCREEN dashboard"));
  expect(screen.getByText("SCREEN Exams")).toBeTruthy();
});

test("nome e ruolo dell'utente nella barra in alto", () => {
  renderNav("teacher");
  expect(screen.getByText("Anna Neri")).toBeTruthy();
  expect(screen.getByText("teacher")).toBeTruthy();
});

test("modifiche non salvate: cambiare sezione o uscire passa dalla conferma", () => {
  const { onLogout } = renderNav("admin");
  const guard = jest.fn();
  setLeaveGuard(guard);
  fireEvent.press(screen.getByText("Studenti"));
  expect(screen.getByText("SCREEN dashboard")).toBeTruthy();
  act(() => guard.mock.calls[0][0]()); // "Esci senza salvare"
  expect(screen.getByText("SCREEN Students")).toBeTruthy();
  fireEvent.press(screen.getByText("Esci"));
  expect(onLogout).not.toHaveBeenCalled();
  act(() => guard.mock.calls[1][0]());
  expect(onLogout).toHaveBeenCalled();
});

test("pagina interna: '← Sezione / titolo' nella barra", () => {
  renderNav("admin");
  fireEvent.press(screen.getByText("Esami"));
  const onBack = jest.fn();
  act(() => setPageHeader({ title: "Nuovo esame", onBack }));
  expect(screen.getByText("Nuovo esame")).toBeTruthy();
  fireEvent.press(screen.getByLabelText("Torna a Esami"));
  expect(onBack).toHaveBeenCalled();
});

describe("app su telefono (schermo stretto)", () => {
  test("barra in alto e schede in basso con Home e Profilo", () => {
    mockWidth = 400;
    renderNav("admin");
    expect(screen.getByText(/Electronic Register/)).toBeTruthy();
    expect(screen.queryByText("Membri")).toBeNull();
    fireEvent.press(screen.getByText("Profilo"));
    expect(screen.getByText("SCREEN Profile")).toBeTruthy();
    fireEvent.press(screen.getByLabelText("Torna alla Home"));
    expect(screen.getByText("SCREEN dashboard")).toBeTruthy();
  });
});

test("accessibilità: la voce attiva della sidebar è 'selected'", () => {
  renderNav("admin");
  const selected = () => screen.getAllByRole("link").filter(el => el.props.accessibilityState?.selected).map(el => el.props["aria-current"]);
  expect(selected()).toEqual(["page"]);
  fireEvent.press(screen.getByText("Membri"));
  const active = screen.getAllByRole("link").find(el => el.props.accessibilityState?.selected);
  expect(within(active).getByText("Membri")).toBeTruthy();
});
