// src/utils/webAlert.js
// Su web Alert.alert di react-native non fa nulla: lo sostituisce con il dialog dell'app
// (components/AppDialog.js), così tutte le conferme ed errori delle schermate funzionano anche nel browser
// con lo stesso aspetto dell'app. Pulsanti: "destructive" → rosso; "cancel" o "Annulla" → annulla (Esc);
// gli altri → azione primaria (Invio).
import { Alert, Platform } from "react-native";
import { showDialog } from "../components/AppDialog";
import { isHandledError } from "./handledErrors";

if (Platform.OS !== "web") {
  // Sull'app resta l'Alert di sistema, ma senza i dialog di errori già gestiti (utils/handledErrors.js)
  const systemAlert = Alert.alert;
  Alert.alert = (title, message, ...rest) => { if (!isHandledError(message)) systemAlert(title, message, ...rest); };
}

if (Platform.OS === "web") {
  Alert.alert = (title, message, buttons) => {
    const list = buttons && buttons.length ? buttons : [{ text: "OK" }];
    showDialog({
      title, message,
      buttons: list.map(b => ({
        text: b.text || "OK",
        style: b.style === "destructive" ? "destructive" : b.style === "cancel" || b.text === "Annulla" ? "cancel" : "primary",
        onPress: b.onPress,
      })),
    });
  };
}
