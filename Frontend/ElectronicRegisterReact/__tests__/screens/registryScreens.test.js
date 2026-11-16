// Anagrafiche: studenti, materie, professori. Permessi per ruolo, ricerca, aggiunta, modifica, eliminazione.
import { Alert } from "react-native";
import { act, fireEvent, screen, waitFor } from "@testing-library/react-native";
import StudentsScreen from "../../src/screens/StudentsScreen";
import SubjectsScreen from "../../src/screens/SubjectsScreen";
import TeachersScreen from "../../src/screens/TeachersScreen";
import * as studentService from "../../src/api/studentService";
import * as subjectService from "../../src/api/subjectService";
import * as teacherService from "../../src/api/teacherService";
import { flush, lastAlert, pressAlertButton, renderAs } from "../helpers/render";

jest.mock("../../src/api/studentService");
jest.mock("../../src/api/subjectService");
jest.mock("../../src/api/teacherService");
jest.mock("../../src/api/accountService", () => ({ ...jest.requireActual("../../src/api/accountService"), createAccount: jest.fn() }));
const accountService = require("../../src/api/accountService");
// La pagina voti di uno studente ha i suoi test (components/studentGrades)
jest.mock("../../src/components/StudentGrades", () => {
  const { Text } = require("react-native");
  return ({ student }) => <Text>Voti di {student.firstName}</Text>;
});

const conflict = () => Object.assign(new Error("conflict"), { status: 409 });

