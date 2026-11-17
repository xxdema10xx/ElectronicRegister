import axiosClient from "../../src/api/axiosClient";
import { session } from "../../src/api/session";
import {
  buildUser, changePassword, freshTicket, getAccount, getContext, getInstitutions, login, loginWithMicrosoft,
  primaryRole, selectInstitution, updateDisplayName,
} from "../../src/api/authService";

const ok = (data) => Promise.resolve({ data });

beforeEach(() => {
  session.clear();
  jest.spyOn(axiosClient, "get");
  jest.spyOn(axiosClient, "post");
  jest.spyOn(axiosClient, "put");
});
afterEach(() => jest.restoreAllMocks());

describe("accesso", () => {
  test("login: invia le credenziali e conserva il ticket", async () => {
    axiosClient.post.mockReturnValue(ok({ ticket: "tk", expiresInSeconds: 300 }));
    await expect(login("a@b.it", "pwd")).resolves.toEqual({ ticket: "tk", expiresInSeconds: 300 });
    expect(axiosClient.post).toHaveBeenCalledWith("/Auth/identity/login", { email: "a@b.it", password: "pwd" });
    expect(session.ticket).toBe("tk");
  });

  test("login Microsoft", async () => {
    axiosClient.post.mockReturnValue(ok({ ticket: "ms", expiresInSeconds: 300 }));
    await loginWithMicrosoft("ms-token");
    expect(axiosClient.post).toHaveBeenCalledWith("/Auth/identity/microsoft-login", { accessToken: "ms-token" });
    expect(session.ticket).toBe("ms");
  });

  test("login con password temporanea: passwordChangeRequired arriva a chi gestisce l'accesso", async () => {
    axiosClient.post.mockReturnValue(ok({ ticket: "tk", expiresInSeconds: 300, passwordChangeRequired: true }));
    await expect(login("a@b.it", "Temp-123")).resolves.toMatchObject({ passwordChangeRequired: true });
  });

  test("niente registrazione da parte degli utenti (route ritirata dal backend)", () => {
    expect(require("../../src/api/authService").register).toBeUndefined();
  });

  test("istituti: con il ticket; risposta non valida → elenco vuoto", async () => {
    axiosClient.get.mockReturnValueOnce(ok([{ institutionId: "i1" }])).mockReturnValueOnce(ok(null));
    await expect(getInstitutions("tk")).resolves.toEqual([{ institutionId: "i1" }]);
    expect(axiosClient.get).toHaveBeenCalledWith("/Auth/institutions", { headers: { Authorization: "Bearer tk" } });
    await expect(getInstitutions("tk")).resolves.toEqual([]);
  });

  test("scelta dell'istituto: salva token e istituto nella sessione", async () => {
    axiosClient.post.mockReturnValue(ok({ token: "acc", expiresInSeconds: 900 }));
    await selectInstitution("tk", { institutionId: "i1", membershipId: "m1", name: "ITS Umbria" });
    expect(axiosClient.post).toHaveBeenCalledWith("/Auth/select-institution", { institutionId: "i1", membershipId: "m1" },
      { headers: { Authorization: "Bearer tk" } });
    expect(session.accessToken).toBe("acc");
    expect(session.institution).toEqual({ institutionId: "i1", membershipId: "m1", name: "ITS Umbria" });
  });

  test("contesto, account, nome visualizzato e password", async () => {
    axiosClient.get.mockReturnValue(ok({ userId: "u" }));
    axiosClient.put.mockReturnValue(ok());
    await getContext("acc");
    expect(axiosClient.get).toHaveBeenLastCalledWith("/Auth/context", { headers: { Authorization: "Bearer acc" } });
    await getAccount("tk");
    expect(axiosClient.get).toHaveBeenLastCalledWith("/identity/account", { headers: { Authorization: "Bearer tk" } });
    await updateDisplayName("tk", "  Mario  ");
    expect(axiosClient.put).toHaveBeenLastCalledWith("/identity/account", { displayName: "Mario" }, { headers: { Authorization: "Bearer tk" } });
    await changePassword("tk", "old", "new");
    expect(axiosClient.put).toHaveBeenLastCalledWith("/identity/account/password", { currentPassword: "old", newPassword: "new" },
      { headers: { Authorization: "Bearer tk" } });
  });
});

describe("freshTicket", () => {
  test("usa il ticket ancora valido", async () => {
    session.setTicket("valid", 300);
    await expect(freshTicket()).resolves.toBe("valid");
    expect(axiosClient.post).not.toHaveBeenCalled();
  });

  test("senza ticket rifà il login con le credenziali in memoria", async () => {
    session.setCredentials({ email: "a@b.it", password: "p" });
    axiosClient.post.mockReturnValue(ok({ ticket: "new", expiresInSeconds: 300 }));
    await expect(freshTicket()).resolves.toBe("new");
  });

  test("né ticket né credenziali: chiede di accedere di nuovo", async () => {
    await expect(freshTicket()).rejects.toThrow("Per questa operazione esci e accedi di nuovo.");
  });
});

describe("utente dell'istituto", () => {
  test("ruolo principale per priorità", () => {
    expect(primaryRole(["Student", "InstitutionAdmin"])).toBe("admin");
    expect(primaryRole(["Teacher", "Staff"])).toBe("staff");
    expect(primaryRole(["Teacher"])).toBe("teacher");
    expect(primaryRole([])).toBe("student");
  });

  test("buildUser: contesto, account e profilo docente collegato", async () => {
    session.setAccess("acc", 900, { institutionId: "i1", name: "ITS" });
    axiosClient.get.mockImplementation((url) => {
      if (url === "/Auth/context") return ok({ userId: "u1", institutionId: "i1", membershipId: "m1", roles: ["Teacher"] });
      if (url === "/institution/teachers/me") return ok({ id: "t1", firstName: "Anna", lastName: "Neri" });
      throw new Error(url);
    });
    const user = await buildUser("acc", { email: "anna@x.it", displayName: "Anna" });
    expect(user).toEqual({
      id: "u1", email: "anna@x.it", displayName: "Anna", roles: ["Teacher"], role: "teacher",
      institutionId: "i1", membershipId: "m1", institutionName: "ITS",
      teacherId: "t1", teacherFirstName: "Anna", teacherLastName: "Neri",
    });
  });

  test("buildUser: profilo studente non collegato → utente senza studentId; email dalle credenziali", async () => {
    session.setCredentials({ email: "cred@x.it", password: "p" });
    axiosClient.get.mockImplementation((url) => (url === "/Auth/context"
      ? ok({ userId: "u2", roles: ["Student"] })
      : Promise.reject(Object.assign(new Error("404"), { status: 404 }))));
    const user = await buildUser("acc", null);
    expect(user.role).toBe("student");
    expect(user.email).toBe("cred@x.it");
    expect(user.studentId).toBeUndefined();
  });
});
