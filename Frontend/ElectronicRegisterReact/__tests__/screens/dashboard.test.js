// Home: numeri chiave, pannelli e orario, diversi per ruolo.
import { fireEvent, screen } from "@testing-library/react-native";
import DashboardScreen from "../../src/screens/DashboardScreen";
import * as subjectService from "../../src/api/subjectService";
import * as studentService from "../../src/api/studentService";
import * as teacherService from "../../src/api/teacherService";
import * as userService from "../../src/api/userService";
import * as classService from "../../src/api/classService";
import * as lessonService from "../../src/api/lessonService";
import * as examInsights from "../../src/api/examInsights";
import * as studentExams from "../../src/data/studentExams";
import { flush, renderAs } from "../helpers/render";

jest.mock("../../src/api/subjectService");
jest.mock("../../src/api/studentService");
jest.mock("../../src/api/teacherService");
jest.mock("../../src/api/userService");
jest.mock("../../src/api/classService");
jest.mock("../../src/api/lessonService", () => ({ ...jest.requireActual("../../src/api/lessonService"), getSchedule: jest.fn(), getClassesForPicker: jest.fn() }));
jest.mock("../../src/api/examInsights", () => ({ ...jest.requireActual("../../src/api/examInsights"), getExamInsights: jest.fn() }));
jest.mock("../../src/data/studentExams");

const mockExams = jest.fn();
jest.mock("../../src/hooks/useExamApi", () => {
  const api = { listExams: () => mockExams(), getClassSubjects: () => Promise.resolve([{ id: "cs1", subjectName: "Reti", className: "SW-25A" }]) };
  return { useExamApi: () => api };
});

const future = new Date(Date.now() + 86400000).toISOString();
const past = new Date(Date.now() - 86400000).toISOString();

beforeEach(() => {
  subjectService.getSubjects.mockResolvedValue([{ id: "m1" }, { id: "m2" }]);
  studentService.getStudents.mockResolvedValue([{ id: "s1" }, { id: "s2" }, { id: "s3" }]);
  teacherService.getTeachers.mockResolvedValue([{ id: "t1" }]);
  userService.getUsers.mockResolvedValue([{}, {}, {}, {}]);
  lessonService.getSchedule.mockResolvedValue([{ id: 1, day: 1, start: "09:00", end: "11:00", title: "Reti", teacherName: "", className: "", room: "" }]);
  lessonService.getClassesForPicker.mockResolvedValue([{ id: "c1", name: "SW-25A" }]);
  classService.getMyClasses.mockResolvedValue([]);
  examInsights.getExamInsights.mockResolvedValue({ retakeGroups: [{ absentOnly: true }, { absentOnly: false }], pendingReviewCount: 4, failed: 0 });
  mockExams.mockResolvedValue([
    { id: "e1", status: 1, classSubjectId: "cs1", availableFrom: past, availableUntil: future },
    { id: "e2", status: 0, name: "Java", availableFrom: future, availableUntil: future },
    { id: "e3", status: 1, name: "Vecchio", availableFrom: past, availableUntil: past },
  ]);
  studentExams.useStudentExams.mockReturnValue({ assigned: [], loaded: true, unseenCount: 0, isSeen: () => true });
  studentExams.useStudentBooklet.mockReturnValue([]);
});

test("admin: conteggi dell'istituto, prossimi esami e orario della classe scelta", async () => {
  const navigate = jest.fn();
  renderAs("admin", <DashboardScreen navigate={navigate} />);
  await flush();
  expect(screen.getByText("3")).toBeTruthy(); // studenti
  expect(screen.getByText("4")).toBeTruthy(); // membri
  expect(screen.getByText("Prossimi esami")).toBeTruthy();
  expect(screen.getByText("Reti · SW-25A")).toBeTruthy(); // pubblicato non terminato
  expect(screen.getByText("Bozza")).toBeTruthy();
  expect(screen.queryByText("Vecchio")).toBeNull(); // terminato
  expect(lessonService.getSchedule).toHaveBeenLastCalledWith(expect.objectContaining({ role: "admin", classId: "c1" }), "tok");
  expect(await screen.findByText("09:00–11:00")).toBeTruthy();
  // "Membri": numero chiave e, sull'app, anche la card della sezione in fondo
  expect(screen.getAllByText("Membri")).toHaveLength(2);
  fireEvent.press(screen.getAllByText("Membri")[0]);
  expect(navigate).toHaveBeenCalledWith("users");
  fireEvent.press(screen.getByText("Calendario"));
  expect(navigate).toHaveBeenCalledWith("calendar");
  fireEvent.press(screen.getByText("Vedi tutti ›"));
  expect(navigate).toHaveBeenCalledWith("exams");
});

