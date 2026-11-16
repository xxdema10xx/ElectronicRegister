import { session } from "../../src/api/session";

describe("session", () => {
  beforeEach(() => { jest.useFakeTimers({ now: new Date("2026-11-16T10:00:00Z") }); session.clear(); });
  afterEach(() => jest.useRealTimers());

  test("ticket valido fino a 5 secondi prima della scadenza", () => {
    session.setTicket("tk", 300);
    expect(session.ticket).toBe("tk");
    jest.advanceTimersByTime(294 * 1000);
    expect(session.ticket).toBe("tk");
    jest.advanceTimersByTime(2000);
    expect(session.hasValidTicket()).toBe(false);
    expect(session.ticket).toBeNull();
  });

  test("token istituzionale, istituto e rinnovo un minuto prima della scadenza", () => {
    session.setAccess("acc", 900, { institutionId: "i1", name: "ITS" });
    expect(session.accessToken).toBe("acc");
    expect(session.institution).toEqual({ institutionId: "i1", name: "ITS" });
    expect(session.msUntilRenew()).toBe(840 * 1000);
    jest.advanceTimersByTime(900 * 1000);
    expect(session.msUntilRenew()).toBe(0);
  });

  test("setAccess senza istituto mantiene quello precedente", () => {
    session.setAccess("a1", 900, { institutionId: "i1" });
    session.setAccess("a2", 900);
    expect(session.institution).toEqual({ institutionId: "i1" });
  });

  test("restore dopo un refresh della pagina", () => {
    session.restore({ accessToken: "x", accessExpiresAt: Date.now() + 1000, institution: { institutionId: "i" } });
    expect(session.accessToken).toBe("x");
    expect(session.accessExpiresAt).toBe(Date.now() + 1000);
  });

  test("credenziali solo in memoria e clear azzera tutto", () => {
    session.setCredentials({ email: "a@b.it", password: "p" });
    session.setTicket("t", 300);
    session.setAccess("a", 900, { institutionId: "i" });
    session.clear();
    expect([session.credentials, session.ticket, session.accessToken, session.institution]).toEqual([null, null, null, null]);
  });
});
