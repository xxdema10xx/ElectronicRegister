// Consegne di un esame (elenco con filtri) e correzione di una consegna (punti alle aperte, voto finale).
import { fireEvent, screen, waitFor } from "@testing-library/react-native";
import ExamSubmissions, { SubmissionReview } from "../../src/components/ExamSubmissions";
import { setPageHeader } from "../../src/navigation/examRoutes";
import { flush, pressDialog, renderAs } from "../helpers/render";

afterEach(() => setPageHeader(null));

describe("elenco delle consegne", () => {
  const items = [
    { id: "s1", studentFirstName: "Mario", studentLastName: "Rossi", status: "pending_review", submittedAt: "2026-11-16T09:00:00Z", totalScore: 6, maxScore: 10, violationCount: 2 },
    { id: "s2", studentFirstName: "Giulia", studentLastName: "Bianchi", status: "graded", submittedAt: "2026-11-16T09:30:00Z", totalScore: 9, maxScore: 10 },
    { id: "s3", studentFirstName: "Luca", studentLastName: "Verdi", status: "in_progress" },
  ];

  test("filtri con contatori e apertura della correzione", async () => {
    const api = { listSubmissions: jest.fn().mockResolvedValue(items) };
    const onOpen = jest.fn();
    renderAs("teacher", <ExamSubmissions exam={{ id: "e1" }} subtitle="Reti · SW-25A" api={api} hrefOf={(id) => id} onOpen={onOpen} />);
    await flush();
    ["Tutte (3)", "In correzione (1)", "Valutate (1)", "In corso (1)", "Da valutare (0)"].forEach(t => expect(screen.getByText(t)).toBeTruthy());
    expect(screen.getByText(/6\/10 · 2 uscite/)).toBeTruthy();
    expect(screen.getByText("Non ancora consegnato")).toBeTruthy();

    fireEvent.press(screen.getByText("Valutate (1)"));
    expect(screen.queryByText("Rossi Mario")).toBeNull();
    expect(screen.getByText("Bianchi Giulia")).toBeTruthy();
    fireEvent.press(screen.getByText("Da valutare (0)"));
    expect(screen.getByText("Nessuna consegna")).toBeTruthy();

    fireEvent.press(screen.getByText("Tutte (3)"));
    fireEvent.press(screen.getByLabelText("Correggi Rossi Mario"));
    expect(onOpen).toHaveBeenCalledWith("s1");
  });

  test("errore di caricamento: avviso ed elenco vuoto", async () => {
    const api = { listSubmissions: jest.fn().mockRejectedValue(new Error("Server giù")) };
    renderAs("teacher", <ExamSubmissions exam={{ id: "e1" }} api={api} hrefOf={() => ""} onOpen={() => {}} />);
    expect(await screen.findByText("Server giù")).toBeTruthy();
  });
});

