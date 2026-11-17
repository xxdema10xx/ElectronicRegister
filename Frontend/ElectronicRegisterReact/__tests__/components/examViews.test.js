// Scheda esame nell'elenco, dettaglio in sola lettura e pagina "Chi deve recuperare".
import { fireEvent, render, screen } from "@testing-library/react-native";
import ExamCard from "../../src/components/ExamCard";
import TestViewer from "../../src/components/TestViewer";
import ExamRetakes from "../../src/components/ExamRetakes";

const exam = {
  id: "e1", description: "Prova scritta", availableFrom: "2026-11-16T08:00:00Z", availableUntil: "2026-11-16T10:00:00Z",
  durationMinutes: 60, passingPercent: 60, totalPoints: 20, assignedStudentsCount: 1, isRetake: true,
};

describe("ExamCard", () => {
  test("dettagli: finestra nello stesso giorno, durata, soglia, punti, studenti", () => {
    render(<ExamCard exam={exam} status="published" displayName="Reti · SW-25A" actions={{}} />);
    ["Reti · SW-25A", "Disponibile dal 16/11/2026", "Prova scritta", "Pubblicato", "Recupero",
      "16/11/2026 · 09:00–11:00", "60 min", "Soglia 60%", "20 punti", "1 studente"].forEach(t => expect(screen.getByText(t)).toBeTruthy());
  });

  test("finestra su più giorni e date mancanti", () => {
    const { rerender } = render(<ExamCard exam={{ ...exam, availableUntil: "2026-11-18T10:00:00Z", assignedStudentsCount: 3 }} status="draft" displayName="X" actions={{}} />);
    expect(screen.getByText("16/11/2026 09:00 → 18/11/2026 11:00")).toBeTruthy();
    expect(screen.getByText("3 studenti")).toBeTruthy();
    rerender(<ExamCard exam={{ ...exam, availableFrom: null }} status="draft" displayName="X" actions={{}} />);
    expect(screen.getByText("Date da definire")).toBeTruthy();
  });

  test("solo le azioni consentite, ognuna con la sua funzione", () => {
    const actions = { submissions: jest.fn(), close: jest.fn(), publish: null, cancel: jest.fn(), remove: null };
    const onPress = jest.fn();
    render(<ExamCard exam={exam} status="published" displayName="Reti" actions={actions} onPress={onPress} unsaved />);
    expect(screen.queryByLabelText("Pubblica")).toBeNull();
    expect(screen.queryByLabelText("Elimina")).toBeNull();
    fireEvent.press(screen.getByLabelText("Consegne"));
    fireEvent.press(screen.getByLabelText("Chiudi"));
    fireEvent.press(screen.getByLabelText("Annulla esame"));
    fireEvent.press(screen.getByLabelText("Apri Reti"));
    expect([actions.submissions, actions.close, actions.cancel, onPress].every(f => f.mock.calls.length === 1)).toBe(true);
    expect(screen.getByText("Modifiche non salvate")).toBeTruthy();
  });

  test("mentre si opera sull'esame le azioni sono disattivate", () => {
    const publish = jest.fn();
    render(<ExamCard exam={exam} status="draft" displayName="Reti" busy actions={{ publish, remove: jest.fn() }} />);
    fireEvent.press(screen.getByLabelText("Elimina"));
    expect(publish).not.toHaveBeenCalled();
  });
});

describe("TestViewer", () => {
  const detail = {
    ...exam, status: 0, assignedStudents: [{ studentId: "s1" }, { studentId: "s2" }],
    questions: [
      { id: "q2", position: 1, text: "Spiega REST", type: 2, points: 5, required: true, options: [] },
      { id: "q1", position: 0, text: "Metodo per creare?", type: 0, points: 2, options: [{ id: "o1", text: "POST", position: 0, isCorrect: true }, { id: "o2", text: "GET", position: 1 }] },
    ],
  };

  test("domande in ordine, risposte giuste e riepilogo", () => {
    render(<TestViewer exam={detail} displayName="Reti · SW-25A" contextLabel="Disponibile dal 16/11/2026" actions={{}} />);
    expect(screen.getByText("1. Metodo per creare?")).toBeTruthy();
    expect(screen.getByText("2. Spiega REST *")).toBeTruthy();
    expect(screen.getAllByText("giusta")).toHaveLength(1);
    expect(screen.getByText("Risposta scritta dallo studente")).toBeTruthy();
    expect(screen.getByText(/2 domande \(1 chiuse, 1 aperte\) · 20 punti · 2 studenti/)).toBeTruthy();
  });

  test("azioni del dettaglio di una bozza", () => {
    const actions = { edit: jest.fn(), publish: jest.fn(), remove: jest.fn() };
    render(<TestViewer exam={detail} displayName="X" actions={actions} />);
    fireEvent.press(screen.getByLabelText("Modifica"));
    fireEvent.press(screen.getByLabelText("Pubblica"));
    fireEvent.press(screen.getByLabelText("Elimina"));
    expect(Object.values(actions).every(f => f.mock.calls.length === 1)).toBe(true);
  });

  test("senza esame non mostra nulla", () => {
    expect(render(<TestViewer exam={null} />).toJSON()).toBeNull();
  });
});

describe("ExamRetakes", () => {
  const props = { labelOf: (e) => `Esame ${e.id}`, hrefOf: (id) => `/esami/${id}`, onOpenExam: jest.fn() };
  const groups = [
    { studentId: "s1", firstName: "Mario", lastName: "Rossi", absentOnly: false, exams: [{ id: "e1", reason: "failed", isRetake: true }] },
    { studentId: "s2", firstName: "Giulia", lastName: "Bianchi", absentOnly: true, exams: [{ id: "e2", reason: "missing" }] },
  ];

  test("studenti con esami e motivo; tocco sull'esame lo apre", () => {
    render(<ExamRetakes loading={false} groups={groups} failed={0} {...props} />);
    expect(screen.getByText(/2 da recuperare \(1 assente\)/)).toBeTruthy();
    ["Rossi Mario", "Bianchi Giulia", "Deve recuperare", "Non ha consegnato", "Recupero"].forEach(t => expect(screen.getByText(t)).toBeTruthy());
    fireEvent.press(screen.getByLabelText("Apri Esame e2"));
    expect(props.onOpenExam).toHaveBeenCalledWith("e2");
  });

  test("consegne non lette: avviso di elenco incompleto", () => {
    render(<ExamRetakes loading={false} groups={groups} failed={2} {...props} />);
    expect(screen.getByText(/Impossibile caricare le consegne di 2 esami/)).toBeTruthy();
  });

  test("nessuno da recuperare / errore", () => {
    const { rerender } = render(<ExamRetakes loading={false} groups={[]} failed={0} {...props} />);
    expect(screen.getByText("Nessuno studente da recuperare")).toBeTruthy();
    rerender(<ExamRetakes loading={false} groups={[]} failed={1} {...props} />);
    expect(screen.getByText("Impossibile caricare le consegne")).toBeTruthy();
  });
});
