// Editor guidato degli esami: Dettagli → Domande → Studenti.
import { useState } from "react";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react-native";
import TestBuilder, { newTestDraft } from "../../src/components/TestBuilder";

const students = [
  { id: "s1", firstName: "Mario", lastName: "Rossi" },
  { id: "s2", firstName: "Giulia", lastName: "Bianchi" },
  { id: "s3", firstName: "Luca", lastName: "Verdi" },
];

function setup(draftPatch = {}, { env: envPatch = {}, ...props } = {}) {
  let current;
  const env = {
    classSubjects: [{ id: "cs1", classId: "c1", label: "SW-25A · Reti" }], classSubjectLabel: "SW-25A · Reti",
    draftName: "Reti · SW-25A", retakeOptions: [], onChangeClassSubject: jest.fn(),
    getStudents: jest.fn().mockResolvedValue(students), getRetakeCandidates: jest.fn(), ...envPatch,
  };
  const handlers = { onSaveDraft: jest.fn(), onSaveAndClose: jest.fn(), ...props };
  function Harness() {
    const [draft, setDraft] = useState({ ...newTestDraft(), classSubjectId: "cs1", ...draftPatch });
    current = draft;
    return <TestBuilder draft={draft} onChange={setDraft} env={env} draftStatus="Bozza" draftStatusShort={{ label: "Non salvata", tone: "amber" }} {...handlers} />;
  }
  render(<Harness />);
  return { env, handlers, draft: () => current };
}

const details = { availableFrom: "2026-11-16T09:00", availableUntil: "2026-11-16T11:00", durationMinutes: "60" };

describe("step 1: dettagli", () => {
  test("classe e materia fisse con 'Cambia' per un esame nuovo", () => {
    const { env } = setup();
    expect(screen.getByText("Nuovo esame")).toBeTruthy();
    expect(screen.getByText("SW-25A · Reti")).toBeTruthy();
    fireEvent.press(screen.getByLabelText("Cambia classe e materia"));
    expect(env.onChangeClassSubject).toHaveBeenCalled();
  });

  test("'Avanti' con dettagli incompleti: banner con gli errori e si resta allo step 1", () => {
    const { draft } = setup();
    fireEvent.press(screen.getByText("Avanti →"));
    expect(screen.getByTestId("editor-banner")).toBeTruthy();
    expect(screen.getByText("Completa i dettagli per andare avanti (2)")).toBeTruthy();
    expect(screen.getAllByText("Manca l'inizio della disponibilità.").length).toBeGreaterThan(0);
    expect(draft().step).toBe(1);
    fireEvent.press(screen.getByLabelText("Chiudi l'avviso"));
    expect(screen.queryByTestId("editor-banner")).toBeNull();
  });

  test("descrizione, date e durata si scrivono nella bozza; la durata accetta solo cifre", () => {
    const { draft } = setup();
    fireEvent.changeText(screen.getByPlaceholderText(/Facoltativa/), "Senza appunti");
    const [from, until] = screen.getAllByPlaceholderText("AAAA-MM-GG HH:mm");
    fireEvent.changeText(from, "2026-11-16 09:00");
    fireEvent.changeText(until, "2026-11-16 11:00");
    fireEvent.changeText(screen.getByPlaceholderText("Es. 45"), "9a0");
    expect(draft()).toMatchObject({ description: "Senza appunti", availableFrom: "2026-11-16T09:00", availableUntil: "2026-11-16T11:00", durationMinutes: "90" });
    expect(screen.getByText(/Deve stare nella finestra di disponibilità \(2 h\)/)).toBeTruthy();
    fireEvent.press(screen.getByText("Avanti →"));
    expect(draft().step).toBe(2);
  });

  test("esame di recupero: scelta dell'esame originale della stessa classe-materia", () => {
    const retakeOptions = [{ id: "e1", name: "Reti — 01/10/2026", classSubjectId: "cs1" }, { id: "e2", name: "Java", classSubjectId: "cs9" }];
    const { draft } = setup({}, { env: { retakeOptions } });
    fireEvent.press(screen.getByText("Esame di recupero"));
    fireEvent.press(screen.getByText("Seleziona l'esame originale"));
    expect(screen.queryByText("Java")).toBeNull();
    fireEvent.press(screen.getByText("Reti — 01/10/2026"));
    expect(draft().retakeOfExamId).toBe("e1");
  });
});