describe("correzione di una consegna", () => {
  const review = (over = {}) => ({
    status: "pending_review", studentFirstName: "Mario", studentLastName: "Rossi", examName: "Reti",
    submittedAt: "2026-11-16T09:00:00Z", submitReason: 1, violationCount: 1,
    closedScore: 2, openMaxPoints: 5, openScore: null, totalScore: 2, maxScore: 7, passingScore: 4.2, suggestedGrade: 18, finalGrade: null,
    answers: [
      { answerId: "a1", questionText: "Metodo per creare?", questionType: 0, maxPoints: 2, awardedPoints: 2, isCorrect: true,
        options: [{ id: "o1", text: "POST", isSelected: true, isCorrect: true }, { id: "o2", text: "GET" }] },
      { answerId: "a2", questionText: "Spiega REST", questionType: 2, maxPoints: 5, awardedPoints: null, openText: "Architettura…" },
    ],
    ...over,
  });

  function setup(data = review()) {
    const api = {
      getReview: jest.fn().mockResolvedValue(data),
      reviewSubmission: jest.fn().mockResolvedValue({}),
      finalizeSubmission: jest.fn().mockResolvedValue({}),
    };
    const props = { onClose: jest.fn(), onChanged: jest.fn() };
    renderAs("teacher", <SubmissionReview submissionId="sub1" displayName="Reti · SW-25A" api={api} {...props} />);
    return { api, ...props };
  }

  test("riepilogo della consegna", async () => {
    setup();
    await flush();
    expect(screen.getByText("Consegnato il 16/11/2026, 10:00 · tempo scaduto")).toBeTruthy();
    expect(screen.getByText("1 uscite dal test")).toBeTruthy();
    expect(screen.getByText("Architettura…")).toBeTruthy();
    expect(screen.getByText("Salva correzione (0/1 aperte)")).toBeTruthy();
  });

  test("punti fuori intervallo: avviso, niente salvataggio", async () => {
    const { api } = setup();
    await flush();
    fireEvent.changeText(screen.getByPlaceholderText("Punti"), "9");
    fireEvent.press(screen.getByText("Salva correzione (1/1 aperte)"));
    expect(await screen.findByText(/devono essere tra 0 e 5/)).toBeTruthy();
    expect(api.reviewSubmission).not.toHaveBeenCalled();
  });

  test("nessun punto assegnato: avviso", async () => {
    setup();
    await flush();
    fireEvent.press(screen.getByText("Salva correzione (0/1 aperte)"));
    expect(await screen.findByText("Assegna i punti ad almeno una risposta aperta.")).toBeTruthy();
  });

  test("salva punti (anche con la virgola) e commento", async () => {
    const { api, onChanged } = setup();
    await flush();
    fireEvent.changeText(screen.getByPlaceholderText("Punti"), "3,5");
    fireEvent.changeText(screen.getByPlaceholderText("Commento (facoltativo)"), "  Buona  ");
    fireEvent.press(screen.getByText("Salva correzione (1/1 aperte)"));
    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    expect(api.reviewSubmission).toHaveBeenCalledWith("sub1", { answers: [{ answerId: "a2", awardedPoints: 3.5, comment: "Buona" }] });
    expect(api.getReview).toHaveBeenCalledTimes(2);
  });

  test("voto diverso dal suggerito: serve il motivo", async () => {
    const { api } = setup();
    await flush();
    fireEvent.changeText(screen.getByPlaceholderText("0–30"), "24");
    fireEvent.press(screen.getByText("Assegna il voto"));
    expect(await screen.findByText(/indica il motivo/)).toBeTruthy();
    await pressDialog("OK");
    fireEvent.changeText(screen.getByPlaceholderText("Motivo del voto diverso"), "Ottimo orale");
    fireEvent.press(screen.getByText("Assegna il voto"));
    await waitFor(() => expect(api.finalizeSubmission).toHaveBeenCalledWith("sub1", { finalGrade: 24, overrideReason: "Ottimo orale" }));
  });

  test("voto uguale al suggerito: nessun motivo; voto fuori scala rifiutato", async () => {
    const { api } = setup();
    await flush();
    for (const wrong of ["31", "24,5", "-1"]) {
      fireEvent.changeText(screen.getByPlaceholderText("0–30"), wrong);
      fireEvent.press(screen.getByText("Assegna il voto"));
      expect(await screen.findByText("Il voto deve essere un numero intero tra 0 e 30.")).toBeTruthy();
      await pressDialog("OK");
    }
    expect(api.finalizeSubmission).not.toHaveBeenCalled();
    fireEvent.changeText(screen.getByPlaceholderText("0–30"), "18");
    fireEvent.press(screen.getByText("Assegna il voto"));
    await waitFor(() => expect(api.finalizeSubmission).toHaveBeenCalledWith("sub1", { finalGrade: 18, overrideReason: null }));
  });

  test("segreteria: consegna da correggere ma in sola lettura (correzione e voto spettano a docente e admin)", async () => {
    const api = { getReview: jest.fn().mockResolvedValue(review()), reviewSubmission: jest.fn(), finalizeSubmission: jest.fn() };
    renderAs("staff", <SubmissionReview submissionId="sub1" api={api} onClose={jest.fn()} onChanged={jest.fn()} canGrade={false} />);
    await flush();
    expect(screen.getByText("Architettura…")).toBeTruthy();
    expect(screen.queryByPlaceholderText("Punti")).toBeNull();
    expect(screen.queryByText("Assegna il voto")).toBeNull();
    expect(screen.getByText(/spettano al docente o all'amministratore/)).toBeTruthy();
  });

  test("consegna già valutata in via definitiva: sola lettura", async () => {
    setup(review({ status: "finalized", finalGrade: 21, finalizedAt: "2026-11-17T10:00:00Z", answers: [] }));
    await flush();
    expect(screen.getByText("Voto assegnato: 21")).toBeTruthy();
    expect(screen.queryByText("Assegna il voto")).toBeNull();
  });

  test("errore di caricamento: avviso e ritorno all'elenco", async () => {
    const api = { getReview: jest.fn().mockRejectedValue(new Error("Non trovata")) };
    const onClose = jest.fn();
    renderAs("teacher", <SubmissionReview submissionId="x" api={api} onClose={onClose} onChanged={() => {}} />);
    expect(await screen.findByText("Non trovata")).toBeTruthy();
    expect(onClose).toHaveBeenCalled();
  });
});
