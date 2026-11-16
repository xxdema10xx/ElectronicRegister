// Esami lato docente/admin/segreteria: schede, ricerca, azioni di stato, dettaglio, recuperi, nuovo esame.
import { act, fireEvent, screen, waitFor } from "@testing-library/react-native";
import ExamsScreen from "../../src/screens/ExamsScreen";
import { setPageHeader, usePageHeader } from "../../src/navigation/examRoutes";
import { invalidateExamInsights } from "../../src/api/examInsights";
import { setStorageUser } from "../../src/utils/userStorage";
import { flush, pressDialog, renderAs } from "../helpers/render";

const mockApi = {};
jest.mock("../../src/hooks/useExamApi", () => ({ useExamApi: () => mockApi }));

const classSubjects = [
  { id: "cs1", classId: "c1", className: "SW-25A", subjectName: "Reti", label: "SW-25A · Reti" },
  { id: "cs2", classId: "c1", className: "SW-25A", subjectName: "Java", label: "SW-25A · Java" },
  { id: "cs3", classId: "c2", className: "SW-25B", subjectName: "Reti", label: "SW-25B · Reti" },
];
const base = { durationMinutes: 60, passingPercent: 60, totalPoints: 10, assignedStudentsCount: 2, availableFrom: "2026-11-20T08:00:00Z", availableUntil: "2026-11-20T10:00:00Z" };
const exams = [
  { ...base, id: "e1", status: 0, classSubjectId: "cs1" },
  { ...base, id: "e2", status: 1, classSubjectId: "cs2", publishedAt: "2026-11-10T08:00:00Z" },
  { ...base, id: "e3", status: 1, classSubjectId: "cs3", publishedAt: "2026-11-11T08:00:00Z" },
  { ...base, id: "e4", status: 2, classSubjectId: "cs1", closedAt: "2026-11-01T08:00:00Z" },
];

beforeEach(() => {
  invalidateExamInsights();
  setStorageUser("u-teacher");
  Object.assign(mockApi, {
    cacheKey: () => "exams-screen",
    listExams: jest.fn().mockResolvedValue(exams),
    getClassSubjects: jest.fn().mockResolvedValue(classSubjects),
    getExam: jest.fn(async (id) => ({ ...exams.find(e => e.id === id), questions: [], assignedStudents: [] })),
    publishExam: jest.fn().mockResolvedValue({}), closeExam: jest.fn().mockResolvedValue({}),
    cancelExam: jest.fn().mockResolvedValue({}), deleteExam: jest.fn().mockResolvedValue({}),
    listSubmissions: jest.fn().mockResolvedValue([]),
    getStudentsOfClassSubject: jest.fn().mockResolvedValue([]),
    createExam: jest.fn().mockResolvedValue("new-id"),
  });
});
afterEach(() => { setPageHeader(null); setStorageUser(null); });

// L'header della pagina (titolo e "←") lo mostra AppNav: qui lo si legge dal suo stato
function Header() {
  const h = usePageHeader();
  const { Text } = require("react-native");
  return h ? <Text onPress={h.onBack}>{`HEADER: ${h.title}`}</Text> : null;
}
const renderScreen = (role = "teacher") => renderAs(role, <><Header /><ExamsScreen /></>);

test("schede con i contatori; 'Attivi' all'inizio, dal più recente", async () => {
  renderScreen();
  await flush();
  expect(screen.getByLabelText("Bozze, 1")).toBeTruthy();
  expect(screen.getByLabelText("Attivi, 2")).toBeTruthy();
  expect(screen.getByLabelText("Storico, 1")).toBeTruthy();
  const titles = screen.getAllByText(/ · SW-25/).map(t => t.props.children);
  expect(titles).toEqual(["Reti · SW-25B", "Java · SW-25A"]);
  fireEvent.press(screen.getByLabelText("Storico, 1"));
  expect(screen.getByText("Chiuso")).toBeTruthy();
});

test("ricerca per materia o classe e filtro per classe", async () => {
  renderScreen();
  await flush();
  fireEvent.changeText(screen.getByPlaceholderText("Cerca per materia o classe…"), "java");
  expect(screen.getByLabelText("Attivi, 1")).toBeTruthy();
  expect(screen.getByLabelText("Bozze, 0")).toBeTruthy();
  fireEvent.changeText(screen.getByPlaceholderText("Cerca per materia o classe…"), "inglese");
  expect(screen.getByText("Nessun esame trovato per “inglese”.")).toBeTruthy();
  fireEvent.changeText(screen.getByPlaceholderText("Cerca per materia o classe…"), "");
  fireEvent.press(screen.getByLabelText(/Filtro classe/));
  fireEvent.press(screen.getByText("SW-25B"));
  expect(screen.getByLabelText("Attivi, 1")).toBeTruthy();
  expect(screen.getByLabelText("Storico, 0")).toBeTruthy();
});

