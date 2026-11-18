// Svolgimento dell'esame: risposte, salvataggio automatico, consegna, uscite dall'app e scadenza del tempo.
import { AppState } from "react-native";
import { act, fireEvent, render, screen } from "@testing-library/react-native";
import ExamTaker, { ALREADY_SUBMITTED_MESSAGE, EXCLUDED_MESSAGE, MAX_VIOLATIONS } from "../../src/components/ExamTaker";
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
    getResult: jest.fn().mockResolvedValue({ status: 1, submitReason: 2, passed: false, grade: 4 }),
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
  setStorageUser("u-student", "i1");
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
  expect(screen.getByText(/Hai risposto a 1 domanda su 3/)).toBeTruthy();
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

const appStateChange = () => AppState.addEventListener.mock.calls.at(-1)[1];
const receipt = (violationCount, autoSubmitted = false) => ({ eventId: "e", violationCount, duplicate: false, autoSubmitted });

test("uscire dall'app mette in pausa; alla terza il server consegna e lo studente è escluso (niente /submit)", async () => {
  const reportViolation = jest.fn()
    .mockResolvedValueOnce(receipt(1)).mockResolvedValueOnce(receipt(2)).mockResolvedValueOnce(receipt(3, true));
  const { api, onFinish } = setup({ api: { reportViolation } });
  await act(async () => appStateChange()("background"));
  expect(screen.getByText("Esame in pausa")).toBeTruthy();
  expect(screen.getByText("Hai lasciato l'app durante l'esame.")).toBeTruthy();
  expect(reportViolation).toHaveBeenCalledWith("sub1", expect.objectContaining({ type: "tab_hidden" }));
  await act(async () => appStateChange()("background")); // in pausa non si contano altre uscite
  expect(screen.getByText(`Uscite: 1/${MAX_VIOLATIONS}`)).toBeTruthy();

  fireEvent.press(screen.getByText("▶  Riprendi l'esame"));
  expect(screen.queryByText("Esame in pausa")).toBeNull();
  await act(async () => appStateChange()("background"));
  fireEvent.press(screen.getByText("▶  Riprendi l'esame"));
  await act(async () => appStateChange()("inactive"));
  expect(reportViolation).toHaveBeenCalledTimes(3);
  expect(api.getResult).toHaveBeenCalledWith("sub1");
  expect(api.submitExam).not.toHaveBeenCalled();
  expect(api.submitExpired).not.toHaveBeenCalled();
  expect(onFinish).toHaveBeenCalledWith(expect.objectContaining({
    auto: true, excluded: true, notice: EXCLUDED_MESSAGE, violations: 3, result: { status: 1, submitReason: 2, passed: false, grade: 4 },
  }));
  // dopo l'esclusione niente più salvataggi né timer
  await act(async () => { jest.advanceTimersByTime(31 * 60000); });
  expect(api.saveAnswers).not.toHaveBeenCalled();
  expect(api.submitExpired).not.toHaveBeenCalled();
});

