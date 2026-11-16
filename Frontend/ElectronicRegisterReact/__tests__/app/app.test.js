// Flusso completo dell'app: login → scelta dell'istituto → app; uscita, sessione scaduta, rinnovo del token.
import { Alert } from "react-native";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react-native";
import App from "../../App";
import * as authService from "../../src/api/authService";
import { session } from "../../src/api/session";

jest.mock("../../src/api/authService");
jest.mock("../../src/api/axiosClient", () => {
  let handler = null;
  return { __esModule: true, default: {}, setUnauthorizedHandler: (fn) => { handler = fn; }, getHandler: () => handler, authHeader: () => ({}), ticketHeader: () => ({}) };
});
// L'app vera e propria (sezioni) ha i suoi test: qui un segnaposto con utente e "Esci"
jest.mock("../../src/navigation/AppNav", () => {
  const { Text } = require("react-native");
  return ({ user, onLogout }) => <Text onPress={onLogout}>{`APP ${user.role} ${user.email}`}</Text>;
});

const ITS = { institutionId: "i1", membershipId: "m1", name: "ITS Umbria", roles: ["Teacher"] };
const MARCHE = { institutionId: "i2", membershipId: "m2", name: "ITS Marche", roles: ["Student"] };

beforeEach(() => {
  session.clear();
  authService.login.mockResolvedValue({ ticket: "tk", expiresInSeconds: 300 });
  authService.getAccount.mockResolvedValue({ email: "anna@its.it" });
  authService.getInstitutions.mockResolvedValue([ITS]);
  // Come il servizio vero: il token istituzionale finisce nella sessione (dura 15 minuti)
  authService.selectInstitution.mockImplementation(async () => { session.setAccess("acc", 900); return { token: "acc", expiresInSeconds: 900 }; });
  authService.buildUser.mockImplementation(async (_t, account) => ({ id: "u1", role: "teacher", email: account?.email }));
});

async function login() {
  await waitFor(() => expect(screen.getByText("Accedi")).toBeTruthy());
  fireEvent.changeText(screen.getByPlaceholderText("allievo_nome@itsumbria.it"), "anna@its.it");
  fireEvent.changeText(screen.getByPlaceholderText("••••••••"), "Segreta1!");
  await act(async () => { fireEvent.press(screen.getByText("Entra")); });
}

test("un solo istituto: dal login direttamente all'app", async () => {
  render(<App />);
  await login();
  expect(await screen.findByText("APP teacher anna@its.it")).toBeTruthy();
  expect(authService.selectInstitution).toHaveBeenCalledWith(null, ITS);
  expect(session.credentials).toEqual({ email: "anna@its.it", password: "Segreta1!" });
});

test("più istituti: scelta, poi l'app", async () => {
  authService.getInstitutions.mockResolvedValue([ITS, MARCHE]);
  render(<App />);
  await login();
  expect(await screen.findByText("Scegli l'istituto")).toBeTruthy();
  await act(async () => { fireEvent.press(screen.getByText("ITS Marche")); });
  expect(authService.selectInstitution).toHaveBeenCalledWith(null, MARCHE);
  expect(await screen.findByText(/^APP /)).toBeTruthy();
});

test("dalla scelta dell'istituto si può tornare al login", async () => {
  authService.getInstitutions.mockResolvedValue([ITS, MARCHE]);
  render(<App />);
  await login();
  fireEvent.press(await screen.findByText("Torna al login"));
  expect(await screen.findByText("Accedi")).toBeTruthy();
});

test("account senza istituti: avviso, si resta sul login", async () => {
  authService.getInstitutions.mockResolvedValue([]);
  render(<App />);
  await login();
  expect(Alert.alert).toHaveBeenCalledWith("Nessun istituto", expect.stringMatching(/Contatta la segreteria/));
  expect(screen.getByText("Accedi")).toBeTruthy();
});

test("ingresso nell'istituto non riuscito: avviso", async () => {
  authService.selectInstitution.mockRejectedValue(new Error("Membro sospeso"));
  render(<App />);
  await login();
  expect(Alert.alert).toHaveBeenCalledWith("Accesso non riuscito", "Membro sospeso");
});

