// src/components/EmptyState.js
import { Text, View } from "react-native";
import { s } from "../constants/theme";

export default function EmptyState({ message }) {
  return (
    <View style={s.emptyState}>
      <Text style={s.emptyIcon}>📭</Text>
      <Text style={s.emptyText}>{message}</Text>
    </View>
  );
}
