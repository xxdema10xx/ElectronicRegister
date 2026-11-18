// src/components/WeekNavigator.js
// ‹ settimana precedente · Oggi · settimana successiva ›, con l'intervallo mostrato ("16–22 novembre 2026").
// Nessun limite: si può scorrere oltre il mese importato in entrambe le direzioni.
import { Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C } from "../constants/theme";
import { currentWeek, formatWeekRange } from "../api/lessonService";

function NavButton({ icon, label, text, onPress }) {
  return (
    <TouchableOpacity onPress={onPress} accessibilityRole="button" accessibilityLabel={label}
      style={{ flexDirection: "row", alignItems: "center", gap: 4, paddingVertical: 8, paddingHorizontal: 10, borderRadius: 8, borderWidth: 1, borderColor: C.border, backgroundColor: C.white, minHeight: 40 }}>
      {!!icon && <Ionicons name={icon} size={18} color={C.primary} />}
      {!!text && <Text style={{ color: C.primary, fontWeight: "700", fontSize: 13 }}>{text}</Text>}
    </TouchableOpacity>
  );
}

export default function WeekNavigator({ week, onPrev, onNext, onToday }) {
  const isCurrent = week.from === currentWeek().from;
  const range = formatWeekRange(week);
  return (
    <View style={{ flexDirection: "row", alignItems: "center", flexWrap: "wrap", gap: 8, marginBottom: 12 }}>
      <NavButton icon="chevron-back" label="Settimana precedente" onPress={onPrev} />
      <NavButton text="Oggi" label="Torna alla settimana corrente" onPress={onToday} />
      <NavButton icon="chevron-forward" label="Settimana successiva" onPress={onNext} />
      <Text accessibilityRole="header" accessibilityLiveRegion="polite" style={{ color: C.text, fontWeight: "700", fontSize: 14, flexShrink: 1 }}>
        {range}{isCurrent ? " · questa settimana" : ""}
      </Text>
    </View>
  );
}