describe("Studenti", () => {
  beforeEach(() => {
    studentService.getStudents.mockResolvedValue([
      { id: "s1", firstName: "Mario", lastName: "Rossi" }, { id: "s2", firstName: "Giulia", lastName: "Bianchi" },
    ]);
    studentService.createStudent.mockResolvedValue("s3");
    studentService.updateStudent.mockResolvedValue();
    studentService.deleteStudent.mockResolvedValue();
  });

  test("elenco e ricerca per nome o cognome", async () => {
    renderAs("admin", <StudentsScreen />);
    await flush();
    expect(screen.getByText("Mario Rossi")).toBeTruthy();
    fireEvent.changeText(screen.getByPlaceholderText("Cerca per nome o cognome…"), "bian");
    expect(screen.queryByText("Mario Rossi")).toBeNull();
    expect(screen.getByText("Giulia Bianchi")).toBeTruthy();
    fireEvent.changeText(screen.getByPlaceholderText("Cerca per nome o cognome…"), "zzz");
    expect(screen.getByText("Nessuno studente trovato")).toBeTruthy();
  });

  test("nuovo studente: nome e cognome obbligatori", async () => {
    renderAs("admin", <StudentsScreen />);
    await flush();
    fireEvent.press(screen.getByText("+ Studente"));
    fireEvent.press(screen.getByText("Salva"));
    expect(Alert.alert).toHaveBeenCalledWith("Attenzione", "Inserisci nome e cognome");
    const [nome, cognome] = screen.getAllByDisplayValue("").slice(-2); // dopo il campo di ricerca
    fireEvent.changeText(nome, "Luca");
    fireEvent.changeText(cognome, "Verdi");
    await act(async () => { fireEvent.press(screen.getByText("Salva")); });
    expect(studentService.createStudent).toHaveBeenCalledWith({ firstName: "Luca", lastName: "Verdi" }, "tok");
    expect(studentService.getStudents).toHaveBeenCalledTimes(2);
  });

  test("modifica", async () => {
    renderAs("staff", <StudentsScreen />);
    await flush();
    fireEvent.press(screen.getAllByLabelText("Modifica")[0]);
    fireEvent.changeText(screen.getByDisplayValue("Mario"), "Marco");
    await act(async () => { fireEvent.press(screen.getByText("Aggiorna")); });
    expect(studentService.updateStudent).toHaveBeenCalledWith("s1", { firstName: "Marco", lastName: "Rossi" }, "tok");
  });

  test("eliminazione con conferma; 409 se ha voti collegati", async () => {
    renderAs("admin", <StudentsScreen />);
    await flush();
    fireEvent.press(screen.getAllByLabelText("Elimina")[1]);
    expect(lastAlert()[1]).toBe("Eliminare studente?");
    await pressAlertButton("Elimina");
    expect(studentService.deleteStudent).toHaveBeenCalledWith("s2", "tok");

    studentService.deleteStudent.mockRejectedValue(conflict());
    fireEvent.press(screen.getAllByLabelText("Elimina")[0]);
    await pressAlertButton("Elimina");
    expect(Alert.alert).toHaveBeenLastCalledWith("Errore", "Lo studente non può essere eliminato: ha voti o altri dati collegati.");
  });

  test("account di accesso per la scheda: nome e ruolo fissi, collegato allo studente", async () => {
    accountService.createAccount.mockResolvedValue({ email: "mario.rossi@its.example", temporaryPassword: "Tmp-1" });
    renderAs("staff", <StudentsScreen />);
    await flush();
    fireEvent.press(screen.getAllByLabelText("Crea account di accesso")[0]);
    expect(screen.getByText(/collegato alla scheda di Mario Rossi/)).toBeTruthy();
    expect(screen.getByLabelText("Nome").props.editable).toBe(false);
    expect(screen.queryByText("Ruoli")).toBeNull();
    fireEvent.changeText(screen.getByLabelText("Codice fiscale"), "RSSMRA80A01H501U");
    fireEvent.changeText(screen.getByLabelText("Motivazione (resta nello storico)"), "Accesso al registro");
    await act(async () => { fireEvent.press(screen.getByText("Crea account")); });
    expect(accountService.createAccount).toHaveBeenCalledWith(expect.objectContaining({
      firstName: "Mario", lastName: "Rossi", roles: ["Student"], studentId: "s1", classId: null,
    }), "tok");
    expect(screen.getByText("Tmp-1")).toBeTruthy();
  });

  test("account per una scheda: nome diverso da quello registrato (422)", async () => {
    accountService.createAccount.mockRejectedValue(Object.assign(new Error("x"), { status: 422 }));
    renderAs("admin", <StudentsScreen />);
    await flush();
    fireEvent.press(screen.getAllByLabelText("Crea account di accesso")[0]);
    fireEvent.changeText(screen.getByLabelText("Codice fiscale"), "RSSMRA80A01H501U");
    fireEvent.changeText(screen.getByLabelText("Motivazione (resta nello storico)"), "x");
    await act(async () => { fireEvent.press(screen.getByText("Crea account")); });
    expect(screen.getByText(/non coincidono con quelli della scheda/)).toBeTruthy();
  });

  test("segreteria: modifica sì, eliminazione no", async () => {
    renderAs("staff", <StudentsScreen />);
    await flush();
    expect(screen.getAllByLabelText("Modifica")).toHaveLength(2);
    expect(screen.queryByLabelText("Elimina")).toBeNull();
  });

  test("docente: sola lettura", async () => {
    renderAs("teacher", <StudentsScreen />);
    await flush();
    expect(screen.queryByText("+ Studente")).toBeNull();
    expect(screen.queryByLabelText("Modifica")).toBeNull();
    expect(screen.queryByText("Clicca per vedere i voti")).toBeNull();
  });

  test("tocco sullo studente: pagina dei suoi voti", async () => {
    renderAs("admin", <StudentsScreen />);
    await flush();
    fireEvent.press(screen.getByText("Mario Rossi"));
    expect(await screen.findByText("Voti di Mario")).toBeTruthy();
  });

  test("errore di caricamento (non 404): avviso", async () => {
    studentService.getStudents.mockRejectedValue(Object.assign(new Error("Server giù"), { status: 500 }));
    renderAs("admin", <StudentsScreen />);
    await flush();
    expect(Alert.alert).toHaveBeenCalledWith("Errore", "Server giù");
  });
});

