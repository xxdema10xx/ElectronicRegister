// Voti (registro e libretto dello studente), classi dell'admin (catalogo) e classi del docente.
import { Alert } from "react-native";
import { act, fireEvent, screen, waitFor } from "@testing-library/react-native";
import GradesScreen from "../../src/screens/GradesScreen";
import ClassesScreen from "../../src/screens/ClassesScreen";
import MyClassesScreen from "../../src/screens/MyClassesScreen";
import * as gradeService from "../../src/api/gradeService";
import * as subjectService from "../../src/api/subjectService";
import * as studentService from "../../src/api/studentService";
import * as teacherService from "../../src/api/teacherService";
import * as classService from "../../src/api/classService";
import * as studentExams from "../../src/data/studentExams";
import { flush, lastAlert, pressAlertButton, pressDialog, renderAs } from "../helpers/render";

jest.mock("../../src/api/gradeService");
jest.mock("../../src/api/subjectService");
jest.mock("../../src/api/studentService");
jest.mock("../../src/api/teacherService");
jest.mock("../../src/api/classService");
jest.mock("../../src/data/studentExams");
// "Voti alla classe" ha i suoi test: qui basta sapere che si apre e con quali dati
jest.mock("../../src/components/ClassGradesForm", () => {
  const { Text } = require("react-native");
  return { __esModule: true, default: ({ teacherId, classSubjects, students }) => (
    <Text>{`BATCH teacher=${teacherId || "-"} options=${(classSubjects || []).map(o => `${o.classSubjectId}:${o.label}`).join(",") || "-"} students=${(students || []).length}`}</Text>
  ) };
});

