// Login, primo accesso con password temporanea e scelta dell'istituto.
import { Alert } from "react-native";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react-native";
import LoginScreen from "../../src/screens/LoginScreen";
import FirstPasswordScreen from "../../src/screens/FirstPasswordScreen";
import InstitutionPickerScreen from "../../src/screens/InstitutionPickerScreen";
import * as authService from "../../src/api/authService";
import { pressAlertButton } from "../helpers/render";

jest.mock("../../src/api/authService");

describe("Login", () => {
  test("campi vuoti: avviso", () => {
    render(<LoginScreen onLogin={jest.fn()} />);
    fireEvent.press(screen.getByText("Entra"));
    expect(Alert.alert).toHaveBeenCalledWith("Attenzione", "Compila tutti i campi");
    expect(authService.login).not.toHaveBeenCalled();
  });

  test("accesso riuscito: ticket e credenziali (email senza spazi) a chi gestisce la sessione", async () => {
    authService.login.mockResolvedValue({ ticket: "tk", passwordChangeRequired: false });
    const onLogin = jest.fn();
    render(<LoginScreen onLogin={onLogin} />);
    fireEvent.changeText(screen.getByPlaceholderText("allievo_nome@itsumbria.it"), " mario@its.it ");
    fireEvent.changeText(screen.getByPlaceholderText("••••••••"), "Segreta1!");
    await act(async () => { fireEvent.press(screen.getByText("Entra")); });
    expect(authService.login).toHaveBeenCalledWith("mario@its.it", "Segreta1!");
    expect(onLogin).toHaveBeenCalledWith({ ticket: "tk", credentials: { email: "mario@its.it", password: "Segreta1!" }, passwordChangeRequired: false });
  });

  test("credenziali errate: messaggio del server", async () => {
    authService.login.mockRejectedValue(new Error("Email o password non validi"));
    render(<LoginScreen onLogin={jest.fn()} />);
    fireEvent.changeText(screen.getByPlaceholderText("allievo_nome@itsumbria.it"), "a@b.it");
    fireEvent.changeText(screen.getByPlaceholderText("••••••••"), "x");
    await act(async () => { fireEvent.press(screen.getByText("Entra")); });
    expect(Alert.alert).toHaveBeenCalledWith("Errore login", "Email o password non validi");
  });

  test("password temporanea: il login lo segnala", async () => {
    authService.login.mockResolvedValue({ ticket: "tk", passwordChangeRequired: true });
    const onLogin = jest.fn();
    render(<LoginScreen onLogin={onLogin} />);
    fireEvent.changeText(screen.getByPlaceholderText("allievo_nome@itsumbria.it"), "nuovo@its.it");
    fireEvent.changeText(screen.getByPlaceholderText("••••••••"), "Temp-AbC");
    await act(async () => { fireEvent.press(screen.getByText("Entra")); });
    expect(onLogin).toHaveBeenCalledWith(expect.objectContaining({ passwordChangeRequired: true }));
  });

  test("niente registrazione: l'account lo crea l'istituto", () => {
    render(<LoginScreen onLogin={jest.fn()} />);
    expect(screen.queryByText(/Registrati/)).toBeNull();
    expect(screen.getByText(/Lo crea la segreteria del tuo istituto/)).toBeTruthy();
  });

  test("pulsante 'Accedi con Microsoft'", () => {
    render(<LoginScreen onLogin={jest.fn()} />);
    expect(screen.getByText("Accedi con Microsoft")).toBeTruthy();
  });
});

describe("Primo accesso: nuova password al posto di quella temporanea", () => {
  const setup = (onSubmit = jest.fn().mockResolvedValue()) => {
    const onBack = jest.fn();
    render(<FirstPasswordScreen email="nuovo@its.it" temporaryPassword="Temp-AbC!" onSubmit={onSubmit} onBack={onBack} />);
    const [p, c] = screen.getAllByPlaceholderText("••••••••");
    return { onSubmit, onBack, p, c };
  };

  test("spiega cosa fare e per quale account", () => {
    setup();
    expect(screen.getByText(/password temporanea ricevuta dall'istituto per nuovo@its.it/)).toBeTruthy();
  });

  test("regole del server e conferma", () => {
    const { onSubmit, p, c } = setup();
    fireEvent.changeText(p, "corta");
    expect(screen.getByText("La password deve avere almeno 8 caratteri.")).toBeTruthy();
    fireEvent.changeText(p, "Temp-AbC!");
    expect(screen.getByText(/diversa da quella attuale/)).toBeTruthy();
    fireEvent.changeText(p, "mia-password");
    fireEvent.changeText(c, "altra-password");
    expect(screen.getByText("Le password non coincidono")).toBeTruthy();
    fireEvent.press(screen.getByText("Salva e accedi"));
    expect(onSubmit).not.toHaveBeenCalled();
  });

  test("password valida: la invia", async () => {
    const { onSubmit, p, c } = setup();
    fireEvent.changeText(p, "mia-password");
    fireEvent.changeText(c, "mia-password");
    await act(async () => { fireEvent.press(screen.getByText("Salva e accedi")); });
    expect(onSubmit).toHaveBeenCalledWith("mia-password");
  });

  test("errore del cambio: messaggio sotto il modulo", async () => {
    const { p, c } = setup(jest.fn().mockRejectedValue(new Error("Sessione scaduta")));
    fireEvent.changeText(p, "mia-password");
    fireEvent.changeText(c, "mia-password");
    await act(async () => { fireEvent.press(screen.getByText("Salva e accedi")); });
    expect(screen.getByText("Sessione scaduta")).toBeTruthy();
  });

  test("torna al login", () => {
    const { onBack } = setup();
    fireEvent.press(screen.getByText("Torna al login"));
    expect(onBack).toHaveBeenCalled();
  });
});

describe("Scelta dell'istituto", () => {
  const institutions = [
    { institutionId: "i1", membershipId: "m1", name: "ITS Umbria", roles: ["Teacher", "Staff"] },
    { institutionId: "i2", membershipId: "m2", name: "ITS Marche", roles: ["Student"] },
  ];

  test("elenco con i ruoli, scelta e ritorno al login", () => {
    const onSelect = jest.fn();
    const onBack = jest.fn();
    render(<InstitutionPickerScreen institutions={institutions} onSelect={onSelect} onBack={onBack} />);
    expect(screen.getByText("Docente · Segreteria")).toBeTruthy();
    fireEvent.press(screen.getByText("ITS Marche"));
    expect(onSelect).toHaveBeenCalledWith(institutions[1]);
    fireEvent.press(screen.getByText("Torna al login"));
    expect(onBack).toHaveBeenCalled();
  });
});