test("il contatore è quello del server", async () => {
  setup({ api: { reportViolation: jest.fn().mockResolvedValue(receipt(2)) } });
  await act(async () => appStateChange()("background"));
  expect(screen.getByText(`Uscite: 2/${MAX_VIOLATIONS}`)).toBeTruthy();
  expect(screen.getByText(/Uscita 2 di 3: alla 3ª sei escluso dall'esame/)).toBeTruthy();
});

test("segnalazione non riuscita (rete): resta il conteggio locale e l'esame non si blocca", async () => {
  const { onFinish } = setup({ api: { reportViolation: jest.fn().mockRejectedValue(new Error("rete")) } });
  for (let i = 0; i < 3; i++) {
    await act(async () => appStateChange()("background"));
    fireEvent.press(screen.getByText("▶  Riprendi l'esame"));
  }
  expect(screen.getByText(`Uscite: 3/${MAX_VIOLATIONS}`)).toBeTruthy();
  expect(onFinish).not.toHaveBeenCalled();
});

test("soglia disattivata sul server: alla terza uscita si resta in pausa e si può riprendere, senza consegna dal client", async () => {
  const reportViolation = jest.fn()
    .mockResolvedValueOnce(receipt(1)).mockResolvedValueOnce(receipt(2)).mockResolvedValueOnce(receipt(3, false));
  const { api, onFinish } = setup({ api: { reportViolation } });
  for (let i = 0; i < 3; i++) {
    await act(async () => appStateChange()("background"));
    if (i < 2) fireEvent.press(screen.getByText("▶  Riprendi l'esame"));
  }
  expect(screen.getByText("Esame in pausa")).toBeTruthy();
  expect(api.submitExam).not.toHaveBeenCalled();
  expect(onFinish).not.toHaveBeenCalled();
  fireEvent.press(screen.getByText("▶  Riprendi l'esame"));
  expect(screen.queryByText("Esame in pausa")).toBeNull();
});

test("le risposte non ancora inviate si salvano prima di segnalare l'uscita", async () => {
  const { api } = setup({ api: { reportViolation: jest.fn().mockResolvedValue(receipt(1)) } });
  fireEvent.press(screen.getByText("GET"));
  await act(async () => appStateChange()("background"));
  expect(api.saveAnswers).toHaveBeenCalledTimes(1);
  expect(api.saveAnswers.mock.invocationCallOrder[0]).toBeLessThan(api.reportViolation.mock.invocationCallOrder[0]);
});

test("409 su una violazione con la prova già consegnata: si mostra il risultato, nessun errore generico", async () => {
  const conflict = Object.assign(new Error("Operazione non consentita"), { status: 409 });
  const getResult = jest.fn().mockResolvedValue({ status: 1, submitReason: 0, passed: true, grade: 24 });
  const { api, onFinish } = setup({ api: { reportViolation: jest.fn().mockRejectedValue(conflict), getResult } });
  await act(async () => appStateChange()("background"));
  expect(getResult).toHaveBeenCalledWith("sub1");
  expect(api.submitExam).not.toHaveBeenCalled();
  expect(onFinish).toHaveBeenCalledWith(expect.objectContaining({
    auto: true, excluded: false, notice: ALREADY_SUBMITTED_MESSAGE, result: { status: 1, submitReason: 0, passed: true, grade: 24 },
  }));
});

test("409 su una violazione con la prova ancora in corso (limite di frequenza): si resta in pausa", async () => {
  const conflict = Object.assign(new Error("Operazione non consentita"), { status: 409 });
  const { onFinish } = setup({ api: { reportViolation: jest.fn().mockRejectedValue(conflict), getResult: jest.fn().mockResolvedValue({ status: 0 }) } });
  await act(async () => appStateChange()("background"));
  expect(onFinish).not.toHaveBeenCalled();
  expect(screen.getByText("Esame in pausa")).toBeTruthy();
});

test("409 sull'autosave (consegnata da un'altra scheda): si rilegge il risultato e lo svolgimento si chiude", async () => {
  const conflict = Object.assign(new Error("Operazione non consentita"), { status: 409 });
  const getResult = jest.fn().mockResolvedValue({ status: 2, submitReason: 0 });
  const { onFinish } = setup({ api: { saveAnswers: jest.fn().mockRejectedValue(conflict), getResult } });
  fireEvent.press(screen.getByText("GET"));
  await act(async () => { jest.advanceTimersByTime(2600); });
  expect(getResult).toHaveBeenCalledWith("sub1");
  expect(onFinish).toHaveBeenCalledWith(expect.objectContaining({ notice: ALREADY_SUBMITTED_MESSAGE, result: { status: 2, submitReason: 0 } }));
  expect(screen.queryByText("Salvataggio non riuscito: riproverò")).toBeNull();
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
