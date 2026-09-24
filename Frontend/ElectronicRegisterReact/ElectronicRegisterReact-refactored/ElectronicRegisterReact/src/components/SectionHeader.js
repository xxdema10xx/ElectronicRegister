// src/components/SectionHeader.js
import { Text, View } from "react-native";
import { s } from "../constants/theme";

export default function SectionHeader({ title, action }) {
  return (
    <View style={s.sectionHeader}>
      <Text style={s.sectionTitle}>{title}</Text>
      {action}
    </View>
  );
}
