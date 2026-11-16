// Normalizzazione degli errori HTTP in messaggi leggibili e logout automatico sul 401.
import axiosClient, { authHeader, setUnauthorizedHandler, ticketHeader } from "../../src/api/axiosClient";
import { session } from "../../src/api/session";

// Risposta finta del server: { status, data } oppure un errore di rete ({ code } senza risposta)
function respond(fake) {
  axiosClient.defaults.adapter = (config) => {
    if (fake.network) return Promise.reject(Object.assign(new Error("Network Error"), { config, code: fake.code }));
    const response = { status: fake.status, data: fake.data, headers: {}, config };
    if (fake.status < 400) return Promise.resolve(response);
    return Promise.reject(Object.assign(new Error(`Request failed with status code ${fake.status}`), { config, response }));
  };
}
const fail = (fake, config) => { respond(fake); return axiosClient.get("/x", config).catch(e => e); };

afterEach(() => { session.clear(); setUnauthorizedHandler(null); });

describe("messaggi di errore", () => {
  test.each([
    ["timeout", { network: true, code: "ECONNABORTED" }, "Il server non risponde, riprova"],
    ["rete assente", { network: true }, "Impossibile contattare il server. Controlla la connessione e riprova."],
    ["gateway 502", { status: 502, data: "" }, "Il server non è disponibile in questo momento, riprova tra poco."],
    ["pagina HTML del proxy", { status: 500, data: "<!DOCTYPE html><html>…" }, "Il server non è disponibile in questo momento, riprova tra poco."],
    ["404", { status: 404, data: { title: "Not Found" } }, "Elemento non trovato: potrebbe essere stato eliminato."],
    ["testo semplice", { status: 400, data: "Email già registrata" }, "Email già registrata"],
    ["ProblemDetails con detail", { status: 409, data: { title: "Operazione non consentita", detail: "Classe piena" } }, "Classe piena"],
    ["titolo generico ignorato", { status: 409, data: { title: "Operazione non consentita" } }, "Operazione non consentita nello stato attuale. Ricarica e riprova."],
    ["errori di validazione", { status: 400, data: { title: "x", errors: { Name: ["obbligatorio"] } } }, "Dati non validi: controlla i campi."],
    ["solo codice", { status: 409, data: { code: "calendar.revision-conflict" } }, "Errore: calendar.revision-conflict"],
    ["403", { status: 403, data: {} }, "Non hai i permessi per questa operazione (o la funzione non è attiva)."],
    ["400 senza dettagli", { status: 400, data: {} }, "Dati non validi."],
  ])("%s", async (_, fake, message) => {
    const e = await fail(fake);
    expect(e).toBeInstanceOf(Error);
    expect(e.message).toBe(message);
  });

  test("l'errore porta status, problem e code", async () => {
    const e = await fail({ status: 409, data: { detail: "Motivo", code: "exam.locked" } });
    expect(e).toMatchObject({ status: 409, problem: "Motivo", code: "exam.locked" });
  });

  test("le risposte riuscite passano invariate", async () => {
    respond({ status: 200, data: { ok: true } });
    await expect(axiosClient.get("/x")).resolves.toMatchObject({ data: { ok: true } });
  });
});

describe("401: torna al login solo se è stato rifiutato il token corrente", () => {
  test("token corrente → logout", async () => {
    const handler = jest.fn();
    setUnauthorizedHandler(handler);
    session.setAccess("current", 900);
    await fail({ status: 401, data: {} }, { headers: authHeader() });
    expect(handler).toHaveBeenCalledTimes(1);
  });

  test("ticket o token vecchio → nessun logout", async () => {
    const handler = jest.fn();
    setUnauthorizedHandler(handler);
    session.setAccess("current", 900);
    await fail({ status: 401, data: {} }, { headers: ticketHeader("ticket") });
    expect(handler).not.toHaveBeenCalled();
  });
});

describe("header", () => {
  test("authHeader usa il token della sessione, poi quello passato", () => {
    expect(authHeader("old")).toEqual({ Authorization: "Bearer old" });
    session.setAccess("fresh", 900);
    expect(authHeader("old")).toEqual({ Authorization: "Bearer fresh" });
  });

  test("senza token nessun header", () => {
    expect(authHeader()).toEqual({});
    expect(ticketHeader()).toEqual({});
    expect(ticketHeader("t")).toEqual({ Authorization: "Bearer t" });
  });
});
