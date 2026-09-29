// src/utils/dialogs.js
// Alert.alert non fa nulla su web: qui si usano window.alert / window.confirm.
import { Alert, Platform } from "react-native";

export function notify(title, message) {
  if (Platform.OS === "web") window.alert(message ? `${title}\n\n${message}` : title);
  else Alert.alert(title, message);
}

export function confirmAction(title, message, onOk, okLabel = "Elimina") {
  if (Platform.OS === "web") {
    if (window.confirm(`${title}\n\n${message}`)) onOk();
  } else {
    Alert.alert(title, message, [
      { text: "Annulla" },
      { text: okLabel, style: "destructive", onPress: onOk },
    ]);
  }
}
