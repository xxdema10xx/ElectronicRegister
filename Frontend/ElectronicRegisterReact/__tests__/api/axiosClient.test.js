// Normalizzazione degli errori HTTP in messaggi leggibili e logout automatico sul 401.
import axiosClient, { REVOKED_MESSAGE, authHeader, setUnauthorizedHandler, ticketHeader } from "../../src/api/axiosClient";
import { session } from "../../src/api/session";
import { isHandledError } from "../../src/utils/handledErrors";

// Risposta finta del server: { status, data } oppure un errore di rete ({ code } senza risposta)
function respond(fake) {
  axiosClient.defaults.adapter = (config) => {
    if (fake.network) return Promise.reject(Object.assign(new Error("Network Error"), { config, code: fake.code }));
    const response = { status: fake.status, data: fake.data, headers: fake.headers || {}, config };
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
    ["D-11: 429 con titolo del server", { status: 429, data: { title: "Too Many Requests", detail: "Rate limit" } }, "Troppe richieste: attendi un minuto e riprova."],
    ["D-11: 429 senza corpo", { status: 429, data: "" }, "Troppe richieste: attendi un minuto e riprova."],
    ["D-12: 500 con detail interno", { status: 500, data: { title: "Errore", detail: "SqlException: Timeout at Db.Query" } }, "Si è verificato un errore sul server. Riprova più tardi."],
    ["D-12: 500 con codice di correlazione", { status: 500, data: { detail: "NullReferenceException" }, headers: { "x-correlation-id": "abc-123" } },
      "Si è verificato un errore sul server. Riprova più tardi. Codice per l'assistenza: abc-123"],
    ["D-12: 501 testo semplice", { status: 501, data: "stack trace…" }, "Si è verificato un errore sul server. Riprova più tardi."],
  ])("%s", async (_, fake, message) => {
    const e = await fail(fake);
    expect(e).toBeInstanceOf(Error);
    expect(e.message).toBe(message);
  });

  test("l'errore porta status, problem e code", async () => {
    const e = await fail({ status: 409, data: { detail: "Motivo", code: "exam.locked" } });
    expect(e).toMatchObject({ status: 409, problem: "Motivo", code: "exam.locked" });
  });

  test("D-12: per i 5xx il detail del server non arriva alle schermate", async () => {
    const e = await fail({ status: 500, data: { detail: "SqlException", code: "db.error" } });
    expect(e.problem).toBeNull();
    expect(e.message).not.toMatch(/SqlException|db\.error/);
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
    const e = await fail({ status: 401, data: {} }, { headers: ticketHeader("ticket") });
    expect(handler).not.toHaveBeenCalled();
    expect(isHandledError(e)).toBe(false);
  });

  test("D-01: l'errore del 401 che ha chiuso la sessione è già gestito (un solo dialog)", async () => {
    const handler = jest.fn(() => session.clear());
    setUnauthorizedHandler(handler);
    session.setAccess("token-d01", 900);
    const headers = authHeader();
    // più richieste in volo con lo stesso token: un solo logout, tutti gli errori già gestiti
    const errors = [await fail({ status: 401, data: {} }, { headers }), await fail({ status: 401, data: {} }, { headers }),
      await fail({ status: 500, data: {} }, { headers })];
    expect(handler).toHaveBeenCalledTimes(1);
    errors.forEach(e => {
      expect(e.handled).toBe(true);
      expect(isHandledError(e.message)).toBe(true);
    });
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

describe("D-04: 403 su /institution/** con la membership sospesa o revocata", () => {
  // Risposte diverse per URL: la route istituzionale risponde 403, /Auth/context come indicato
  function route(contextStatus) {
    const calls = [];
    axiosClient.defaults.adapter = (config) => {
      calls.push(config.url);
      const status = config.url === "/Auth/context" ? contextStatus : 403;
      const response = { status, data: {}, headers: {}, config };
      if (status < 400) return Promise.resolve(response);
      return Promise.reject(Object.assign(new Error(`status ${status}`), { config, response }));
    };
    return calls;
  }
  const call = (url) => axiosClient.get(url, { headers: authHeader() }).catch(e => e);

  test.each([401, 403])("/Auth/context %i → logout con il messaggio della sospensione", async (contextStatus) => {
    const handler = jest.fn(() => session.clear());
    setUnauthorizedHandler(handler);
    session.setAccess(`token-revoked-${contextStatus}`, 900);
    const calls = route(contextStatus);
    // tre 403 in parallelo: una sola verifica, un solo logout, nessun errore da mostrare
    const errors = await Promise.all([call("/institution/students"), call("/institution/classes"), call("/institution/exams")]);
    expect(calls.filter(u => u === "/Auth/context")).toHaveLength(1);
    expect(handler).toHaveBeenCalledTimes(1);
    expect(handler).toHaveBeenCalledWith(REVOKED_MESSAGE);
    errors.forEach(e => expect(e.handled).toBe(true));
  });

  test("/Auth/context 200 → normale permesso mancante: messaggio attuale, nessun logout", async () => {
    const handler = jest.fn();
    setUnauthorizedHandler(handler);
    session.setAccess("token-ok", 900);
    const calls = route(200);
    const e = await call("/institution/admin/members");
    expect(calls).toEqual(["/institution/admin/members", "/Auth/context"]);
    expect(handler).not.toHaveBeenCalled();
    expect(e.handled).toBeUndefined();
    expect(e.message).toBe("Non hai i permessi per questa operazione (o la funzione non è attiva).");
  });

  test("403 fuori da /institution o con un altro token: nessuna verifica", async () => {
    setUnauthorizedHandler(jest.fn());
    session.setAccess("token-x", 900);
    const calls = route(401);
    await axiosClient.get("/identity/account", { headers: authHeader() }).catch(() => {});
    await axiosClient.get("/institution/students", { headers: ticketHeader("ticket") }).catch(() => {});
    expect(calls).not.toContain("/Auth/context");
  });
});
