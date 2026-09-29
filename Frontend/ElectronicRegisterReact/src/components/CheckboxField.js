// src/components/CheckboxField.js
import { Text, TouchableOpacity } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C } from "../constants/theme";

export default function CheckboxField({ label, value, onChange }) {
  return (
    <TouchableOpacity onPress={() => onChange(!value)} activeOpacity={0.7}
      style={{ flexDirection: "row", alignItems: "center", gap: 10, marginBottom: 14 }}>
      <Ionicons name={value ? "checkbox" : "square-outline"} size={26} color={value ? C.primary : C.textLight} />
      <Text style={{ color: C.text, fontSize: 15 }}>{label}</Text>
    </TouchableOpacity>
  );
}
