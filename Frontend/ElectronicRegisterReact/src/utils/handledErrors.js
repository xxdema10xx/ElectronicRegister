// src/utils/handledErrors.js
// Errori già gestiti in modo globale: dopo un 401 (o una membership sospesa/revocata) l'app torna al login e
// mostra UN dialog "Sessione terminata". Le richieste ancora in corso falliscono con lo stesso motivo e i
// `catch` delle schermate mostrerebbero un secondo dialog "Errore": quegli errori hanno `handled: true` e il
// messaggio HANDLED_MESSAGE, che i dialog dell'app (AppDialog, notify, Alert) ignorano.
export const HANDLED_MESSAGE = "Sessione terminata: accedi di nuovo.";

export function markHandled(error) {
  error.handled = true;
  error.message = HANDLED_MESSAGE;
  return error;
}

// Accetta l'errore o il testo che una schermata vuole mostrare (es. `Alert.alert("Errore", e.message)`)
export function isHandledError(errorOrMessage) {
  if (!errorOrMessage) return false;
  if (typeof errorOrMessage === "object") return errorOrMessage.handled === true;
  return String(errorOrMessage).includes(HANDLED_MESSAGE);
}
