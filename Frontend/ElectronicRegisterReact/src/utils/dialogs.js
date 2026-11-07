// src/utils/dialogs.js
// Avvisi e conferme con il dialog dell'app (components/AppDialog.js), su web e sull'app.
// Le firme sono quelle di sempre: notify(title, message) e confirmAction(title, message, onOk, okLabel).
//
// Eccezione: lo svolgimento lato studente (StudentExamsScreen + ExamTaker) usa notifyBrowser / confirmBrowser,
// cioè il comportamento precedente (window.alert / window.confirm su web, Alert di sistema sull'app), per
// non cambiare avvisi, focus, schermo intero e conteggio delle violazioni.
import { Alert, Platform } from "react-native";
import { showDialog } from "../components/AppDialog";

// Etichette di azioni che distruggono o annullano: pulsante rosso e mai scelte con Invio
const DESTRUCTIVE = /elimin|annulla esame|rimuov|cancell|revoc|esci senza|scarta/i;

export function notify(title, message) {
  showDialog({ title, message, buttons: [{ text: "OK", style: "primary" }] });
}

export function confirmAction(title, message, onOk, okLabel = "Elimina", { cancelLabel = "Annulla", destructive } = {}) {
  const isDestructive = destructive ?? DESTRUCTIVE.test(okLabel);
  showDialog({
    title, message,
    buttons: [
      { text: cancelLabel, style: "cancel" },
      { text: okLabel, style: isDestructive ? "destructive" : "primary", onPress: onOk },
    ],
  });
}

// Uscita da una pagina con modifiche non salvate sul server
export function confirmLeave(onLeave, { message, onStay } = {}) {
  showDialog({
    title: "Modifiche non salvate",
    message: message || "Le modifiche non sono ancora state salvate sul server.",
    buttons: [
      { text: "Esci senza salvare", style: "destructive", onPress: onLeave },
      { text: "Continua a modificare", style: "primary", onPress: onStay, escape: true },
    ],
  });
}

// ── Comportamento precedente (solo svolgimento lato studente) ───────────────────────────────
export function notifyBrowser(title, message) {
  if (Platform.OS === "web") window.alert(message ? `${title}\n\n${message}` : title);
  else Alert.alert(title, message);
}

export function confirmBrowser(title, message, onOk, okLabel = "Elimina") {
  if (Platform.OS === "web") {
    if (window.confirm(`${title}\n\n${message}`)) onOk();
  } else {
    Alert.alert(title, message, [
      { text: "Annulla" },
      { text: okLabel, style: "destructive", onPress: onOk },
    ]);
  }
}
