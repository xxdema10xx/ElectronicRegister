// src/components/DateTimeField.js
// Data e ora in un solo campo. Valore: "AAAA-MM-GGTHH:mm" (ora locale), stringa vuota se non impostato.
// Web: selettore nativo del browser. App: un campo di testo "AAAA-MM-GG HH:mm".
import { Platform, Text, TextInput, View } from "react-native";
import { C, s } from "../constants/theme";

export default function DateTimeField({ label, value, onChange }) {
  return (
    <View style={{ marginBottom: 14 }}>
      {label && <Text style={s.label}>{label}</Text>}
      {Platform.OS === "web" ? (
        <View style={s.selectBox}>
          <input
            type="datetime-local"
            value={value || ""}
            onChange={(e) => onChange(e.target.value)}
            style={{ width: "100%", height: "100%", border: "none", outline: "none", background: "transparent", fontFamily: "inherit", fontSize: "15px", color: value ? C.text : C.textLight, cursor: "pointer" }}
          />
        </View>
      ) : (
        <TextInput
          style={s.input}
          placeholder="AAAA-MM-GG HH:mm"
          placeholderTextColor={C.textLight}
          value={(value || "").replace("T", " ")}
          onChangeText={(v) => onChange(v.trim().replace(" ", "T"))}
          autoCapitalize="none"
        />
      )}
    </View>
  );
}
