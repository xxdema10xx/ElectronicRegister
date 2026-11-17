// Esami dello studente: elenco con stati, regole prima dell'inizio, avvio, ripresa, esito; notifiche "nuovo".
import { act, fireEvent, renderHook, screen, waitFor } from "@testing-library/react-native";
import StudentExamsScreen from "../../src/screens/StudentExamsScreen";
import { getRun, markSeen, saveRun, useStudentExams } from "../../src/data/studentExams";
import { clearUserStorage, setStorageUser } from "../../src/utils/userStorage";
import { AuthContext } from "../../src/hooks/useAuth";
import { flush, renderAs, USERS } from "../helpers/render";

const mockApi = {};
jest.mock("../../src/api/examApi", () => ({ createExamApi: () => mockApi }));
// Lo svolgimento ha i suoi test: qui basta sapere che parte e con quali dati
jest.mock("../../src/components/ExamTaker", () => {
  const { Text } = require("react-native");
  return {
    __esModule: true, MAX_VIOLATIONS: 3, enterFullscreen: jest.fn(), exitFullscreen: jest.fn(),
    default: ({ run, onFinish }) => (
      <Text onPress={() => onFinish({ result: { status: "pending_review" }, auto: false, reason: null, violations: 1 })}>
        {`TAKER ${run.submissionId} ${run.exam.questions.map(q => q.id).join(",")}`}
      </Text>
    ),
  };
});

const hour = 3600000;
const iso = (ms) => new Date(Date.now() + ms).toISOString();
const assigned = () => [
  { id: "a1", name: "Reti", canStart: true, durationMinutes: 60, extraTimeMinutes: 10, availableFrom: iso(-hour), availableUntil: iso(hour), description: "Senza appunti" },
  { id: "a2", name: "Java", submissionStatus: "in_progress", submissionId: "sub2", deadlineAt: iso(hour), durationMinutes: 30, availableFrom: iso(-hour), availableUntil: iso(hour) },
  { id: "a3", name: "Inglese", submissionStatus: "in_progress", submissionId: "sub3", deadlineAt: iso(-60000), durationMinutes: 30, availableFrom: iso(-hour), availableUntil: iso(hour) },
  { id: "a4", name: "Basi di dati", submissionStatus: "graded", submissionId: "sub4", durationMinutes: 30, availableFrom: iso(-hour), availableUntil: iso(-1) },
  { id: "a5", name: "Storia", status: 3, durationMinutes: 30, availableFrom: iso(-hour), availableUntil: iso(hour) },
  { id: "a6", name: "Fisica", durationMinutes: 30, availableFrom: iso(48 * hour), availableUntil: iso(50 * hour) },
];

beforeEach(() => {
  setStorageUser("u-student");
  clearUserStorage(); // notifiche viste e consegne dei test precedenti
  Object.assign(mockApi, {
    getAssigned: jest.fn().mockResolvedValue(assigned()),
    startExam: jest.fn().mockResolvedValue({ submissionId: "subNew", startedAt: iso(0), deadlineAt: iso(hour),
      exam: { name: "Reti", questions: [{ id: "q2", position: 1, type: 0, options: [] }, { id: "q1", position: 0, type: 2, options: [] }] } }),
    getSession: jest.fn().mockResolvedValue({ submissionId: "sub2", deadlineAt: iso(hour), currentRevision: 4,
      exam: { name: "Java", questions: [{ id: "qa", position: 0, type: 1, options: [] }, { id: "qb", position: 1, type: 2, options: [] }] },
      answers: [{ questionId: "qa", selectedOptionIds: ["o1", "o2"] }, { questionId: "qb", openText: "risposta" }] }),
    submitExpired: jest.fn().mockResolvedValue({ status: "graded", passed: false, grade: 4, totalScore: 4, maxScore: 10 }),
    getResult: jest.fn().mockResolvedValue({ status: "graded", passed: true, grade: 8, totalScore: 16, maxScore: 20, submittedAt: "2026-11-10T09:00:00Z" }),
  });
});
afterEach(() => setStorageUser(null));

test("stato di ogni esame e 'NUOVO' per quelli da svolgere non ancora visti", async () => {
  renderAs("student", <StudentExamsScreen />);
  await flush();
  ["Da svolgere", "NUOVO", "Riprendi l'esame", "Scaduto: tocca per consegnare", "Valutato", "Annullato", "60 +10 min"]
    .forEach(t => expect(screen.getByText(t)).toBeTruthy());
  expect(screen.getByText(/^Dal \d{2}\/\d{2}\/\d{4}, \d{2}:\d{2}$/)).toBeTruthy(); // non ancora disponibile
});

