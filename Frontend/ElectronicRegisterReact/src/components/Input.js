// src/components/Input.js
import { Text, TextInput, View } from "react-native";
import { C, s } from "../constants/theme";

export default function Input({ label, error, ...props }) {
  return (
    <View style={{ marginBottom: 14 }}>
      {label && <Text style={s.label}>{label}</Text>}
      <TextInput style={[s.input, error && s.inputError]} placeholderTextColor={C.textLight} {...props} />
      {error && <Text style={s.errorText}>{error}</Text>}
    </View>
  );
}
