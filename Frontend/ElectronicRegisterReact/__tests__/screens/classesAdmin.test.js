// Classi (admin): preparare l'istituto. Biennio, area, corso e classe a ogni livello; materie della classe con docente.
import { act, fireEvent, screen, waitFor } from "@testing-library/react-native";
import ClassesScreen from "../../src/screens/ClassesScreen";
import * as classService from "../../src/api/classService";
import * as subjectService from "../../src/api/subjectService";
import * as teacherService from "../../src/api/teacherService";
import { flush, pressDialog, renderAs } from "../helpers/render";

jest.mock("../../src/api/classService");
jest.mock("../../src/api/subjectService");
jest.mock("../../src/api/teacherService");

const conflict = () => Object.assign(new Error("x"), { status: 409 });

beforeEach(() => {
  classService.getActiveBienni.mockResolvedValue([{ id: "b1", startYear: "2025", endYear: "2027" }]);
  classService.getStudyAreas.mockResolvedValue([{ id: "ba1", studyAreaId: "a1", name: "ICT" }]);
  classService.getStudyPaths.mockResolvedValue([{ id: "bp1", studyPathId: "p1", name: "Software developer" }]);
  classService.getClasses.mockResolvedValue([{ id: "c1", name: "SW-25A" }]);
  classService.getClassDetail.mockResolvedValue({
    id: "c1", name: "SW-25A", students: [],
    subjects: [{ classSubjectId: "cs1", subjectId: "m1", subjectName: "Reti", teacherId: "t1", teacherFirstName: "Anna", teacherLastName: "Neri", revision: 3 }],
  });
  classService.getAllStudyAreas.mockResolvedValue([{ id: "a1", name: "ICT" }, { id: "a2", name: "Turismo" }]);
  classService.getAllStudyPaths.mockResolvedValue([]);
  for (const f of ["createBiennium", "createStudyArea", "createStudyPath", "linkStudyArea", "linkStudyPath", "createClass", "addClassSubject", "changeClassSubjectTeacher", "removeClassSubject"]) {
    classService[f].mockResolvedValue("new-id");
  }
  subjectService.getSubjects.mockResolvedValue([{ id: "m1", name: "Reti" }, { id: "m2", name: "Java" }]);
  teacherService.getTeachers.mockResolvedValue([{ id: "t1", firstName: "Anna", lastName: "Neri" }, { id: "t2", firstName: "Paolo", lastName: "Gialli" }]);
});

const open = async (...labels) => { for (const l of labels) fireEvent.press(await screen.findByText(l)); };

test("nuovo biennio con le date di inizio e fine", async () => {
  renderAs("admin", <ClassesScreen />);
  await flush();
  fireEvent.press(screen.getByText("+ Biennio"));
  fireEvent.press(screen.getByText("Salva"));
  expect(screen.getByText("Indica l'inizio e la fine del biennio.")).toBeTruthy();
  // Selettore di sistema (iOS): "Conferma" sceglie la data mostrata, oggi
  fireEvent.press(screen.getByText("Data di inizio"));
  fireEvent.press(screen.getByText("Conferma"));
  fireEvent.press(screen.getByText("Data di fine"));
  fireEvent.press(screen.getByText("Conferma"));
  await act(async () => { fireEvent.press(screen.getByText("Salva")); });
  const today = new Date();
  const iso = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;
  expect(classService.createBiennium).toHaveBeenCalledWith({ startDate: iso, endDate: iso }, "tok");
  expect(classService.getActiveBienni).toHaveBeenCalledTimes(2); // elenco ricaricato
});

test("area: si riusa un'area dell'istituto non ancora collegata al biennio", async () => {
  renderAs("admin", <ClassesScreen />);
  await open(/2025/);
  fireEvent.press(await screen.findByText("+ Area"));
  fireEvent.press(await screen.findByText("Scegli un'area…"));
  expect(screen.queryAllByText("ICT").length).toBe(1); // solo nell'elenco: già collegata, non proposta
  fireEvent.press(screen.getByText("Turismo"));
  await act(async () => { fireEvent.press(screen.getByText("Salva")); });
  expect(classService.linkStudyArea).toHaveBeenCalledWith("b1", "a2", "tok");
  expect(classService.createStudyArea).not.toHaveBeenCalled();
});