describe("step 2: domande", () => {
  test("scrittura della domanda, opzioni, risposta giusta e riepilogo", () => {
    const { draft } = setup({ ...details, step: 2 });
    fireEvent.changeText(screen.getByPlaceholderText(/Quale metodo HTTP/), "Metodo per creare?");
    fireEvent.changeText(screen.getByPlaceholderText(/Opzione 1/), "POST");
    fireEvent.changeText(screen.getByPlaceholderText(/Opzione 2/), "GET");
    fireEvent.press(screen.getAllByLabelText("Segna come giusta")[0]);
    fireEvent.press(screen.getByText("Aggiungi opzione"));
    expect(draft().questions[0]).toMatchObject({ text: "Metodo per creare?", correct: [draft().questions[0].options[0].id] });
    expect(draft().questions[0].options.map(o => o.text)).toEqual(["POST", "GET", ""]);
    expect(screen.getByText("GIUSTA")).toBeTruthy();
  });

  test("aggiunta di domande per tipo, cambio di tipo, duplicazione ed eliminazione", () => {
    const { draft } = setup({ ...details, step: 2 });
    const types = () => draft().questions.map(q => q.type);
    // Su schermi stretti il pannello "Aggiungi domanda" sta sopra l'editor: [0] aggiunge, [1] è il tipo della domanda aperta
    fireEvent.press(screen.getAllByText("Aperta")[0]);
    expect(types()).toEqual(["single", "open"]);
    expect(screen.getByText("Domanda 2 di 2")).toBeTruthy();
    expect(screen.getByText(/Lo studente scriverà la risposta/)).toBeTruthy();

    fireEvent.press(screen.getAllByText("Multipla")[1]); // la domanda 2 diventa a risposta multipla
    expect(types()).toEqual(["single", "multiple"]);

    fireEvent.press(screen.getByLabelText("Duplica"));
    expect(types()).toEqual(["single", "multiple", "multiple"]);
    expect(screen.getByText("Domanda 3 di 3")).toBeTruthy();

    fireEvent.press(screen.getByLabelText("Sposta su"));
    expect(screen.getByText("Domanda 2 di 3")).toBeTruthy();

    fireEvent.press(screen.getByLabelText("Elimina domanda"));
    expect(types()).toHaveLength(2);
  });

  test("punti: solo cifre; soglia in punti", () => {
    const { draft } = setup({ ...details, step: 2 });
    fireEvent.changeText(screen.getByPlaceholderText("Es. 2"), "1x0");
    expect(draft().questions[0].points).toBe("10");
    expect(screen.getByText("= 6 punti su 10")).toBeTruthy();
    fireEvent.changeText(screen.getByLabelText("Soglia di sufficienza in percentuale"), "75");
    expect(screen.getByText("= 7,5 punti su 10")).toBeTruthy();
  });

  test("'Indietro' torna ai dettagli", () => {
    const { draft } = setup({ ...details, step: 2 });
    fireEvent.press(screen.getByText("← Indietro"));
    expect(draft().step).toBe(1);
  });
});