test("docente: esami in programma, da correggere e da recuperare", async () => {
  renderAs("teacher", <DashboardScreen navigate={jest.fn()} />);
  await flush();
  expect(screen.getByText("Esami in programma")).toBeTruthy();
  expect(screen.getByText("1")).toBeTruthy(); // solo e1 è pubblicato e aperto
  expect(screen.getByText("4")).toBeTruthy(); // da correggere
  expect(screen.getByText("2")).toBeTruthy(); // studenti da recuperare
  expect(screen.getByText(/2 da recuperare \(1 assente\)/)).toBeTruthy();
  expect(lessonService.getSchedule).toHaveBeenCalledWith(expect.objectContaining({ role: "teacher", teacherName: "Anna Neri" }), "tok");
});

test("docente: consegne non leggibili", async () => {
  examInsights.getExamInsights.mockResolvedValue({ retakeGroups: [], pendingReviewCount: 0, failed: 2 });
  renderAs("teacher", <DashboardScreen navigate={jest.fn()} />);
  await flush();
  expect(screen.getAllByText("Impossibile caricare le consegne").length).toBe(2);
});

test("studente: esami da svolgere, da recuperare e media dal libretto", async () => {
  studentExams.useStudentExams.mockReturnValue({
    assigned: [{ id: "a1", name: "Reti", canStart: true, durationMinutes: 60, availableFrom: past, availableUntil: future }],
    loaded: true, unseenCount: 1, isSeen: () => false,
  });
  studentExams.useStudentBooklet.mockReturnValue([
    { r: { showGrade: true, grade: 27, passed: true, status: "graded" } },
    { r: { showGrade: true, grade: 15, passed: false, status: "graded" } },
    { r: { showGrade: true, grade: 12, passed: false, status: "finalized" } },
    { r: { showGrade: false, status: "failed_auto" } },
  ]);
  const navigate = jest.fn();
  renderAs("student", <DashboardScreen navigate={navigate} />);
  await flush();
  expect(screen.getByText("18.0")).toBeTruthy(); // media
  expect(screen.getByText("2")).toBeTruthy(); // da recuperare: solo i voti ufficiali insufficienti, non FailedAuto
  expect(screen.getAllByText("Esami da svolgere")).toHaveLength(2); // numero chiave e pannello
  expect(screen.getByText(/60 min/)).toBeTruthy();
  fireEvent.press(screen.getByText("I miei voti"));
  expect(navigate).toHaveBeenCalledWith("grades");
});

test("segreteria: niente orario, niente membri", async () => {
  renderAs("staff", <DashboardScreen navigate={jest.fn()} />);
  await flush();
  expect(screen.queryByText("Orario delle lezioni")).toBeNull();
  expect(screen.queryByText("Membri")).toBeNull();
  expect(userService.getUsers).not.toHaveBeenCalled();
});

test("un servizio che non risponde non blocca la home", async () => {
  subjectService.getSubjects.mockRejectedValue(new Error("x"));
  lessonService.getSchedule.mockRejectedValue(new Error("x"));
  renderAs("admin", <DashboardScreen navigate={jest.fn()} />);
  await flush();
  expect(screen.getByText(/Benvenuto/)).toBeTruthy();
  expect(await screen.findByText("Orario non ancora disponibile")).toBeTruthy();
});
