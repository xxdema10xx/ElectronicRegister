// src/components/Badge.js
import { Text, View } from "react-native";
import { C, ROLE_COLOR, ROLE_TEXT, s } from "../constants/theme";

export default function Badge({ text, role }) {
  return (
    <View style={[s.badge, { backgroundColor: ROLE_COLOR[role] || C.border }]}>
      <Text style={[s.badgeText, { color: ROLE_TEXT[role] || C.text }]}>{text}</Text>
    </View>
  );
}
