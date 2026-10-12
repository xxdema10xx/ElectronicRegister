// src/utils/webAlert.js
// Su web Alert.alert di react-native non fa nulla: lo sostituisce con window.alert / window.confirm,
// così tutte le conferme ed errori delle schermate funzionano anche nel browser.
import { Alert, Platform } from "react-native";

if (Platform.OS === "web") {
  Alert.alert = (title, message, buttons) => {
    const text = message ? `${title}\n\n${message}` : title;
    if (!buttons || buttons.length === 0) { window.alert(text); return; }
    if (buttons.length === 1) { window.alert(text); buttons[0].onPress?.(); return; }
    const action = buttons.find(b => b.style === "destructive") || buttons.find(b => b.style !== "cancel" && b.text !== "Annulla") || buttons[buttons.length - 1];
    if (window.confirm(text)) action.onPress?.();
    else buttons.find(b => b.style === "cancel" || b.text === "Annulla")?.onPress?.();
  };
}