describe("Voti: registro (docente, admin, segreteria)", () => {
  beforeEach(() => {
    gradeService.getGradesPage.mockResolvedValue({ totalCount: 2, items: [
      { id: "g1", subjectName: "Reti", value: 8, date: "2026-11-02", student: { firstName: "Mario", lastName: "Rossi" }, description: "Verifica scritta" },
      { id: "g2", subjectId: "m2", value: 5, date: "2026-11-01", studentId: "s2" },
    ] });
    gradeService.getGradeFilters.mockResolvedValue({ subjects: [{ id: "m1", name: "Reti" }], students: [{ id: "s1", firstName: "Mario", lastName: "Rossi" }] });
    subjectService.getSubjects.mockResolvedValue([{ id: "m2", name: "Java" }]);
    studentService.getStudents.mockResolvedValue([{ id: "s2", firstName: "Giulia", lastName: "Bianchi" }]);
    teacherService.getTeachers.mockResolvedValue([]);
    gradeService.deleteGrade.mockResolvedValue();
    gradeService.updateGrade.mockResolvedValue();
  });

  test("elenco con materia e studente (anche dalle anagrafiche)", async () => {
    renderAs("teacher", <GradesScreen />);
    await flush();
    ["Reti", "Java", "👤 Mario Rossi", "👤 Giulia Bianchi", "📅 2026-11-02", "📝 Verifica scritta"].forEach(t => expect(screen.getByText(t)).toBeTruthy());
    expect(gradeService.getGradesPage).toHaveBeenCalledWith({ pageNumber: 1, pageSize: 20, subjectId: "", studentId: "", date: "" }, "tok");
  });

  test("filtri: materia scelta → nuova richiesta; 'Cancella filtri'", async () => {
    renderAs("admin", <GradesScreen />);
    await flush();
    fireEvent.press(screen.getByText("Filtri"));
    fireEvent.press(screen.getByText("Tutte le materie"));
    fireEvent.press(screen.getAllByText("Reti")[0]); // opzione della tendina (sta sopra l'elenco dei voti)
    await flush();
    expect(gradeService.getGradesPage).toHaveBeenLastCalledWith(expect.objectContaining({ subjectId: "m1" }), "tok");
    fireEvent.press(screen.getByText("✕ Cancella filtri"));
    await flush();
    expect(gradeService.getGradesPage).toHaveBeenLastCalledWith(expect.objectContaining({ subjectId: "" }), "tok");
  });

  test("modifica ed eliminazione (solo admin elimina)", async () => {
    renderAs("admin", <GradesScreen />);
    await flush();
    fireEvent.press(screen.getAllByLabelText("Modifica")[0]);
    await act(async () => { fireEvent.press(screen.getByText("Aggiorna")); });
    expect(gradeService.updateGrade).toHaveBeenCalledWith("g1", expect.objectContaining({ value: 8, date: "2026-11-02" }), "tok");
    fireEvent.press(screen.getAllByLabelText("Elimina")[1]);
    expect(lastAlert()[1]).toBe("Eliminare questo voto?");
    await pressAlertButton("Elimina");
    expect(gradeService.deleteGrade).toHaveBeenCalledWith("g2", "tok");
  });

  test("D-09: voto d'esame (source \"exam\") → 'da esame', senza Modifica né Elimina", async () => {
    gradeService.getGradesPage.mockResolvedValue({ totalCount: 2, items: [
      { id: "g1", subjectName: "Reti", value: 24, date: "2026-11-02", source: "exam", examId: "e1", submissionId: "sub1" },
      { id: "g2", subjectName: "Java", value: 18, date: "2026-11-01", source: "manual" },
    ] });
    renderAs("admin", <GradesScreen />);
    await flush();
    expect(screen.getByText("da esame")).toBeTruthy();
    expect(screen.getByLabelText("Voto da esame: si modifica dalla correzione dell'esame")).toBeTruthy();
    expect(screen.getAllByLabelText("Modifica")).toHaveLength(1);
    expect(screen.getAllByLabelText("Elimina")).toHaveLength(1);
    fireEvent.press(screen.getByLabelText("Elimina"));
    await pressAlertButton("Elimina");
    expect(gradeService.deleteGrade).toHaveBeenCalledWith("g2", "tok");
  });

  test("D-09: 409 su modifica ed eliminazione (server senza source) → voto generato da un esame", async () => {
    const conflict = () => Object.assign(new Error("Operazione non consentita nello stato attuale. Ricarica e riprova."), { status: 409 });
    gradeService.updateGrade.mockRejectedValue(conflict());
    gradeService.deleteGrade.mockRejectedValue(conflict());
    renderAs("admin", <GradesScreen />);
    await flush();
    fireEvent.press(screen.getAllByLabelText("Modifica")[0]);
    await act(async () => { fireEvent.press(screen.getByText("Aggiorna")); });
    expect(lastAlert()).toEqual(["Errore", expect.stringMatching(/generato da un esame \(si modifica dalla correzione/)]);
    fireEvent.press(screen.getAllByLabelText("Elimina")[1]);
    await pressAlertButton("Elimina");
    expect(lastAlert()).toEqual(["Errore", expect.stringMatching(/non si può eliminare: è un voto generato da un esame/)]);
  });

  test("modifica: voto intero 0–30, altrimenti nessuna richiesta", async () => {
    renderAs("staff", <GradesScreen />);
    await flush();
    fireEvent.press(screen.getAllByLabelText("Modifica")[0]);
    expect(screen.getByText("Nuovo voto (0-30)")).toBeTruthy();
    fireEvent.changeText(screen.getByDisplayValue("8"), "28,5");
    await act(async () => { fireEvent.press(screen.getByText("Aggiorna")); });
    expect(lastAlert()).toEqual(["Attenzione", "Il voto deve essere un numero intero tra 0 e 30."]);
    expect(gradeService.updateGrade).not.toHaveBeenCalled();
    fireEvent.changeText(screen.getByDisplayValue("28,5"), "28");
    await act(async () => { fireEvent.press(screen.getByText("Aggiorna")); });
    expect(gradeService.updateGrade).toHaveBeenCalledWith("g1", expect.objectContaining({ value: "28" }), "tok");
  });

  test("docente: modifica sì, eliminazione no", async () => {
    renderAs("teacher", <GradesScreen />);
    await flush();
    expect(screen.getAllByLabelText("Modifica")).toHaveLength(2);
    expect(screen.queryByLabelText("Elimina")).toBeNull();
  });

  test("altre pagine scorrendo in fondo", async () => {
    gradeService.getGradesPage
      .mockResolvedValueOnce({ totalCount: 3, items: [{ id: "g1", subjectName: "A", value: 6, date: "d" }, { id: "g2", subjectName: "B", value: 6, date: "d" }] })
      .mockResolvedValueOnce({ totalCount: 3, items: [{ id: "g3", subjectName: "C", value: 6, date: "d" }] });
    const { UNSAFE_getByType } = renderAs("admin", <GradesScreen />);
    await flush();
    const { FlatList } = require("react-native");
    await act(async () => { UNSAFE_getByType(FlatList).props.onEndReached(); });
    expect(gradeService.getGradesPage).toHaveBeenLastCalledWith(expect.objectContaining({ pageNumber: 2 }), "tok");
    expect(screen.getByText("C")).toBeTruthy();
  });
});

describe("Voti: libretto dello studente", () => {
  beforeEach(() => gradeService.getGradesPage.mockResolvedValue({ items: [] }));

  test("un esame per riga con voto o stato", async () => {
    studentExams.useStudentExams.mockReturnValue({ assigned: [], loaded: true, error: null });
    studentExams.useStudentBooklet.mockReturnValue([
      { item: { id: "e1", name: "Reti", submittedAt: "2026-11-10T10:00:00Z" }, r: { showGrade: true, grade: 24, passed: true } },
      { item: { id: "e2", name: "Java", isRetake: true }, r: { showGrade: false, title: "In correzione", status: "pending_review" } },
    ]);
    renderAs("student", <GradesScreen />);
    await flush();
    ["Libretto", "Reti", "24", "Superato", "Java · Recupero", "In correzione"].forEach(t => expect(screen.getByText(t)).toBeTruthy());
  });

  test("voti del registro con la descrizione, sotto gli esiti d'esame", async () => {
    studentExams.useStudentExams.mockReturnValue({ assigned: [], loaded: true, error: null });
    studentExams.useStudentBooklet.mockReturnValue([]);
    gradeService.getGradesPage.mockResolvedValue({ items: [
      { id: "r1", subjectName: "Reti", value: 24, date: "2026-11-17", description: "Verifica in presenza" },
    ] });
    renderAs("student", <GradesScreen />);
    await flush();
    expect(gradeService.getGradesPage).toHaveBeenCalledWith({ pageNumber: 1, pageSize: 100 }, "tok");
    ["Voti del registro", "24", "📝 Verifica in presenza", "Nessun esame svolto"].forEach(t => expect(screen.getByText(t)).toBeTruthy());
  });

  test("un voto d'esame compare solo come esito dell'esame, non anche tra i voti del registro", async () => {
    studentExams.useStudentExams.mockReturnValue({ assigned: [], loaded: true, error: null });
    studentExams.useStudentBooklet.mockReturnValue([
      { item: { id: "e1", name: "Reti", submittedAt: "2026-11-10T10:00:00Z" }, r: { showGrade: true, grade: 27, passed: true } },
    ]);
    gradeService.getGradesPage.mockResolvedValue({ items: [
      { id: "x1", subjectName: "Reti", value: 27, date: "2026-11-10", source: "exam", examId: "e1", submissionId: "sub1" },
      { id: "r1", subjectName: "Java", value: 20, date: "2026-11-17", source: "manual", description: "Orale" },
    ] });
    renderAs("student", <GradesScreen />);
    await flush();
    expect(screen.getAllByText("27")).toHaveLength(1);
    expect(screen.getByText("Voti del registro")).toBeTruthy();
    expect(screen.getByText("📝 Orale")).toBeTruthy();
  });

  test("solo voti d'esame: niente sezione 'Voti del registro'", async () => {
    studentExams.useStudentExams.mockReturnValue({ assigned: [], loaded: true, error: null });
    studentExams.useStudentBooklet.mockReturnValue([
      { item: { id: "e1", name: "Reti", submittedAt: "2026-11-10T10:00:00Z" }, r: { showGrade: true, grade: 27, passed: true } },
    ]);
    gradeService.getGradesPage.mockResolvedValue({ items: [{ id: "x1", subjectName: "Reti", value: 27, date: "2026-11-10", source: "exam" }] });
    renderAs("student", <GradesScreen />);
    await flush();
    expect(screen.queryByText("Voti del registro")).toBeNull();
  });

  test("nessun esame svolto", async () => {
    studentExams.useStudentExams.mockReturnValue({ assigned: [], loaded: true, error: "Rete assente" });
    studentExams.useStudentBooklet.mockReturnValue([]);
    renderAs("student", <GradesScreen />);
    await flush();
    expect(screen.getByText("Nessun esame svolto")).toBeTruthy();
    expect(screen.getByText("Rete assente")).toBeTruthy();
  });
});

describe("Classi (admin): biennio → area → corso → classe → dettaglio", () => {
  beforeEach(() => {
    classService.getActiveBienni.mockResolvedValue([{ id: "b1", startYear: "2025", endYear: "2027" }]);
    classService.getStudyAreas.mockResolvedValue([{ id: "a1", name: "ICT", description: "Informatica" }]);
    classService.getStudyPaths.mockResolvedValue([{ id: "p1", name: "Software developer" }]);
    classService.getClasses.mockResolvedValue([{ id: "c1", name: "SW-25A" }]);
    classService.getClassDetail.mockResolvedValue({
      id: "c1", name: "SW-25A",
      students: [{ id: "s1", firstName: "Mario", lastName: "Rossi" }],
      subjects: [{ classSubjectId: "cs1", subjectId: "m1", subjectName: "Reti", teacherId: "t1", teacherFirstName: "Anna", teacherLastName: "Neri" },
        { classSubjectId: "cs2", subjectId: "m2", subjectName: "Java", teacherId: "t1", teacherFirstName: "Anna", teacherLastName: "Neri" }],
    });
  });

  test("percorso completo e schede del dettaglio", async () => {
    renderAs("admin", <ClassesScreen />);
    fireEvent.press(await screen.findByText(/2025/));
    fireEvent.press(await screen.findByText("ICT"));
    expect(classService.getStudyPaths).toHaveBeenCalledWith("a1", "tok");
    fireEvent.press(await screen.findByText("Software developer"));
    fireEvent.press(await screen.findByText("SW-25A"));
    expect(await screen.findByText("Classe SW-25A")).toBeTruthy();
    expect(classService.getClassDetail).toHaveBeenCalledWith("c1", "SW-25A", "tok");
    expect(screen.getByText(/Insegnanti \(1\)/)).toBeTruthy();
    expect(screen.getByText("Reti · Java")).toBeTruthy();
    fireEvent.press(screen.getByText(/Studenti \(1\)/));
    expect(screen.getByText("Mario Rossi")).toBeTruthy();
    fireEvent.press(screen.getByText(/Materie \(2\)/));
    expect(screen.getAllByText("Prof. Anna Neri")).toHaveLength(2);
  });
});

describe("Classi del docente", () => {
  beforeEach(() => {
    classService.getMyClasses.mockResolvedValue([{
      id: "c1", name: "SW-25A",
      subjects: [{ subjectId: "m1", subjectName: "Reti" }],
      students: [{ id: "s1", firstName: "Mario", lastName: "Rossi" }, { id: "s2", firstName: "Giulia", lastName: "Bianchi" }],
    }]);
    gradeService.getGradesPage.mockResolvedValue({ items: [{ id: "g1", subjectId: "m1", value: 7, date: "2026-11-01" }] });
    gradeService.createGrade.mockResolvedValue("g2");
  });

  test("classi con conteggi, materie e studenti", async () => {
    renderAs("teacher", <MyClassesScreen />);
    expect(await screen.findByText("2 studenti · 1 materie")).toBeTruthy();
    expect(classService.getMyClasses).toHaveBeenCalledWith("t1", "tok");
    fireEvent.press(screen.getByText("SW-25A"));
    expect(screen.getByText("Classe SW-25A")).toBeTruthy();
    expect(screen.getByText("Reti")).toBeTruthy();
    fireEvent.press(screen.getByText(/Studenti \(2\)/));
    expect(screen.getByText("Giulia Bianchi")).toBeTruthy();
  });

  test("voti di uno studente e aggiunta di un voto", async () => {
    renderAs("teacher", <MyClassesScreen />);
    fireEvent.press(await screen.findByText("SW-25A"));
    fireEvent.press(screen.getByText(/Studenti \(2\)/));
    fireEvent.press(screen.getByText("Mario Rossi"));
    expect(await screen.findByText("2026-11-01")).toBeTruthy();
    fireEvent.press(screen.getByText("+ Voto"));
    fireEvent.press(screen.getByText("Seleziona materia…"));
    fireEvent.press(screen.getAllByText("Reti").at(-1));
    fireEvent.changeText(screen.getAllByDisplayValue("").at(0), "9");
    fireEvent.press(screen.getByText("Salva"));
    expect(await screen.findByText("Compila materia, voto e data.")).toBeTruthy(); // manca la data
    await pressDialog("OK");
    fireEvent.press(screen.getByText("Seleziona data…")); // selettore di sistema (iOS): conferma la data di oggi
    fireEvent.press(screen.getByText("Conferma"));
    await act(async () => { fireEvent.press(screen.getByText("Salva")); });
    await waitFor(() => expect(gradeService.createGrade).toHaveBeenCalledWith(expect.objectContaining({ studentId: "s1", subjectId: "m1", value: "9" }), "tok"));
  });

  test("voto d'esame dello studente: 'da esame' al posto di 'Modifica voto'", async () => {
    gradeService.getGradesPage.mockResolvedValue({ items: [
      { id: "g1", subjectId: "m1", value: 7, date: "2026-11-01" },
      { id: "g9", subjectId: "m1", value: 25, date: "2026-11-05", source: "exam", examId: "e1", submissionId: "sub1" },
    ] });
    renderAs("teacher", <MyClassesScreen />);
    fireEvent.press(await screen.findByText("SW-25A"));
    fireEvent.press(screen.getByText(/Studenti \(2\)/));
    fireEvent.press(screen.getByText("Mario Rossi"));
    expect(await screen.findByText("da esame")).toBeTruthy();
    expect(screen.getAllByLabelText("Modifica voto")).toHaveLength(1);
  });

  test("docente senza profilo collegato", async () => {
    classService.getMyClasses.mockResolvedValue([]);
    renderAs("teacher", <MyClassesScreen />, { user: { role: "teacher" } });
    expect(await screen.findByText(/non è collegato a un profilo docente/)).toBeTruthy();
  });
});

test("'Voti alla classe' dal registro (segreteria) e dalla classe del docente (solo le sue materie)", async () => {
  gradeService.getGradesPage.mockResolvedValue({ totalCount: 0, items: [] });
  gradeService.getGradeFilters.mockResolvedValue({ subjects: [], students: [] });
  subjectService.getSubjects.mockResolvedValue([]);
  studentService.getStudents.mockResolvedValue([]);
  teacherService.getTeachers.mockResolvedValue([]);
  const { unmount } = renderAs("staff", <GradesScreen />);
  await flush();
  fireEvent.press(screen.getByText("Voti alla classe"));
  expect(screen.getByText("BATCH teacher=- options=- students=0")).toBeTruthy();
  unmount();

  classService.getMyClasses.mockResolvedValue([{ id: "c1", name: "SW-25A",
    subjects: [{ classSubjectId: "cs1", subjectId: "m1", subjectName: "Reti" }],
    students: [{ id: "s1", firstName: "Mario", lastName: "Rossi" }] }]);
  renderAs("teacher", <MyClassesScreen />);
  fireEvent.press(await screen.findByText("SW-25A"));
  fireEvent.press(screen.getByText("Voti alla classe"));
  expect(screen.getByText("BATCH teacher=- options=cs1:SW-25A · Reti students=1")).toBeTruthy();
});