test("pubblica una bozza dopo la conferma e ricarica l'elenco", async () => {
  renderScreen();
  await flush();
  fireEvent.press(screen.getByLabelText("Bozze, 1"));
  fireEvent.press(screen.getByLabelText("Pubblica"));
  expect(await screen.findByText(/Pubblicare “Reti · SW-25A”\?/)).toBeTruthy();
  await pressDialog("Pubblica");
  await waitFor(() => expect(mockApi.publishExam).toHaveBeenCalledWith("e1"));
  expect(mockApi.listExams).toHaveBeenCalledTimes(2);
});

test("chiudi e annulla un esame pubblicato; errore 409 con messaggio dell'azione", async () => {
  mockApi.cancelExam.mockRejectedValue(Object.assign(new Error("x"), { status: 409 }));
  renderScreen();
  await flush();
  fireEvent.press(screen.getAllByLabelText("Chiudi")[0]);
  await pressDialog("Chiudi");
  await waitFor(() => expect(mockApi.closeExam).toHaveBeenCalledWith("e3"));
  fireEvent.press(screen.getAllByLabelText("Annulla esame")[0]);
  await pressDialog("Annulla esame");
  expect(await screen.findByText(/almeno uno studente lo ha già iniziato/)).toBeTruthy();
});

test("elimina una bozza", async () => {
  renderScreen("admin");
  await flush();
  fireEvent.press(screen.getByLabelText("Bozze, 1"));
  fireEvent.press(screen.getByLabelText("Elimina"));
  await pressDialog("Elimina");
  await waitFor(() => expect(mockApi.deleteExam).toHaveBeenCalledWith("e1"));
});

test("segreteria: pubblica e chiude, ma non annulla né elimina", async () => {
  renderScreen("staff");
  await flush();
  expect(screen.getAllByLabelText("Chiudi")).toHaveLength(2);
  expect(screen.queryByLabelText("Annulla esame")).toBeNull();
  fireEvent.press(screen.getByLabelText("Bozze, 1"));
  expect(screen.getByLabelText("Pubblica")).toBeTruthy();
  expect(screen.queryByLabelText("Elimina")).toBeNull();
});

test("dettaglio dell'esame e ritorno all'elenco", async () => {
  renderScreen();
  await flush();
  fireEvent.press(screen.getByLabelText("Apri Java · SW-25A"));
  expect(await screen.findByText("HEADER: Java · SW-25A")).toBeTruthy();
  expect(mockApi.getExam).toHaveBeenCalledWith("e2");
  expect(screen.getByText(/0 domande/)).toBeTruthy();
  fireEvent.press(screen.getByText("HEADER: Java · SW-25A"));
  expect(await screen.findByLabelText("Attivi, 2")).toBeTruthy();
});

test("esame non più esistente: avviso e ritorno all'elenco", async () => {
  mockApi.getExam.mockRejectedValue(Object.assign(new Error("x"), { status: 404 }));
  renderScreen();
  await flush();
  fireEvent.press(screen.getByLabelText("Apri Java · SW-25A"));
  expect(await screen.findByText("Esame non trovato")).toBeTruthy();
});

test("consegne di un esame pubblicato", async () => {
  mockApi.listSubmissions.mockResolvedValue([{ id: "s1", studentFirstName: "Mario", studentLastName: "Rossi", status: 2 }]);
  renderScreen();
  await flush();
  fireEvent.press(screen.getAllByLabelText("Consegne")[0]);
  expect(await screen.findByText("Rossi Mario")).toBeTruthy();
  expect(screen.getByText("HEADER: Consegne")).toBeTruthy();
});

test("segreteria: apre la correzione ma non può assegnare punti o voto", async () => {
  mockApi.listSubmissions.mockResolvedValue([{ id: "s1", studentFirstName: "Mario", studentLastName: "Rossi", status: 2 }]);
  mockApi.getReview = jest.fn().mockResolvedValue({ status: 2, studentFirstName: "Mario", studentLastName: "Rossi", answers: [
    { answerId: "a1", questionText: "Spiega", questionType: 2, maxPoints: 5, openText: "Risposta" },
  ] });
  renderScreen("staff");
  await flush();
  fireEvent.press(screen.getAllByLabelText("Consegne")[0]);
  fireEvent.press(await screen.findByLabelText("Correggi Rossi Mario"));
  expect(await screen.findByText("Risposta")).toBeTruthy();
  expect(screen.queryByText("Assegna il voto")).toBeNull();
});

test("'Chi deve recuperare'", async () => {
  mockApi.listSubmissions.mockImplementation(async (id) => (id === "e2" ? [{ studentId: "s1", studentFirstName: "Mario", studentLastName: "Rossi", status: "failed_auto" }] : []));
  renderScreen();
  await flush();
  fireEvent.press(screen.getByText("Chi deve recuperare"));
  expect(await screen.findByText("Rossi Mario")).toBeTruthy();
  expect(screen.getByText("Deve recuperare")).toBeTruthy();
  expect(screen.getByText("HEADER: Chi deve recuperare")).toBeTruthy();
});

