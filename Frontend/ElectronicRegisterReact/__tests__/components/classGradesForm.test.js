// "Voti alla classe": tabella degli studenti, validazione per riga, una sola richiesta batch, eventId stabile tra i
// retry dello stesso payload e nuovo dopo una correzione, errori per riga, 404, 409, guardia "modifiche non salvate".
import { act, fireEvent, screen } from "@testing-library/react-native";
import ClassGradesForm, { OPTIONS_UNAVAILABLE, ROW_INVALID } from "../../src/components/ClassGradesForm";
import * as batch from "../../src/api/gradeBatchService";
import { flush, pressDialog, renderAs } from "../helpers/render";

jest.mock("../../src/api/gradeBatchService", () => ({
  ...jest.requireActual("../../src/api/gradeBatchService"),
  getGradeClassSubjects: jest.fn(),
  getClassStudents: jest.fn(),
  createGradesBatch: jest.fn(),
  newBatchEventId: jest.fn(),
}));

const STUDENTS = [
  { id: "s1", firstName: "Mario", lastName: "Rossi" },
  { id: "s2", firstName: "Anna", lastName: "Bianchi" },
  { id: "s3", firstName: "Luca", lastName: "Verdi" },
];
const cell = (name) => screen.getByLabelText(`Voto di ${name}`);
const conflict = (status, extra) => Object.assign(new Error("errore"), { status, ...extra });

let ids;
beforeEach(() => {
  ids = 0;
  batch.newBatchEventId.mockImplementation(() => `ev-${++ids}`);
  batch.getGradeClassSubjects.mockResolvedValue([
    { classSubjectId: "cs1", classId: "c1", label: "SW-25A · Reti" },
    { classSubjectId: "cs2", classId: "c2", label: "SW-25B · Java" },
  ]);
  batch.getClassStudents.mockResolvedValue(STUDENTS);
  batch.createGradesBatch.mockResolvedValue({ eventId: "ev-1", created: [{ studentId: "s1" }, { studentId: "s3" }] });
});

async function open(props = {}) {
  const onClose = jest.fn();
  const onSaved = jest.fn();
  renderAs("teacher", <ClassGradesForm visible token="tok" teacherId="t1" onClose={onClose} onSaved={onSaved} {...props} />);
  await flush();
  if (!props.classSubjects) {
    fireEvent.press(screen.getByText("Scegli classe e materia…"));
    fireEvent.press(screen.getByText("SW-25A · Reti"));
    await flush();
  }
  return { onClose, onSaved };
}
const save = () => act(async () => { fireEvent.press(screen.getByText("Salva")); });

test("tutti gli studenti della classe; celle vuote non inviate; una sola richiesta; 'Salvati N voti'", async () => {
  const { onClose, onSaved } = await open();
  expect(batch.getGradeClassSubjects).toHaveBeenCalledWith("tok", { teacherId: "t1" });
  expect(batch.getClassStudents).toHaveBeenCalledWith("c1", "tok");
  ["Rossi Mario", "Bianchi Anna", "Verdi Luca"].forEach(n => expect(screen.getByText(n)).toBeTruthy());
  expect(screen.getByText("0 voti da salvare")).toBeTruthy();
  fireEvent.changeText(cell("Rossi Mario"), "24");
  fireEvent.changeText(cell("Verdi Luca"), "17");
  fireEvent.changeText(screen.getByPlaceholderText("Es. Verifica scritta in presenza"), "Verifica in presenza");
  expect(screen.getByText("2 voti da salvare")).toBeTruthy();
  await save();
  expect(batch.createGradesBatch).toHaveBeenCalledTimes(1);
  expect(batch.createGradesBatch).toHaveBeenCalledWith({
    eventId: "ev-1", classSubjectId: "cs1", date: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/), description: "Verifica in presenza",
    grades: [{ studentId: "s1", value: 24 }, { studentId: "s3", value: 17 }],
  }, "tok");
  expect(await screen.findByText("Salvati 2 voti.")).toBeTruthy();
  expect(onSaved).toHaveBeenCalledWith(2);
  expect(onClose).toHaveBeenCalled();
});

test("validazione in tempo reale per riga: 'Salva' disattivato finché c'è un errore", async () => {
  await open();
  fireEvent.changeText(cell("Rossi Mario"), "31");
  fireEvent.changeText(cell("Bianchi Anna"), "18");
  expect(screen.getByText(ROW_INVALID)).toBeTruthy();
  expect(screen.getByText("1 voto da salvare")).toBeTruthy();
  await save();
  expect(batch.createGradesBatch).not.toHaveBeenCalled();
  fireEvent.changeText(cell("Rossi Mario"), "2,5");
  expect(screen.getByText(ROW_INVALID)).toBeTruthy();
  fireEvent.changeText(cell("Rossi Mario"), "30");
  expect(screen.queryByText(ROW_INVALID)).toBeNull();
  await save();
  expect(batch.createGradesBatch).toHaveBeenCalledTimes(1);
});

test("nessun voto compilato: niente invio", async () => {
  await open();
  await save();
  expect(batch.createGradesBatch).not.toHaveBeenCalled();
});

