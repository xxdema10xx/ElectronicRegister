// Membri dell'istituto (admin) e profilo dell'utente.
import { Alert } from "react-native";
import { act, fireEvent, screen, waitFor } from "@testing-library/react-native";
import UsersScreen, {
  EVENT_CONFLICT_MESSAGE, INVALID_TRANSITION_MESSAGE, LAST_ADMIN_MESSAGE, MEMBER_CHANGED_MESSAGE, NO_OP_MESSAGE,
} from "../../src/screens/UsersScreen";
import ProfileScreen from "../../src/screens/ProfileScreen";
import * as userService from "../../src/api/userService";
import * as authService from "../../src/api/authService";
import { flush, renderAs, USERS } from "../helpers/render";

jest.mock("../../src/api/userService");
jest.mock("../../src/api/authService");
jest.mock("../../src/api/lessonService");
jest.mock("../../src/api/accountService", () => ({ ...jest.requireActual("../../src/api/accountService"), createAccount: jest.fn() }));
const accountService = require("../../src/api/accountService");
const lessonService = require("../../src/api/lessonService");

describe("Membri", () => {
  beforeEach(() => {
    lessonService.getClassesForPicker.mockResolvedValue([]);
    userService.getMembers.mockResolvedValue([
      { membershipId: "m1", userId: "u-admin", email: "admin@scuola.it", status: "active", roles: ["InstitutionAdmin"] },
      { membershipId: "m2", userId: "u2", email: "anna@scuola.it", status: "active", roles: ["Teacher"], revision: 3 },
      { membershipId: "m3", userId: "u3", email: "luca@scuola.it", status: "suspended", roles: ["Student"] },
    ]);
  });

  test("elenco con stato, ruoli e '(tu)'; ricerca per email", async () => {
    renderAs("admin", <UsersScreen />);
    await flush();
    expect(screen.getByText("admin@scuola.it (tu)")).toBeTruthy();
    expect(screen.getByText("Sospeso")).toBeTruthy();
    expect(screen.getByText("Docente")).toBeTruthy();
    fireEvent.changeText(screen.getByPlaceholderText("Cerca per email…"), "anna");
    expect(screen.queryByText("luca@scuola.it")).toBeNull();
  });

  test("cambio di ruolo: serve il motivo", async () => {
    userService.mutateMember.mockResolvedValue({ status: "active", roles: ["Teacher", "Staff"] });
    renderAs("admin", <UsersScreen />);
    await flush();
    fireEvent.press(screen.getAllByLabelText("Gestisci membro")[1]);
    expect(screen.getByText("Gestisci membro")).toBeTruthy();
    // Ruoli: Amministratore, Segreteria, Docente, Studente → "Togli" solo per Docente
    fireEvent.press(screen.getAllByText("Aggiungi")[1]);
    expect(Alert.alert).toHaveBeenLastCalledWith("Attenzione", "Indica il motivo della modifica (resta nello storico).");
    fireEvent.changeText(screen.getByPlaceholderText("Es. incarico di segreteria"), "Incarico di segreteria");
    await act(async () => { fireEvent.press(screen.getAllByText("Aggiungi")[1]); });
    expect(userService.mutateMember).toHaveBeenCalledWith("m2", { action: "AddRole", role: "Staff", reason: "Incarico di segreteria", expectedRevision: 3 }, "tok");
    expect(await screen.findAllByText("Togli")).toHaveLength(2);
  });

  test("il dialog usa la revisione del dettaglio appena letto e lo rilegge dopo la modifica", async () => {
    userService.getMember.mockResolvedValueOnce({ membershipId: "m2", userId: "u2", email: "anna@scuola.it", status: "active", roles: ["Teacher"], revision: 7 })
      .mockResolvedValueOnce({ membershipId: "m2", userId: "u2", email: "anna@scuola.it", status: "suspended", roles: ["Teacher"], revision: 8 });
    userService.mutateMember.mockResolvedValue({ status: "suspended", roles: ["Teacher"], revision: 8 });
    renderAs("admin", <UsersScreen />);
    await flush();
    await act(async () => { fireEvent.press(screen.getAllByLabelText("Gestisci membro")[1]); });
    expect(userService.getMember).toHaveBeenCalledWith("m2", "tok");
    fireEvent.changeText(screen.getByPlaceholderText("Es. incarico di segreteria"), "Assenza");
    await act(async () => { fireEvent.press(screen.getByText("Sospendi membro")); });
    expect(userService.mutateMember).toHaveBeenCalledWith("m2", expect.objectContaining({ action: "Suspend", expectedRevision: 7 }), "tok");
    expect(userService.getMember).toHaveBeenCalledTimes(2);
  });

  test("sospensione e riattivazione; non su se stessi", async () => {
    userService.mutateMember.mockResolvedValue({ status: "suspended", roles: ["Teacher"] });
    renderAs("admin", <UsersScreen />);
    await flush();
    fireEvent.press(screen.getAllByLabelText("Gestisci membro")[0]);
    expect(screen.queryByText("Sospendi membro")).toBeNull();
    fireEvent.press(screen.getByText("✕"));

    fireEvent.press(screen.getAllByLabelText("Gestisci membro")[1]);
    fireEvent.changeText(screen.getByPlaceholderText("Es. incarico di segreteria"), "Assenza prolungata");
    await act(async () => { fireEvent.press(screen.getByText("Sospendi membro")); });
    expect(userService.mutateMember).toHaveBeenCalledWith("m2", expect.objectContaining({ action: "Suspend" }), "tok");
    expect(await screen.findByText("Riattiva membro")).toBeTruthy();
  });

  describe("nuovo account", () => {
    beforeEach(() => { lessonService.getClassesForPicker.mockResolvedValue([{ id: "c1", name: "SW-25A" }]); });

    async function openAndFill({ roles = ["Studente"] } = {}) {
      renderAs("admin", <UsersScreen />);
      await flush();
      fireEvent.press(screen.getByText("+ Account"));
      fireEvent.changeText(screen.getByLabelText("Nome"), "Anna Maria");
      fireEvent.changeText(screen.getByLabelText("Cognome"), "De Luca");
      fireEvent.changeText(screen.getByLabelText("Codice fiscale"), "rssmra80a01h501u");
      roles.forEach(r => fireEvent.press(screen.getAllByText(r).at(-1))); // la casella del modulo (i badge dell'elenco vengono prima)
      fireEvent.changeText(screen.getByLabelText("Motivazione (resta nello storico)"), "Iscrizione al corso");
    }

    test("tutti i ruoli per l'admin, controlli prima dell'invio", async () => {
      renderAs("admin", <UsersScreen />);
      await flush();
      fireEvent.press(screen.getByText("+ Account"));
      ["Studente", "Docente", "Segreteria", "Amministratore"].forEach(r => expect(screen.getAllByText(r).length).toBeGreaterThan(0));
      fireEvent.press(screen.getByText("Crea account"));
      expect(screen.getByText("Codice fiscale non valido: servono 16 caratteri.")).toBeTruthy();
      expect(screen.getByText("Scegli almeno un ruolo.")).toBeTruthy();
      expect(accountService.createAccount).not.toHaveBeenCalled();
    });

    test("account creato: email e password temporanea mostrate una volta, elenco ricaricato", async () => {
      accountService.createAccount.mockResolvedValue({ email: "anna.maria.de.luca@its.example", temporaryPassword: "AbCdE-FgHjK", roles: ["Student"] });
      await openAndFill();
      fireEvent.press(screen.getByText("Nessuna classe"));
      fireEvent.press(screen.getAllByText("SW-25A").at(-1));
      await act(async () => { fireEvent.press(screen.getByText("Crea account")); });
      const [body] = accountService.createAccount.mock.calls[0];
      expect(body).toMatchObject({ firstName: "Anna Maria", lastName: "De Luca", fiscalCode: "RSSMRA80A01H501U", roles: ["Student"], classId: "c1", reason: "Iscrizione al corso" });
      expect(typeof body.eventId).toBe("string");
      expect(screen.getByText("anna.maria.de.luca@its.example")).toBeTruthy();
      expect(screen.getByText("AbCdE-FgHjK")).toBeTruthy();
      expect(screen.getByText(/non sarà più visibile/)).toBeTruthy();
      expect(userService.getMembers).toHaveBeenCalledTimes(2);
    });

    test("persona già attiva altrove: nessuna password, si usa quella che ha", async () => {
      accountService.createAccount.mockResolvedValue({ email: "anna@its.example", temporaryPassword: null });
      await openAndFill({ roles: ["Docente"] });
      await act(async () => { fireEvent.press(screen.getByText("Crea account")); });
      expect(screen.getByText(/ha già un account attivo/)).toBeTruthy();
    });

    test("server occupato: si riprova con lo stesso eventId; modificando il modulo cambia", async () => {
      accountService.createAccount.mockRejectedValueOnce(Object.assign(new Error("x"), { status: 503 }));
      accountService.createAccount.mockResolvedValueOnce({ email: "a@b", temporaryPassword: "x" });
      await openAndFill();
      await act(async () => { fireEvent.press(screen.getByText("Crea account")); });
      expect(screen.getByText(/Il server è occupato/)).toBeTruthy();
      await act(async () => { fireEvent.press(screen.getByText("Crea account")); });
      const [first, second] = accountService.createAccount.mock.calls.map(c => c[0].eventId);
      expect(second).toBe(first);
    });

    test("modifica concorrente (409 ConcurrentChange)", async () => {
      accountService.createAccount.mockRejectedValue(Object.assign(new Error("x"), { status: 409, code: "ConcurrentChange" }));
      await openAndFill();
      await act(async () => { fireEvent.press(screen.getByText("Crea account")); });
      expect(screen.getByText("Modifica concorrente: ricarica e riprova.")).toBeTruthy();
    });

    test("409 senza code: titolo del server", async () => {
      accountService.createAccount.mockRejectedValue(Object.assign(new Error("x"), { status: 409, problem: "Indirizzo email non disponibile" }));
      await openAndFill();
      await act(async () => { fireEvent.press(screen.getByText("Crea account")); });
      expect(screen.getByText("Indirizzo email non disponibile")).toBeTruthy();
    });

    test("409 senza code né titolo: messaggio neutro", async () => {
      accountService.createAccount.mockRejectedValue(Object.assign(new Error("x"), { status: 409 }));
      await openAndFill();
      await act(async () => { fireEvent.press(screen.getByText("Crea account")); });
      expect(screen.getByText("Impossibile creare l'account: conflitto con dati esistenti.")).toBeTruthy();
      expect(screen.queryByText(/già nell'istituto/)).toBeNull();
    });
  });

  test("409 generico: ricarica il membro e messaggio neutro (niente 'ultimo amministratore')", async () => {
    userService.mutateMember.mockRejectedValue(Object.assign(new Error("Conflitto sulla membership istituzionale"),
      { status: 409, problem: "Conflitto sulla membership istituzionale" }));
    userService.getMember.mockResolvedValueOnce({ membershipId: "m2", email: "anna@scuola.it", status: "active", roles: ["Teacher"], revision: 3 })
      .mockResolvedValueOnce({ membershipId: "m2", email: "anna@scuola.it", status: "active", roles: ["Teacher", "Staff"], revision: 4 });
    renderAs("admin", <UsersScreen />);
    await flush();
    await act(async () => { fireEvent.press(screen.getAllByLabelText("Gestisci membro")[1]); });
    fireEvent.changeText(screen.getByPlaceholderText("Es. incarico di segreteria"), "prova");
    await act(async () => { fireEvent.press(screen.getAllByText("Aggiungi")[1]); });
    expect(userService.getMember).toHaveBeenCalledTimes(2);
    expect(Alert.alert).toHaveBeenLastCalledWith("Modifica non applicata", MEMBER_CHANGED_MESSAGE);
    expect(Alert.alert).not.toHaveBeenLastCalledWith(expect.anything(), expect.stringMatching(/ultimo amministratore/));
    // i dati ricaricati sono quelli mostrati: ora la Segreteria risulta già assegnata
    expect(screen.getAllByText("Togli")).toHaveLength(2);
  });

  test.each([
    ["LastAdministrator", LAST_ADMIN_MESSAGE],
    ["StaleRevision", MEMBER_CHANGED_MESSAGE],
    ["NoOp", NO_OP_MESSAGE],
    ["EventIdConflict", EVENT_CONFLICT_MESSAGE],
    ["InvalidTransition", INVALID_TRANSITION_MESSAGE],
    ["ConcurrentChange", MEMBER_CHANGED_MESSAGE],
    ["CodiceSconosciuto", MEMBER_CHANGED_MESSAGE],
  ])("409 con code %s: messaggio dedicato e membro ricaricato", async (code, message) => {
    userService.mutateMember.mockRejectedValue(Object.assign(new Error("x"), { status: 409, code }));
    userService.getMember.mockResolvedValue({ membershipId: "m1", userId: "u-admin", email: "admin@scuola.it", status: "active", roles: ["InstitutionAdmin"], revision: 1 });
    renderAs("admin", <UsersScreen />);
    await flush();
    await act(async () => { fireEvent.press(screen.getAllByLabelText("Gestisci membro")[0]); });
    fireEvent.changeText(screen.getByPlaceholderText("Es. incarico di segreteria"), "prova");
    await act(async () => { fireEvent.press(screen.getAllByText("Togli")[0]); });
    expect(Alert.alert).toHaveBeenLastCalledWith("Modifica non applicata", message);
    expect(userService.getMember).toHaveBeenCalledTimes(2); // apertura + ricarica dopo il 409
  });
});

describe("Profilo", () => {
  const teacher = { ...USERS.teacher, displayName: "Prof. Neri", institutionName: "ITS Umbria" };

  test("dati dell'account e uscita", () => {
    const onLogout = jest.fn();
    renderAs("teacher", <ProfileScreen onLogout={onLogout} />, { user: teacher });
    ["Anna Neri", "ITS Umbria", "Docente", "Prof. Neri"].forEach(t => expect(screen.getByText(t)).toBeTruthy());
    expect(screen.getAllByText("docente@scuola.it").length).toBeGreaterThan(0);
    fireEvent.press(screen.getByText("Logout"));
    expect(onLogout).toHaveBeenCalled();
  });

  test("cambio del nome visualizzato", async () => {
    authService.freshTicket.mockResolvedValue("tk");
    authService.updateDisplayName.mockResolvedValue();
    const { auth } = renderAs("teacher", <ProfileScreen onLogout={jest.fn()} />, { user: teacher });
    fireEvent.press(screen.getByText("Modifica nome visualizzato"));
    fireEvent.changeText(screen.getByPlaceholderText("Mario Rossi"), "  Anna N.  ");
    await act(async () => { fireEvent.press(screen.getByText("Salva")); });
    expect(authService.updateDisplayName).toHaveBeenCalledWith("tk", "  Anna N.  ");
    expect(auth.updateUser).toHaveBeenCalledWith({ displayName: "Anna N." });
  });

  test("nome vuoto: avviso", () => {
    renderAs("teacher", <ProfileScreen onLogout={jest.fn()} />, { user: teacher });
    fireEvent.press(screen.getByText("Modifica nome visualizzato"));
    fireEvent.changeText(screen.getByPlaceholderText("Mario Rossi"), "  ");
    fireEvent.press(screen.getByText("Salva"));
    expect(Alert.alert).toHaveBeenCalledWith("Attenzione", "Inserisci un nome");
  });

  describe("cambio password", () => {
    const open = () => {
      fireEvent.press(screen.getByText("Cambia password"));
      return screen.getAllByDisplayValue("");
    };

    test("campi mancanti e requisiti della nuova password", () => {
      renderAs("teacher", <ProfileScreen onLogout={jest.fn()} />, { user: teacher });
      const [old, next] = open();
      fireEvent.press(screen.getByText("Salva"));
      expect(Alert.alert).toHaveBeenLastCalledWith("Attenzione", "Inserisci vecchia password, nuova password e conferma");
      fireEvent.changeText(old, "Vecchia1!");
      fireEvent.changeText(next, "debole");
      expect(screen.getByText("La password deve avere almeno 8 caratteri.")).toBeTruthy();
    });

    test("password attuale errata", async () => {
      authService.login.mockRejectedValue(new Error("401"));
      renderAs("teacher", <ProfileScreen onLogout={jest.fn()} />, { user: teacher });
      const [old, next, confirm] = open();
      fireEvent.changeText(old, "Sbagliata1!");
      fireEvent.changeText(next, "Nuova123!");
      fireEvent.changeText(confirm, "Nuova123!");
      await act(async () => { fireEvent.press(screen.getByText("Salva")); });
      expect(Alert.alert).toHaveBeenLastCalledWith("Errore", "La password attuale non è corretta.");
      expect(authService.changePassword).not.toHaveBeenCalled();
    });

    test("password cambiata: il server revoca le sessioni, si torna al login", async () => {
      authService.login.mockResolvedValue({ ticket: "fresh" });
      authService.changePassword.mockResolvedValue();
      const { auth } = renderAs("teacher", <ProfileScreen onLogout={jest.fn()} />, { user: teacher });
      const [old, next, confirm] = open();
      fireEvent.changeText(old, "Vecchia1!");
      fireEvent.changeText(next, "Nuova123!");
      fireEvent.changeText(confirm, "Nuova123!");
      await act(async () => { fireEvent.press(screen.getByText("Salva")); });
      expect(authService.login).toHaveBeenCalledWith("docente@scuola.it", "Vecchia1!");
      expect(authService.changePassword).toHaveBeenCalledWith("fresh", "Vecchia1!", "Nuova123!");
      await waitFor(() => expect(auth.logout).toHaveBeenCalledWith("Password aggiornata: accedi di nuovo con la nuova password."));
    });
  });
});