describe("Materie", () => {
  beforeEach(() => {
    subjectService.getSubjects.mockResolvedValue([{ id: "m1", name: "Reti" }, { id: "m2", name: "Basi di dati" }]);
    subjectService.createSubject.mockResolvedValue("m3");
    subjectService.updateSubject.mockResolvedValue();
    subjectService.deleteSubject.mockResolvedValue();
  });

  test("admin: aggiunta con nome duplicato (409) e senza nome", async () => {
    renderAs("admin", <SubjectsScreen />);
    await flush();
    fireEvent.press(screen.getByText("+ Materia"));
    fireEvent.press(screen.getByText("Salva"));
    expect(Alert.alert).toHaveBeenLastCalledWith("Attenzione", "Inserisci il nome della materia");
    subjectService.createSubject.mockRejectedValueOnce(conflict());
    fireEvent.changeText(screen.getAllByDisplayValue("").at(-1), "Reti");
    await act(async () => { fireEvent.press(screen.getByText("Salva")); });
    expect(Alert.alert).toHaveBeenLastCalledWith("Errore", "Esiste già una materia con questo nome.");
  });

  test("admin: modifica ed eliminazione (409 se assegnata a classi)", async () => {
    renderAs("admin", <SubjectsScreen />);
    await flush();
    fireEvent.press(screen.getAllByLabelText("Modifica")[0]);
    fireEvent.changeText(screen.getByDisplayValue("Reti"), "Reti di calcolatori");
    await act(async () => { fireEvent.press(screen.getByText("Aggiorna")); });
    expect(subjectService.updateSubject).toHaveBeenCalledWith("m1", { name: "Reti di calcolatori" }, "tok");

    subjectService.deleteSubject.mockRejectedValue(conflict());
    fireEvent.press(screen.getAllByLabelText("Elimina")[1]);
    await pressAlertButton("Elimina");
    expect(Alert.alert).toHaveBeenLastCalledWith("Errore", expect.stringMatching(/assegnata a una o più classi/));
  });

  test("studente: sola lettura con ricerca", async () => {
    renderAs("student", <SubjectsScreen />);
    await flush();
    expect(screen.queryByText("+ Materia")).toBeNull();
    expect(screen.queryByLabelText("Modifica")).toBeNull();
    fireEvent.changeText(screen.getByPlaceholderText("Cerca per nome…"), "basi");
    expect(screen.queryByText("Reti")).toBeNull();
    expect(screen.getByText("Basi di dati")).toBeTruthy();
  });
});

describe("Professori", () => {
  beforeEach(() => {
    teacherService.getTeachers.mockResolvedValue([{ id: "t1", firstName: "Anna", lastName: "Neri" }]);
    teacherService.createTeacher.mockResolvedValue("t2");
    teacherService.updateTeacher.mockResolvedValue();
    teacherService.deleteTeacher.mockResolvedValue();
  });

  test("elenco con 'Prof.'", async () => {
    renderAs("student", <TeachersScreen />);
    await flush();
    expect(screen.getByText("Prof. Anna Neri")).toBeTruthy();
    expect(screen.queryByText("+ Professore")).toBeNull();
  });

  test("admin: aggiunta, modifica, eliminazione", async () => {
    renderAs("admin", <TeachersScreen />);
    await flush();
    fireEvent.press(screen.getByText("+ Professore"));
    const [nome, cognome] = screen.getAllByDisplayValue("");
    fireEvent.changeText(nome, "Paolo");
    fireEvent.changeText(cognome, "Gialli");
    await act(async () => { fireEvent.press(screen.getByText("Salva")); });
    expect(teacherService.createTeacher).toHaveBeenCalledWith({ firstName: "Paolo", lastName: "Gialli" }, "tok");

    fireEvent.press(screen.getByLabelText("Modifica"));
    await act(async () => { fireEvent.press(screen.getByText("Aggiorna")); });
    expect(teacherService.updateTeacher).toHaveBeenCalledWith("t1", { firstName: "Anna", lastName: "Neri" }, "tok");

    fireEvent.press(screen.getByLabelText("Elimina"));
    await pressAlertButton(lastAlert()[2].at(-1).text);
    await waitFor(() => expect(teacherService.deleteTeacher).toHaveBeenCalledWith("t1", "tok"));
  });

  test("account di accesso per il docente", async () => {
    accountService.createAccount.mockResolvedValue({ email: "anna.neri@its.example", temporaryPassword: "Tmp-2" });
    renderAs("staff", <TeachersScreen />);
    await flush();
    fireEvent.press(screen.getByLabelText("Crea account di accesso"));
    fireEvent.changeText(screen.getByLabelText("Codice fiscale"), "NREANN80A41H501X");
    fireEvent.changeText(screen.getByLabelText("Motivazione (resta nello storico)"), "Nuovo docente");
    await act(async () => { fireEvent.press(screen.getByText("Crea account")); });
    expect(accountService.createAccount).toHaveBeenCalledWith(expect.objectContaining({ roles: ["Teacher"], teacherId: "t1" }), "tok");
  });

  test("segreteria: modifica ma non elimina", async () => {
    renderAs("staff", <TeachersScreen />);
    await flush();
    expect(screen.getByLabelText("Modifica")).toBeTruthy();
    expect(screen.queryByLabelText("Elimina")).toBeNull();
  });
});
