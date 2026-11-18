// Impostazioni del calendario: errori del salvataggio con un testo pertinente.
import { act, fireEvent, screen } from "@testing-library/react-native";
import CalendarSettingsForm, { SETTINGS_CONFLICT_MESSAGE } from "../../src/components/CalendarSettingsForm";
import * as calendarService from "../../src/api/calendarService";
import { calendarErrorMessage } from "../../src/api/calendarImportService";
import { renderAs } from "../helpers/render";

jest.mock("../../src/api/calendarService", () => ({ ...jest.requireActual("../../src/api/calendarService"), saveCalendarSettings: jest.fn() }));

const settings = { timeZoneId: "Europe/Rome", approvalLeadDays: 5, revision: 3 };

async function saveWithLeadDays(value) {
  fireEvent.changeText(screen.getByLabelText("Giorni di anticipo per l'approvazione del mese (0–366)"), value);
  fireEvent.changeText(screen.getByLabelText("Motivo (obbligatorio, resta nello storico)"), "Seconda scheda");
  await act(async () => { fireEvent.press(screen.getByText("Salva impostazioni")); });
}

test("conflitto di revisione (salvataggio da un'altra scheda): testo sulle impostazioni, non sul mese", async () => {
  calendarService.saveCalendarSettings.mockRejectedValue(Object.assign(new Error("x"), { status: 409, code: "calendar.revision-conflict" }));
  renderAs("admin", <CalendarSettingsForm settings={settings} />);
  await saveWithLeadDays("6");
  expect(calendarService.saveCalendarSettings).toHaveBeenCalledWith(expect.objectContaining({ expectedRevision: 3, approvalLeadDays: 6 }), "tok");
  expect(screen.getByText(SETTINGS_CONFLICT_MESSAGE)).toBeTruthy();
  expect(SETTINGS_CONFLICT_MESSAGE).toBe("Le impostazioni sono state modificate nel frattempo: ricarica e riprova.");
  expect(screen.queryByText(/Il mese è stato modificato/)).toBeNull();
});

test("gli altri errori restano quelli del calendario; il conflitto sul mese non cambia", async () => {
  calendarService.saveCalendarSettings.mockRejectedValue(Object.assign(new Error("x"), { status: 400, code: "calendar.invalid-time-zone" }));
  renderAs("staff", <CalendarSettingsForm settings={settings} />);
  await saveWithLeadDays("6");
  expect(screen.getByText("Fuso orario non riconosciuto dal server (es. Europe/Rome).")).toBeTruthy();
  expect(calendarErrorMessage({ status: 409, code: "calendar.revision-conflict" })).toMatch(/Il mese è stato modificato/);
});

test("valori fuori intervallo: bloccati prima dell'invio", async () => {
  renderAs("admin", <CalendarSettingsForm settings={settings} />);
  for (const value of ["367", "-1", "abc"]) {
    await saveWithLeadDays(value);
    expect(screen.getByText("I giorni di anticipo devono essere un numero tra 0 e 366.")).toBeTruthy();
  }
  expect(calendarService.saveCalendarSettings).not.toHaveBeenCalled();
});
