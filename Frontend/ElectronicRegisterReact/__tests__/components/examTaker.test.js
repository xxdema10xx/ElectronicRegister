// Svolgimento dell'esame: risposte, salvataggio automatico, consegna, uscite dall'app e scadenza del tempo.
import { AppState } from "react-native";
import { act, fireEvent, render, screen } from "@testing-library/react-native";
import ExamTaker, { MAX_VIOLATIONS } from "../../src/components/ExamTaker";
import { setStorageUser } from "../../src/utils/userStorage";
import { getRun } from "../../src/data/studentExams";

const questions = [
  { id: "q1", text: "Metodo per creare?", type: "single", points: 2, required: true, options: [{ id: "o1", text: "POST" }, { id: "o2", text: "GET" }] },
  { id: "q2", text: "Metodi idempotenti", type: "multiple", points: 3, required: false, options: [{ id: "m1", text: "PUT" }, { id: "m2", text: "DELETE" }, { id: "m3", text: "POST" }] },
  { id: "q3", text: "Spiega REST", type: "open", points: 5, required: false, options: [] },
];

function setup({ minutesLeft = 30, api: overrides = {} } = {}) {
  const api = {
    saveAnswers: jest.fn().mockResolvedValue({ currentRevision: 1 }),
    submitExam: jest.fn().mockResolvedValue({ status: "graded" }),
    submitExpired: jest.fn().mockResolvedValue({ status: "graded" }),
    reportViolation: jest.fn().mockResolvedValue({}),
    ...overrides,
  };
  const run = {
    examId: "e1", submissionId: "sub1", deadlineAt: new Date(Date.now() + minutesLeft * 60000).toISOString(),
    exam: { name: "Reti", description: "Leggi bene", questions }, answers: {}, revision: 0,
  };
  const onFinish = jest.fn();
  render(<ExamTaker run={run} api={api} onFinish={onFinish} />);
  return { api, onFinish };
}

beforeEach(() => {
  jest.useFakeTimers({ now: new Date("2026-11-16T09:00:00Z") });
  setStorageUser("u-student");
});
afterEach(() => { jest.useRealTimers(); setStorageUser(null); });

test("intestazione: nome, tempo rimasto e uscite", () => {
  setup();
  expect(screen.getByText("Reti")).toBeTruthy();
  expect(screen.getByText("30:00")).toBeTruthy();
  expect(screen.getByText(`Uscite: 0/${MAX_VIOLATIONS}`)).toBeTruthy();
  expect(screen.getByText("Consegna (0/3)")).toBeTruthy();
});

test("le risposte si salvano sul dispositivo subito e sul server dopo una pausa", async () => {
  const { api } = setup();
  fireEvent.press(screen.getAllByText("POST")[0]);
  fireEvent.press(screen.getByText("PUT"));
  fireEvent.press(screen.getByText("DELETE"));
  fireEvent.press(screen.getByText("PUT")); // tolta
  fireEvent.changeText(screen.getByPlaceholderText("Scrivi qui la tua risposta"), "Stateless");
  expect(getRun("e1").answers).toEqual({ q1: "o1", q2: ["m2"], q3: "Stateless" });
  expect(api.saveAnswers).not.toHaveBeenCalled();
  await act(async () => { jest.advanceTimersByTime(2600); });
  expect(api.saveAnswers).toHaveBeenCalledWith("sub1", {
    clientRevision: 1,
    answers: [
      { questionId: "q1", openText: null, selectedOptionIds: ["o1"] },
      { questionId: "q2", openText: null, selectedOptionIds: ["m2"] },
      { questionId: "q3", openText: "Stateless", selectedOptionIds: [] },
    ],
  });
  expect(screen.getByText("Risposte salvate")).toBeTruthy();
  expect(screen.getByText("Consegna (3/3)")).toBeTruthy();
});

test("salvataggio rifiutato per revisione vecchia: si riparte da quella del server e si salva", async () => {
  const saveAnswers = jest.fn()
    .mockResolvedValueOnce({ accepted: false, currentRevision: 7 })
    .mockResolvedValueOnce({ accepted: true, currentRevision: 8 });
  setup({ api: { saveAnswers } });
  fireEvent.press(screen.getByText("GET"));
  await act(async () => { jest.advanceTimersByTime(2600); });
  expect(saveAnswers.mock.calls.map(c => c[1].clientRevision)).toEqual([1, 8]);
  expect(screen.getByText("Risposte salvate")).toBeTruthy();
});

test("salvataggio rifiutato due volte: non si dice 'salvate'", async () => {
  setup({ api: { saveAnswers: jest.fn().mockResolvedValue({ accepted: false, currentRevision: 50 }) } });
  fireEvent.press(screen.getByText("GET"));
  await act(async () => { jest.advanceTimersByTime(2600); });
  expect(screen.queryByText("Risposte salvate")).toBeNull();
  expect(screen.getByText("Salvataggio non riuscito: riproverò")).toBeTruthy();
});

