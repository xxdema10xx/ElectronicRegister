// src/utils/webAlert.js
// Su web Alert.alert di react-native non fa nulla: lo sostituisce con il dialog dell'app
// (components/AppDialog.js), così tutte le conferme ed errori delle schermate funzionano anche nel browser
// con lo stesso aspetto dell'app. Pulsanti: "destructive" → rosso; "cancel" o "Annulla" → annulla (Esc);
// gli altri → azione primaria (Invio).
import { Alert, Platform } from "react-native";
import { showDialog } from "../components/AppDialog";

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