test("regole prima dell'inizio, poi avvio dell'esame a schermo bloccato", async () => {
  renderAs("student", <StudentExamsScreen />);
  await flush();
  fireEvent.press(screen.getByText("Reti"));
  expect(screen.getByText("Prima di iniziare")).toBeTruthy();
  expect(screen.getByText(/Hai 70 minuti/)).toBeTruthy();
  expect(screen.getByText("Senza appunti")).toBeTruthy();
  await act(async () => { fireEvent.press(screen.getByText("Inizia l'esame")); });
  expect(mockApi.startExam).toHaveBeenCalledWith("a1");
  expect(screen.getByText("TAKER subNew q1,q2")).toBeTruthy(); // domande in ordine
  expect(getRun("a1")).toMatchObject({ submissionId: "subNew", answers: {}, finished: false });
});

test("avvio non riuscito: avviso e si resta nell'elenco", async () => {
  mockApi.startExam.mockRejectedValue(Object.assign(new Error("x"), { status: 409 }));
  renderAs("student", <StudentExamsScreen />);
  await flush();
  fireEvent.press(screen.getByText("Reti"));
  await act(async () => { fireEvent.press(screen.getByText("Inizia l'esame")); });
  const { Alert } = require("react-native");
  expect(Alert.alert).toHaveBeenCalledWith("Impossibile iniziare", "L'esame non è disponibile in questo momento.");
});

test("'Indietro' dalle regole", async () => {
  renderAs("student", <StudentExamsScreen />);
  await flush();
  fireEvent.press(screen.getByText("Reti"));
  fireEvent.press(screen.getByText("Indietro"));
  expect(screen.getByText("I miei esami")).toBeTruthy();
});

test("ripresa da un altro dispositivo: risposte lette dalla sessione sul server", async () => {
  renderAs("student", <StudentExamsScreen />);
  await flush();
  await act(async () => { fireEvent.press(screen.getByText("Riprendi l'esame")); });
  expect(mockApi.getSession).toHaveBeenCalledWith("sub2");
  expect(screen.getByText("TAKER sub2 qa,qb")).toBeTruthy();
  expect(getRun("a2")).toMatchObject({ revision: 4, answers: { qa: ["o1", "o2"], qb: "risposta" } });
});

test("ripresa dopo un refresh: chiama sempre il server e ignora una copia locale diversa", async () => {
  saveRun("a2", { submissionId: "sub2", exam: { name: "Java", questions: [{ id: "local" }] }, answers: { local: "vecchia" }, revision: 99, finished: false });
  renderAs("student", <StudentExamsScreen />);
  await flush();
  await act(async () => { fireEvent.press(screen.getByText("Riprendi l'esame")); });
  expect(mockApi.getSession).toHaveBeenCalledWith("sub2");
  expect(screen.getByText("TAKER sub2 qa,qb")).toBeTruthy();
  expect(getRun("a2")).toMatchObject({ revision: 4, answers: { qa: ["o1", "o2"], qb: "risposta" } });
  expect(getRun("a2").answers.local).toBeUndefined();
});

test("ripresa: sessione non leggibile → errore con 'Riprova', mai la copia locale", async () => {
  saveRun("a2", { submissionId: "sub2", exam: { name: "Java", questions: [{ id: "local" }] }, answers: {}, finished: false });
  mockApi.getSession.mockRejectedValueOnce(Object.assign(new Error("Impossibile contattare il server."), { status: undefined }));
  renderAs("student", <StudentExamsScreen />);
  await flush();
  await act(async () => { fireEvent.press(screen.getByText("Riprendi l'esame")); });
  expect(screen.queryByText(/^TAKER/)).toBeNull();
  expect(screen.getByText("Impossibile contattare il server.")).toBeTruthy();
  await act(async () => { fireEvent.press(screen.getByText("Riprova")); });
  expect(mockApi.getSession).toHaveBeenCalledTimes(2);
  expect(screen.getByText("TAKER sub2 qa,qb")).toBeTruthy();
});