describe("step 3: studenti", () => {
  test("seleziona tutti, tempo aggiuntivo, nessuno", async () => {
    const { draft, env } = setup({ ...details, step: 3 });
    expect(await screen.findByText("Rossi Mario")).toBeTruthy();
    expect(env.getStudents).toHaveBeenCalledWith("cs1");
    fireEvent.press(screen.getByText("Seleziona tutti"));
    expect(draft().students.map(s => s.studentId)).toEqual(["s1", "s2", "s3"]);
    fireEvent.changeText(screen.getAllByDisplayValue("0")[0], "15");
    expect(draft().students.find(s => s.studentId === "s1").extraTimeMinutes).toBe("15");
    fireEvent.press(screen.getByText("Bianchi Giulia")); // tolto
    expect(draft().students.map(s => s.studentId)).toEqual(["s1", "s3"]);
    fireEvent.press(screen.getByText("Nessuno"));
    expect(draft().students).toEqual([]);
  });

  test("ricerca per nome", async () => {
    setup({ ...details, step: 3 });
    await screen.findByText("Rossi Mario");
    fireEvent.changeText(screen.getByPlaceholderText("Cerca per nome…"), "giu");
    expect(screen.queryByText("Rossi Mario")).toBeNull();
    expect(screen.getByText("Bianchi Giulia")).toBeTruthy();
  });

  test("recupero di un esame chiuso: preselezionati chi deve recuperare e gli assenti", async () => {
    const byStudent = new Map([["s1", { reason: "failed", proposed: true }], ["s2", { reason: "passed", proposed: false }], ["s3", { reason: "missing", proposed: true }]]);
    const { draft } = setup({ ...details, step: 3, retakeOfExamId: "e0" }, { env: { getRetakeCandidates: jest.fn().mockResolvedValue({ closed: true, hasSubmissions: true, byStudent }) } });
    await waitFor(() => expect(draft().students.map(s => s.studentId)).toEqual(["s1", "s3"]));
    expect(screen.getByText("2 selezionati: 1 da recuperare, 1 assente")).toBeTruthy();
    expect(screen.getByText("Deve recuperare")).toBeTruthy();
    expect(screen.getByText("Superato")).toBeTruthy();
  });

  test("recupero di un esame non ancora chiuso: nessuno preselezionato", async () => {
    const { draft } = setup({ ...details, step: 3, retakeOfExamId: "e0" }, { env: { getRetakeCandidates: jest.fn().mockResolvedValue({ closed: false, hasSubmissions: false, byStudent: new Map() }) } });
    expect(await screen.findByText(/non è ancora chiuso/)).toBeTruthy();
    expect(draft().students).toEqual([]);
  });

  test("errore nel caricare gli studenti", async () => {
    setup({ ...details, step: 3 }, { env: { getStudents: jest.fn().mockRejectedValue(new Error("Classe non trovata")) } });
    expect(await screen.findByText("Classe non trovata")).toBeTruthy();
  });

  test("'Salva e chiudi' allo step 3", async () => {
    const { handlers } = setup({ ...details, step: 3 });
    await screen.findByText("Rossi Mario");
    fireEvent.press(screen.getByText("Salva e chiudi"));
    expect(handlers.onSaveAndClose).toHaveBeenCalled();
  });
});

describe("barra dell'editor", () => {
  test("'Salva bozza' e stato breve su schermi stretti", () => {
    const { handlers } = setup();
    expect(screen.getByText("Non salvata")).toBeTruthy();
    fireEvent.press(screen.getByLabelText("Salva bozza"));
    expect(handlers.onSaveDraft).toHaveBeenCalled();
  });

  test("azione distruttiva nel menu '⋯'", () => {
    const destroy = jest.fn();
    setup({}, { destroyAction: { label: "Elimina bozza", onPress: destroy } });
    expect(screen.queryByText("Elimina bozza")).toBeNull();
    fireEvent.press(screen.getByLabelText("Altre azioni"));
    act(() => { fireEvent.press(screen.getByText("Elimina bozza")); });
    expect(destroy).toHaveBeenCalled();
  });

  test("banner dopo un salvataggio: voce cliccabile che porta allo step giusto", () => {
    const { draft } = setup({ step: 2 }, { notice: { kind: "error", level: "close", title: "Da sistemare" } });
    expect(screen.getByText(/Da sistemare \(\d+\)/)).toBeTruthy();
    fireEvent.press(screen.getByLabelText("Manca l'inizio della disponibilità. Vai al campo"));
    expect(draft().step).toBe(1);
  });
});
