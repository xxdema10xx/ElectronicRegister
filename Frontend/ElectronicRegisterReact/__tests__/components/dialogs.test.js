// Dialog dell'app (avvisi e conferme) e funzioni notify / confirmAction / confirmLeave.
import { Alert } from "react-native";
import { act, fireEvent, render, screen } from "@testing-library/react-native";
import { DialogHost, showDialog } from "../../src/components/AppDialog";
import { confirmAction, confirmBrowser, confirmLeave, notify, notifyBrowser } from "../../src/utils/dialogs";

// Il dialog sta sopra la schermata: tra più testi uguali si prende l'ultimo
const press = (label) => act(() => { fireEvent.press(screen.getAllByText(label).at(-1)); });
const shown = (text) => screen.queryByText(text) !== null;

beforeEach(() => render(<DialogHost />));

test("notify: titolo, messaggio e OK che chiude", () => {
  act(() => notify("Salvato", "Tutto a posto"));
  expect(shown("Salvato")).toBe(true);
  expect(shown("Tutto a posto")).toBe(true);
  press("OK");
  expect(screen.queryByText("OK")).toBeNull();
});

test("confirmAction: Annulla non fa nulla, l'azione chiama onOk", () => {
  const onOk = jest.fn();
  act(() => confirmAction("Conferma", "Eliminare?", onOk));
  press("Annulla");
  expect(onOk).not.toHaveBeenCalled();
  act(() => confirmAction("Conferma", "Eliminare?", onOk));
  press("Elimina");
  expect(onOk).toHaveBeenCalledTimes(1);
});

test("etichette personalizzate", () => {
  const onOk = jest.fn();
  act(() => confirmAction("Pubblica", "Sicuro?", onOk, "Pubblica", { cancelLabel: "No" }));
  expect(shown("No")).toBe(true);
  press("Pubblica");
  expect(onOk).toHaveBeenCalled();
});

test("confirmLeave: 'Esci senza salvare' oppure 'Continua a modificare'", () => {
  const onLeave = jest.fn();
  const onStay = jest.fn();
  act(() => confirmLeave(onLeave, { onStay }));
  expect(shown("Modifiche non salvate")).toBe(true);
  press("Continua a modificare");
  expect(onStay).toHaveBeenCalled();
  expect(onLeave).not.toHaveBeenCalled();
  act(() => confirmLeave(onLeave, { message: "Si perde tutto" }));
  expect(shown("Si perde tutto")).toBe(true);
  press("Esci senza salvare");
  expect(onLeave).toHaveBeenCalled();
});

test("un dialog alla volta, in coda", () => {
  act(() => { notify("Primo"); notify("Secondo"); });
  expect(shown("Primo")).toBe(true);
  press("OK");
  expect(shown("Secondo")).toBe(true);
  press("OK");
  expect(screen.queryByText("OK")).toBeNull();
});

test("showDialog senza pulsanti: OK", () => {
  act(() => showDialog({ title: "Info" }));
  press("OK");
  expect(screen.queryByText("OK")).toBeNull();
});

test("svolgimento esame sull'app: Alert di sistema", () => {
  notifyBrowser("Titolo", "Messaggio");
  expect(Alert.alert).toHaveBeenCalledWith("Titolo", "Messaggio");
  const onOk = jest.fn();
  confirmBrowser("Conferma", "Sicuro?", onOk, "Consegna");
  const buttons = Alert.alert.mock.calls[1][2];
  expect(buttons.map(b => b.text)).toEqual(["Annulla", "Consegna"]);
  buttons[1].onPress();
  expect(onOk).toHaveBeenCalled();
});

test("D-01: errori già gestiti (sessione terminata) non aprono un secondo dialog", () => {
  const { HANDLED_MESSAGE } = require("../../src/utils/handledErrors");
  act(() => { showDialog({ title: "Errore", message: HANDLED_MESSAGE }); notify("Errore", `Salvataggio: ${HANDLED_MESSAGE}`); });
  expect(shown("Errore")).toBe(false);
  act(() => notifyBrowser("Errore", HANDLED_MESSAGE));
  expect(Alert.alert).not.toHaveBeenCalled();
  act(() => notify("Errore", "Altro problema"));
  expect(shown("Altro problema")).toBe(true);
});