test("nuovo esame: scelta di classe e materia, poi l'editor", async () => {
  renderScreen();
  await flush();
  fireEvent.press(screen.getByText("Crea esame"));
  expect(await screen.findByText("Per quale classe e materia è l'esame?")).toBeTruthy();
  fireEvent.press(screen.getByText("Continua →")); // disattivato: niente scelto
  expect(screen.getByText("Per quale classe e materia è l'esame?")).toBeTruthy();
  fireEvent.press(screen.getByText("Es. SW-25A"));
  fireEvent.press(screen.getAllByText("SW-25A").at(-1));
  fireEvent.press(screen.getByText("Es. Programmazione"));
  fireEvent.press(screen.getAllByText("Java").at(-1));
  fireEvent.press(screen.getByText("Continua →"));
  expect(await screen.findByText("Nuovo esame")).toBeTruthy();
  expect(screen.getByText("SW-25A · Java")).toBeTruthy();
  expect(screen.getByText("HEADER: Java · SW-25A")).toBeTruthy();
});

test("nuovo esame su una classe con una sola materia: materia preselezionata", async () => {
  renderScreen();
  await flush();
  fireEvent.press(screen.getByText("Crea esame"));
  fireEvent.press(await screen.findByText("Es. SW-25A"));
  fireEvent.press(screen.getAllByText("SW-25B").at(-1));
  fireEvent.press(screen.getByText("Continua →"));
  expect(await screen.findByText("SW-25B · Reti")).toBeTruthy();
});

test("'Salva bozza' di un esame incompleto: resta solo sul dispositivo con l'elenco di cosa manca", async () => {
  renderScreen();
  await flush();
  fireEvent.press(screen.getByText("Crea esame"));
  fireEvent.press(await screen.findByText("Es. SW-25A"));
  fireEvent.press(screen.getAllByText("SW-25B").at(-1));
  fireEvent.press(screen.getByText("Continua →"));
  fireEvent.changeText(await screen.findByPlaceholderText(/Facoltativa/), "Prova");
  fireEvent.press(screen.getByLabelText("Salva bozza"));
  expect(await screen.findByText(/Non salvata: se esci, le modifiche si perdono/)).toBeTruthy();
  expect(mockApi.createExam).not.toHaveBeenCalled();
});

test("creazione completa: dettagli → domanda → studenti → 'Salva e chiudi' sul server", async () => {
  mockApi.getStudentsOfClassSubject.mockResolvedValue([{ id: "s1", firstName: "Mario", lastName: "Rossi" }]);
  renderScreen();
  await flush();
  fireEvent.press(screen.getByText("Crea esame"));
  fireEvent.press(await screen.findByText("Es. SW-25A"));
  fireEvent.press(screen.getAllByText("SW-25B").at(-1));
  fireEvent.press(screen.getByText("Continua →"));

  // 1. Dettagli
  const [from, until] = await screen.findAllByPlaceholderText("AAAA-MM-GG HH:mm");
  fireEvent.changeText(from, "2026-12-01 09:00");
  fireEvent.changeText(until, "2026-12-01 11:00");
  fireEvent.changeText(screen.getByPlaceholderText("Es. 45"), "60");
  fireEvent.press(screen.getByText("Avanti →"));

  // 2. Domande
  fireEvent.changeText(await screen.findByPlaceholderText(/Quale metodo HTTP/), "Metodo per creare?");
  fireEvent.changeText(screen.getByPlaceholderText(/Opzione 1/), "POST");
  fireEvent.changeText(screen.getByPlaceholderText(/Opzione 2/), "GET");
  fireEvent.press(screen.getAllByLabelText("Segna come giusta")[0]);
  fireEvent.press(screen.getByText("Avanti →"));

  // 3. Studenti
  fireEvent.press(await screen.findByText("Rossi Mario"));
  await act(async () => { fireEvent.press(screen.getByText("Salva e chiudi")); });

  expect(mockApi.createExam).toHaveBeenCalledWith({
    classSubjectId: "cs3", retakeOfExamId: null, description: null,
    availableFrom: "2026-12-01T08:00:00.000Z", availableUntil: "2026-12-01T10:00:00.000Z",
    durationMinutes: 60, passingPercent: 60,
    assignedStudents: [{ studentId: "s1", extraTimeMinutes: 0 }],
    questions: [{ position: 0, text: "Metodo per creare?", type: 0, points: 1, required: false,
      options: [{ position: 0, text: "POST", isCorrect: true }, { position: 1, text: "GET", isCorrect: false }] }],
  });
  expect(await screen.findByText("Esame salvato")).toBeTruthy();
});

test("'Annulla' nella scelta di classe e materia torna all'elenco", async () => {
  renderScreen();
  await flush();
  fireEvent.press(screen.getByText("Crea esame"));
  await screen.findByText("Per quale classe e materia è l'esame?");
  await act(async () => { fireEvent.press(screen.getByText("Annulla")); });
  expect(await screen.findByLabelText("Attivi, 2")).toBeTruthy();
});

test("studente non autorizzato all'editor: niente 'Crea esame'", async () => {
  renderScreen("student");
  await flush();
  expect(screen.queryByText("Crea esame")).toBeNull();
  expect(screen.queryByLabelText("Pubblica")).toBeNull();
});
