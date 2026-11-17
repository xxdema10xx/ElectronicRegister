// src/components/IconButton.js
// Pulsante con sola icona (modifica, elimina…). Su web è compatto, con sfondo al passaggio del mouse e
// la descrizione nel tooltip del browser; su nativo l'icona resta grande, comoda da toccare.
import { Platform, Pressable, TouchableOpacity } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C } from "../constants/theme";

const isWeb = Platform.OS === "web";

export default function IconButton({ icon, label, onPress, color = C.footer, size = 32 }) {
  if (!isWeb) {
    return (
      <TouchableOpacity onPress={onPress} accessibilityRole="button" accessibilityLabel={label}>
        <Ionicons name={icon} size={size} color={color} />
      </TouchableOpacity>
    );
  }
  return (
    <Pressable onPress={onPress} accessibilityRole="button" accessibilityLabel={label}
      ref={(node) => node?.setAttribute?.("title", label || "")}
      style={({ hovered }) => ({ width: 34, height: 34, borderRadius: 8, alignItems: "center", justifyContent: "center",
        backgroundColor: hovered ? "#E0EEF8" : "transparent" })}>
      <Ionicons name={icon} size={20} color={color} />
    </Pressable>
  );
}
