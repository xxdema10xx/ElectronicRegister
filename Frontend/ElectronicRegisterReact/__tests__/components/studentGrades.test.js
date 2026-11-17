// Pagina dei voti di uno studente (Studenti → studente): elenco, aggiunta, modifica, eliminazione.
import { fireEvent, screen, waitFor } from "@testing-library/react-native";
import StudentGrades from "../../src/components/StudentGrades";
import { flush, pressDialog, renderAs } from "../helpers/render";
import * as gradeService from "../../src/api/gradeService";
import * as subjectService from "../../src/api/subjectService";
import * as classService from "../../src/api/classService";

jest.mock("../../src/api/gradeService");
jest.mock("../../src/api/subjectService");
jest.mock("../../src/api/classService");

const student = { id: "s1", firstName: "Mario", lastName: "Rossi" };

beforeEach(() => {
  gradeService.getGradesPage.mockResolvedValue({ items: [
    { id: "g1", subjectId: "m1", value: 8, date: "2026-10-01" },
    { id: "g2", subjectId: "m2", subjectName: "Java", value: 4, date: "2026-11-02" },
  ] });
  subjectService.getSubjects.mockResolvedValue([{ id: "m1", name: "Reti" }, { id: "m2", name: "Java" }, { id: "m3", name: "Inglese" }]);
  classService.getStudentClassSubjects.mockResolvedValue([{ id: "m1", name: "Reti" }, { id: "m2", name: "Java" }]);
  gradeService.createGrade.mockResolvedValue("new");
  gradeService.updateGrade.mockResolvedValue();
  gradeService.deleteGrade.mockResolvedValue();
});

test("elenco dal più recente con materia e data", async () => {
  renderAs("admin", <StudentGrades student={student} token="tok" />);
  await flush();
  expect(screen.getByText("2 voti")).toBeTruthy();
  const titles = screen.getAllByText(/^(Reti|Java)$/).map(t => t.props.children);
  expect(titles).toEqual(["Java", "Reti"]);
  expect(screen.getByText("02/11/2026")).toBeTruthy();
  expect(gradeService.getGradesPage).toHaveBeenCalledWith({ pageNumber: 1, pageSize: 100, studentId: "s1" }, "tok");
});

test("nuovo voto: controlli su materia e valore, poi salvataggio", async () => {
  renderAs("admin", <StudentGrades student={student} token="tok" />);
  await flush();
  fireEvent.press(screen.getByText("Aggiungi voto"));
  fireEvent.press(screen.getByText("Salva"));
  expect(await screen.findByText("Seleziona la materia.")).toBeTruthy();
  await pressDialog("OK");

  fireEvent.press(screen.getByText("Seleziona materia…"));
  expect(screen.queryByText("Inglese")).toBeNull(); // solo le materie della classe
  fireEvent.press(screen.getAllByText("Reti").at(-1));
  expect(screen.getByText("Voto (0-30)")).toBeTruthy();
  fireEvent.changeText(screen.getAllByDisplayValue("")[0], "31"); // campo "Voto (0-30)"
  fireEvent.press(screen.getByText("Salva"));
  expect(await screen.findByText("Il voto deve essere un numero intero tra 0 e 30.")).toBeTruthy();
  await pressDialog("OK");

  fireEvent.changeText(screen.getAllByDisplayValue("31")[0], "24,5"); // solo interi
  fireEvent.press(screen.getByText("Salva"));
  expect(await screen.findByText("Il voto deve essere un numero intero tra 0 e 30.")).toBeTruthy();
  await pressDialog("OK");

  fireEvent.changeText(screen.getAllByDisplayValue("24,5")[0], "0");
  fireEvent.press(screen.getByText("Salva"));
  await waitFor(() => expect(gradeService.createGrade).toHaveBeenCalledWith(
    expect.objectContaining({ studentId: "s1", subjectId: "m1", value: 0 }), "tok"));
});

test("409 in creazione: materia non della classe", async () => {
  gradeService.createGrade.mockRejectedValue(Object.assign(new Error("conflict"), { status: 409 }));
  renderAs("admin", <StudentGrades student={student} token="tok" />);
  await flush();
  fireEvent.press(screen.getByText("Aggiungi voto"));
  fireEvent.press(screen.getByText("Seleziona materia…"));
  fireEvent.press(screen.getAllByText("Java").at(-1));
  fireEvent.changeText(screen.getAllByDisplayValue("")[0], "6");
  fireEvent.press(screen.getByText("Salva"));
  expect(await screen.findByText(/non è assegnata alla classe dello studente/)).toBeTruthy();
});

test("eliminazione con conferma (solo admin)", async () => {
  renderAs("admin", <StudentGrades student={student} token="tok" />);
  await flush();
  fireEvent.press(screen.getAllByTestId("icon-trash-outline")[0]);
  await pressDialog("Elimina");
  await waitFor(() => expect(gradeService.deleteGrade).toHaveBeenCalledWith("g2", "tok"));
});

test("segreteria: niente eliminazione", async () => {
  renderAs("staff", <StudentGrades student={student} token="tok" canDelete={false} />);
  await flush();
  expect(screen.queryAllByTestId("icon-trash-outline")).toHaveLength(0);
});

test("lo Staff non legge /subjects: materie dai filtri dei voti", async () => {
  subjectService.getSubjects.mockRejectedValue(new Error("403"));
  gradeService.getGradeFilters.mockResolvedValue({ subjects: [{ id: "m1", name: "Reti" }] });
  classService.getStudentClassSubjects.mockRejectedValue(new Error("403"));
  renderAs("staff", <StudentGrades student={student} token="tok" canDelete={false} />);
  await flush();
  expect(screen.getByText("Reti")).toBeTruthy();
});

test("accessibilità: icone di modifica ed eliminazione con etichetta", async () => {
  renderAs("admin", <StudentGrades student={student} token="tok" />);
  await flush();
  expect(screen.getByLabelText("Modifica voto di Java")).toBeTruthy();
  expect(screen.getByLabelText("Elimina voto di Java")).toBeTruthy();
});
