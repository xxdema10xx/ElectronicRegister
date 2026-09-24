// src/components/Btn.js
import { ActivityIndicator, Text, TouchableOpacity } from "react-native";
import { C, s } from "../constants/theme";

export default function Btn({ label, onPress, style, textStyle, icon, variant = "primary", loading, disabled }) {
  const bg = {
    primary: C.primary, danger: C.danger, ghost: "transparent",
    success: C.success, secondary: C.secondary,
  }[variant];
  const tc = variant === "ghost" ? C.primary : C.white;

  return (
    <TouchableOpacity
      style={[s.btn, { backgroundColor: bg, borderWidth: variant === "ghost" ? 1 : 0, borderColor: C.primary, opacity: disabled || loading ? 0.6 : 1 }, style]}
      onPress={onPress}
      disabled={disabled || loading}
    >
      {loading ? <ActivityIndicator color={C.white} size="small" /> :
        <Text style={[s.btnText, { color: tc }, textStyle]}>{icon ? `${icon}  ` : ""}{label}</Text>}
    </TouchableOpacity>
  );
}