test("salvataggio non riuscito: lo dice e riproverà", async () => {
  setup({ api: { saveAnswers: jest.fn().mockRejectedValue(new Error("rete")) } });
  fireEvent.press(screen.getByText("GET"));
  await act(async () => { jest.advanceTimersByTime(2600); });
  expect(screen.getByText("Salvataggio non riuscito: riproverò")).toBeTruthy();
});

test("domande obbligatorie senza risposta bloccano la consegna", () => {
  const { api } = setup();
  fireEvent.press(screen.getByText("Consegna (0/3)"));
  expect(screen.getByText("Rispondi alle domande obbligatorie (*): 1.")).toBeTruthy();
  expect(api.submitExam).not.toHaveBeenCalled();
});

test("consegna volontaria con conferma", async () => {
  const { api, onFinish } = setup();
  fireEvent.press(screen.getAllByText("POST")[0]);
  fireEvent.press(screen.getByText("Consegna (1/3)"));
  expect(screen.getByText("Domande senza risposta (evidenziate): 2, 3.")).toBeTruthy();
  expect(screen.getByText(/Hai risposto a 1 domande su 3/)).toBeTruthy();
  await act(async () => { fireEvent.press(screen.getByText("Sì, consegna")); });
  expect(api.submitExam).toHaveBeenCalledWith("sub1", { clientRevision: 2, answers: [{ questionId: "q1", openText: null, selectedOptionIds: ["o1"] }] });
  expect(onFinish).toHaveBeenCalledWith({ result: { status: "graded" }, auto: false, reason: null, violations: 0 });
});

test("consegna non riuscita: messaggio e si resta nell'esame", async () => {
  const { onFinish } = setup({ api: { submitExam: jest.fn().mockRejectedValue(new Error("Server non raggiungibile")) } });
  fireEvent.press(screen.getAllByText("POST")[0]);
  fireEvent.press(screen.getByText("Consegna (1/3)"));
  await act(async () => { fireEvent.press(screen.getByText("Sì, consegna")); });
  expect(screen.getByText("Server non raggiungibile")).toBeTruthy();
  expect(onFinish).not.toHaveBeenCalled();
});

test("uscire dall'app mette in pausa; alla terza uscita consegna in automatico", async () => {
  const { api, onFinish } = setup();
  const onChange = AppState.addEventListener.mock.calls.at(-1)[1];
  await act(async () => onChange("background"));
  expect(screen.getByText("Esame in pausa")).toBeTruthy();
  expect(screen.getByText("Hai lasciato l'app durante l'esame.")).toBeTruthy();
  expect(api.reportViolation).toHaveBeenCalledWith("sub1", expect.objectContaining({ type: "tab_hidden" }));
  await act(async () => onChange("background")); // in pausa non si contano altre uscite
  expect(screen.getByText(`Uscite: 1/${MAX_VIOLATIONS}`)).toBeTruthy();

  fireEvent.press(screen.getByText("▶  Riprendi l'esame"));
  expect(screen.queryByText("Esame in pausa")).toBeNull();
  await act(async () => onChange("background"));
  fireEvent.press(screen.getByText("▶  Riprendi l'esame"));
  await act(async () => onChange("inactive"));
  expect(api.submitExam).toHaveBeenCalled();
  expect(onFinish).toHaveBeenCalledWith(expect.objectContaining({ auto: true, reason: "troppe uscite dal test", violations: 3 }));
});

test("allo scadere del tempo consegna le risposte già salvate", async () => {
  const { api, onFinish } = setup({ minutesLeft: 0.05 }); // 3 secondi
  await act(async () => { jest.advanceTimersByTime(4000); });
  expect(api.saveAnswers).toHaveBeenCalled(); // ultimo salvataggio prima della fine
  expect(api.submitExpired).toHaveBeenCalledWith("sub1");
  expect(onFinish).toHaveBeenCalledWith(expect.objectContaining({ auto: true, reason: "tempo scaduto" }));
});

test("consegna automatica non riuscita: si può riprovare o uscire", async () => {
  const submitExpired = jest.fn().mockRejectedValue(Object.assign(new Error("Errore del server"), { status: 500 }));
  const { onFinish } = setup({ minutesLeft: 0.01, api: { submitExpired } });
  await act(async () => { jest.advanceTimersByTime(2000); });
  expect(screen.getByText("Consegna automatica non riuscita")).toBeTruthy();
  submitExpired.mockResolvedValueOnce({ status: "graded" });
  await act(async () => { fireEvent.press(screen.getByText("Riprova la consegna")); });
  expect(onFinish).toHaveBeenCalledWith(expect.objectContaining({ result: { status: "graded" }, auto: true }));
});