test("area nuova: si crea e si collega", async () => {
  classService.createStudyArea.mockResolvedValue("a9");
  renderAs("admin", <ClassesScreen />);
  await open(/2025/);
  fireEvent.press(await screen.findByText("+ Area"));
  fireEvent.press(await screen.findByText("Scegli un'area…"));
  fireEvent.press(screen.getByText("+ Nuova area"));
  fireEvent.changeText(screen.getByLabelText("Nome"), "Meccatronica");
  await act(async () => { fireEvent.press(screen.getByText("Salva")); });
  expect(classService.createStudyArea).toHaveBeenCalledWith({ name: "Meccatronica", description: undefined }, "tok");
  expect(classService.linkStudyArea).toHaveBeenCalledWith("b1", "a9", "tok");
});

test("corso: senza corsi da riusare si crea direttamente", async () => {
  classService.createStudyPath.mockResolvedValue("p9");
  renderAs("admin", <ClassesScreen />);
  await open(/2025/, "ICT");
  fireEvent.press(await screen.findByText("+ Corso"));
  fireEvent.changeText(await screen.findByLabelText("Nome"), "Cloud");
  await act(async () => { fireEvent.press(screen.getByText("Salva")); });
  expect(classService.linkStudyPath).toHaveBeenCalledWith("ba1", "p9", "tok");
});

test("classe: nome da 1 a 20 caratteri; duplicato (409)", async () => {
  classService.createClass.mockRejectedValueOnce(conflict());
  renderAs("admin", <ClassesScreen />);
  await open(/2025/, "ICT", "Software developer");
  fireEvent.press(await screen.findByText("+ Classe"));
  fireEvent.press(screen.getByText("Salva"));
  expect(screen.getByText("Il nome della classe deve avere da 1 a 20 caratteri.")).toBeTruthy();
  fireEvent.changeText(screen.getByLabelText("Nome della classe"), "SW-25A");
  await act(async () => { fireEvent.press(screen.getByText("Salva")); });
  expect(classService.createClass).toHaveBeenCalledWith({ name: "SW-25A", bienniumStudyPathId: "bp1" }, "tok");
  expect(screen.getByText(/Esiste già/)).toBeTruthy();
});

describe("materie della classe", () => {
  const openDetail = async () => {
    renderAs("admin", <ClassesScreen />);
    await open(/2025/, "ICT", "Software developer", "SW-25A");
    fireEvent.press(await screen.findByText(/Materie \(1\)/));
  };

  test("aggiungi una materia con il suo docente (solo materie non ancora assegnate)", async () => {
    await openDetail();
    fireEvent.press(screen.getByText("+ Materia"));
    fireEvent.press(await screen.findByText("Scegli la materia…"));
    expect(screen.queryAllByText("Reti")).toHaveLength(1); // già assegnata: solo nell'elenco
    fireEvent.press(screen.getByText("Java"));
    fireEvent.press(screen.getByText("Scegli il docente…"));
    fireEvent.press(screen.getByText("Gialli Paolo"));
    await act(async () => { fireEvent.press(screen.getByText("Salva")); });
    expect(classService.addClassSubject).toHaveBeenCalledWith({ classId: "c1", subjectId: "m2", teacherId: "t2" }, "tok");
  });

  test("cambia docente con la revisione letta (If-Match)", async () => {
    await openDetail();
    fireEvent.press(screen.getByLabelText("Cambia docente"));
    expect(await screen.findByText("Docente di Reti")).toBeTruthy();
    fireEvent.press(screen.getByText("Neri Anna"));
    fireEvent.press(screen.getByText("Gialli Paolo"));
    await act(async () => { fireEvent.press(screen.getByText("Salva")); });
    expect(classService.changeClassSubjectTeacher).toHaveBeenCalledWith("cs1", "t2", 3, "tok");
  });

  test("togli una materia con conferma; bloccato se ci sono esami o voti", async () => {
    classService.removeClassSubject.mockRejectedValueOnce(conflict());
    await openDetail();
    fireEvent.press(screen.getByLabelText("Togli materia"));
    await pressDialog("Togli");
    expect(await screen.findByText(/ci sono esami o voti collegati/)).toBeTruthy();
    await pressDialog("OK");
    fireEvent.press(screen.getByLabelText("Togli materia"));
    await pressDialog("Togli");
    await waitFor(() => expect(classService.removeClassSubject).toHaveBeenCalledTimes(2));
    expect(classService.getClassDetail.mock.calls.length).toBeGreaterThan(1);
  });
});