test("ripresa di una prova già consegnata (altro dispositivo): si mostra l'esito", async () => {
  mockApi.getSession.mockResolvedValueOnce({ submissionId: "sub2", status: 1, deadlineAt: iso(hour), exam: { questions: [] }, answers: [] });
  renderAs("student", <StudentExamsScreen />);
  await flush();
  await act(async () => { fireEvent.press(screen.getByText("Riprendi l'esame")); });
  expect(screen.queryByText(/^TAKER/)).toBeNull();
  expect(mockApi.getResult).toHaveBeenCalledWith("sub2");
  expect(screen.getByText("Esame superato")).toBeTruthy();
});

test("ripresa di una prova scaduta: si consegnano le risposte salvate e si mostra l'esito", async () => {
  mockApi.getSession.mockResolvedValueOnce({ submissionId: "sub2", status: 0, deadlineAt: iso(-1000), exam: { questions: [] }, answers: [] });
  renderAs("student", <StudentExamsScreen />);
  await flush();
  await act(async () => { fireEvent.press(screen.getByText("Riprendi l'esame")); });
  expect(screen.queryByText(/^TAKER/)).toBeNull();
  expect(mockApi.submitExpired).toHaveBeenCalledWith("sub2");
  expect(screen.getByText("Consegna automatica: tempo scaduto")).toBeTruthy();
});

test("fine dello svolgimento: pagina dell'esito", async () => {
  renderAs("student", <StudentExamsScreen />);
  await flush();
  fireEvent.press(screen.getByText("Reti"));
  await act(async () => { fireEvent.press(screen.getByText("Inizia l'esame")); });
  await act(async () => { fireEvent.press(screen.getByText(/^TAKER/)); });
  expect(screen.getByText("Esame consegnato")).toBeTruthy();
  expect(screen.getByText("In correzione")).toBeTruthy();
  expect(screen.getByText("Uscite dall'esame registrate: 1")).toBeTruthy();
  expect(getRun("a1")).toMatchObject({ finished: true });
});

test("esame scaduto: consegna con le risposte già salvate", async () => {
  renderAs("student", <StudentExamsScreen />);
  await flush();
  await act(async () => { fireEvent.press(screen.getByText("Inglese")); });
  expect(mockApi.submitExpired).toHaveBeenCalledWith("sub3");
  expect(screen.getByText("Consegna automatica: tempo scaduto")).toBeTruthy();
  expect(screen.getByText("Voto: 4")).toBeTruthy();
});

test("esito di un esame valutato, poi ritorno all'elenco", async () => {
  renderAs("student", <StudentExamsScreen />);
  await flush();
  await act(async () => { fireEvent.press(screen.getByText("Basi di dati")); });
  expect(mockApi.getResult).toHaveBeenCalledWith("sub4");
  ["Esame superato", "Punteggio: 16 / 20", "Voto: 8"].forEach(t => expect(screen.getByText(t)).toBeTruthy());
  await act(async () => { fireEvent.press(screen.getByText("Torna agli esami")); });
  expect(screen.getByText("I miei esami")).toBeTruthy();
});

test("nessun esame assegnato", async () => {
  mockApi.getAssigned.mockResolvedValue([]);
  renderAs("student", <StudentExamsScreen />);
  expect(await screen.findByText("Nessun esame assegnato")).toBeTruthy();
});

describe("esami assegnati e notifiche", () => {
  const wrapper = (role) => ({ children }) => (
    <AuthContext.Provider value={{ token: "tok", user: USERS[role] }}>{children}</AuthContext.Provider>
  );

  test("conta i nuovi esami da svolgere finché non sono visti", async () => {
    const { result } = renderHook(() => useStudentExams(), { wrapper: wrapper("student") });
    await waitFor(() => expect(result.current.loaded).toBe(true));
    expect(result.current.unseenCount).toBe(1);
    act(() => markSeen(["a1"]));
    expect(result.current.unseenCount).toBe(0);
    expect(result.current.isSeen("a1")).toBe(true);
  });

  test("errore: resta l'ultimo elenco noto con il messaggio", async () => {
    mockApi.getAssigned.mockRejectedValue(new Error("Rete assente"));
    const { result } = renderHook(() => useStudentExams(), { wrapper: wrapper("student") });
    await waitFor(() => expect(result.current.error).toBe("Rete assente"));
    expect(result.current.assigned).toEqual([]);
  });

  test("per gli altri ruoli non chiede nulla", async () => {
    renderHook(() => useStudentExams(), { wrapper: wrapper("teacher") });
    await flush();
    expect(mockApi.getAssigned).not.toHaveBeenCalled();
  });
});