describe("primo accesso con la password temporanea", () => {
  beforeEach(() => {
    // Il primo login (password temporanea) chiede il cambio; quello con la nuova password no
    authService.login.mockImplementation(async (_email, password) => {
      session.setTicket(`tk-${password}`, 300);
      return { ticket: `tk-${password}`, expiresInSeconds: 300, passwordChangeRequired: password === "Segreta1!" };
    });
    authService.changePassword.mockResolvedValue();
  });

  async function chooseNewPassword(pwd) {
    expect(await screen.findByText("Scegli la tua password")).toBeTruthy();
    const [p, c] = screen.getAllByPlaceholderText("••••••••");
    fireEvent.changeText(p, pwd);
    fireEvent.changeText(c, pwd);
    await act(async () => { fireEvent.press(screen.getByText("Salva e accedi")); });
  }

  test("prima di tutto si cambia la password, poi si entra con la nuova", async () => {
    render(<App />);
    await login();
    expect(authService.getInstitutions).not.toHaveBeenCalled(); // con il ticket temporaneo il server rifiuterebbe
    await chooseNewPassword("Nuova-pass1");
    expect(authService.changePassword).toHaveBeenCalledWith("tk-Segreta1!", "Segreta1!", "Nuova-pass1");
    expect(authService.login).toHaveBeenLastCalledWith("anna@its.it", "Nuova-pass1");
    expect(await screen.findByText("APP teacher anna@its.it")).toBeTruthy();
    expect(session.credentials).toEqual({ email: "anna@its.it", password: "Nuova-pass1" }); // per i rinnovi del token
  });

  test("ticket scaduto durante il cambio: messaggio chiaro", async () => {
    authService.changePassword.mockRejectedValue(Object.assign(new Error("401"), { status: 401 }));
    render(<App />);
    await login();
    await chooseNewPassword("Nuova-pass1");
    expect(screen.getByText(/La sessione è scaduta/)).toBeTruthy();
  });

  test("dal cambio password si può tornare al login", async () => {
    render(<App />);
    await login();
    fireEvent.press(await screen.findByText("Torna al login"));
    expect(await screen.findByText("Accedi")).toBeTruthy();
    expect(session.credentials).toBeNull();
  });
});

test("niente registrazione nella schermata di accesso", async () => {
  render(<App />);
  expect(await screen.findByText("Accedi")).toBeTruthy();
  expect(screen.queryByText(/Registrati/)).toBeNull();
});

test("'Esci': si torna al login e la sessione si azzera", async () => {
  render(<App />);
  await login();
  fireEvent.press(await screen.findByText(/^APP /));
  expect(await screen.findByText("Accedi")).toBeTruthy();
  expect(session.accessToken).toBeNull();
  expect(session.credentials).toBeNull();
});

test("token rifiutato dal server (401): si torna al login con un avviso", async () => {
  render(<App />);
  await login();
  await screen.findByText(/^APP /);
  const { getHandler } = require("../../src/api/axiosClient");
  await act(async () => { getHandler()(); });
  expect(await screen.findByText("Accedi")).toBeTruthy();
  expect(Alert.alert).toHaveBeenCalledWith("Sessione terminata", "La sessione è scaduta o è stata revocata: accedi di nuovo.");
});

test("rinnovo del token un minuto prima della scadenza, con le credenziali in memoria", async () => {
  jest.useFakeTimers();
  try {
    render(<App />);
    await login();
    await act(async () => { await jest.runOnlyPendingTimersAsync(); });
    expect(screen.getByText(/^APP /)).toBeTruthy();
    authService.login.mockClear();
    authService.selectInstitution.mockClear();
    await act(async () => { jest.advanceTimersByTime(14 * 60 * 1000 + 1000); });
    expect(authService.login).toHaveBeenCalledWith("anna@its.it", "Segreta1!");
    expect(authService.selectInstitution).toHaveBeenCalledWith("tk", null);
    expect(screen.getByText(/^APP /)).toBeTruthy();
  } finally { jest.useRealTimers(); }
});

test("rinnovo impossibile: si torna al login con un avviso", async () => {
  jest.useFakeTimers();
  try {
    render(<App />);
    await login();
    await act(async () => { await jest.runOnlyPendingTimersAsync(); });
    authService.login.mockRejectedValue(new Error("password cambiata"));
    await act(async () => { jest.advanceTimersByTime(14 * 60 * 1000 + 1000); });
    expect(Alert.alert).toHaveBeenCalledWith("Sessione terminata", "Non è stato possibile rinnovare la sessione: accedi di nuovo.");
    expect(screen.getByText("Accedi")).toBeTruthy();
  } finally { jest.useRealTimers(); }
});