test("retry dello stesso payload (risposta persa): stesso eventId", async () => {
  batch.createGradesBatch.mockRejectedValueOnce(conflict(undefined, { message: "Il server non risponde, riprova" }));
  await open();
  fireEvent.changeText(cell("Rossi Mario"), "24");
  await save();
  expect(await screen.findByText("Il server non risponde, riprova")).toBeTruthy();
  await pressDialog("OK");
  await save();
  expect(batch.createGradesBatch.mock.calls.map(c => c[0].eventId)).toEqual(["ev-1", "ev-1"]);
});

test("400 con errori per riga: righe evidenziate, nulla salvato; dopo la correzione nuovo eventId", async () => {
  batch.createGradesBatch.mockRejectedValueOnce(conflict(400, { code: "GradeBatchInvalid", errors: [
    { index: 1, studentId: "s3", code: "student_not_in_class", message: "x" },
  ] }));
  const { onClose } = await open();
  fireEvent.changeText(cell("Rossi Mario"), "24");
  fireEvent.changeText(cell("Verdi Luca"), "20");
  await save();
  expect(await screen.findByText(/Nessun voto è stato salvato/)).toBeTruthy();
  await pressDialog("OK");
  expect(screen.getByText("Lo studente non fa parte di questa classe.")).toBeTruthy();
  expect(onClose).not.toHaveBeenCalled();
  // con l'errore del server la riga blocca il salvataggio
  await save();
  expect(batch.createGradesBatch).toHaveBeenCalledTimes(1);
  // correzione: si toglie il voto della riga sbagliata → payload diverso → nuovo eventId
  fireEvent.changeText(cell("Verdi Luca"), "");
  expect(screen.queryByText("Lo studente non fa parte di questa classe.")).toBeNull();
  await save();
  expect(batch.createGradesBatch).toHaveBeenCalledTimes(2);
  expect(batch.createGradesBatch.mock.calls[1][0]).toMatchObject({ eventId: "ev-2", grades: [{ studentId: "s1", value: 24 }] });
});

test("404 della classe-materia (non più esistente o di un altro docente)", async () => {
  batch.createGradesBatch.mockRejectedValueOnce(conflict(404));
  const { onClose } = await open();
  fireEvent.changeText(cell("Rossi Mario"), "24");
  await save();
  expect(await screen.findByText(/non esiste più oppure non è tra quelle che insegni/)).toBeTruthy();
  expect(onClose).not.toHaveBeenCalled();
});

test("409 con lo stesso payload: si ricaricano i voti e si avvisa", async () => {
  batch.createGradesBatch.mockRejectedValueOnce(conflict(409));
  const { onSaved } = await open();
  fireEvent.changeText(cell("Rossi Mario"), "24");
  await save();
  expect(onSaved).toHaveBeenCalledWith(0);
  expect(await screen.findByText(/potrebbe essere già stato salvato/)).toBeTruthy();
});

test("classi-materia passate dal docente: studenti già noti, nessuna lettura", async () => {
  await open({ classSubjects: [{ classSubjectId: "cs9", classId: "c9", label: "SW-25A · Reti" }], students: STUDENTS.slice(0, 1) });
  expect(batch.getGradeClassSubjects).not.toHaveBeenCalled();
  expect(batch.getClassStudents).not.toHaveBeenCalled();
  fireEvent.changeText(cell("Rossi Mario"), "18");
  await save();
  expect(batch.createGradesBatch).toHaveBeenCalledWith(expect.objectContaining({ classSubjectId: "cs9" }), "tok");
});

test("segreteria: tutte le classi-materia dell'istituto, senza filtro per docente; salva il blocco", async () => {
  renderAs("staff", <ClassGradesForm visible token="tok" onClose={jest.fn()} onSaved={jest.fn()} />);
  await flush();
  expect(batch.getGradeClassSubjects).toHaveBeenCalledWith("tok", { teacherId: undefined });
  fireEvent.press(screen.getByText("Scegli classe e materia…"));
  expect(screen.getByText("SW-25A · Reti")).toBeTruthy();
  fireEvent.press(screen.getByText("SW-25B · Java"));
  await flush();
  expect(batch.getClassStudents).toHaveBeenCalledWith("c2", "tok");
  fireEvent.changeText(cell("Bianchi Anna"), "26");
  await save();
  expect(batch.createGradesBatch).toHaveBeenCalledWith(expect.objectContaining({ classSubjectId: "cs2", grades: [{ studentId: "s2", value: 26 }] }), "tok");
});

test("classi-materia non leggibili dal ruolo (403) → avviso, niente modulo", async () => {
  batch.getGradeClassSubjects.mockResolvedValue(null);
  renderAs("staff", <ClassGradesForm visible token="tok" onClose={jest.fn()} />);
  await flush();
  expect(screen.getByText(OPTIONS_UNAVAILABLE)).toBeTruthy();
  expect(screen.queryByText("Salva")).toBeNull();
});

test("modifiche non salvate: chiudere chiede conferma", async () => {
  const { onClose } = await open();
  fireEvent.changeText(cell("Rossi Mario"), "24");
  fireEvent.press(screen.getByText("Annulla"));
  expect(await screen.findByText("Modifiche non salvate")).toBeTruthy();
  await pressDialog("Continua a modificare");
  expect(onClose).not.toHaveBeenCalled();
  fireEvent.press(screen.getByText("Annulla"));
  await pressDialog("Esci senza salvare");
  expect(onClose).toHaveBeenCalled();
});

test("senza modifiche si chiude subito", async () => {
  const { onClose } = await open();
  fireEvent.press(screen.getByText("Annulla"));
  expect(onClose).toHaveBeenCalled();
});
