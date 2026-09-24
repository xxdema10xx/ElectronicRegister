// src/components/Card.js
import { View } from "react-native";
import { s } from "../constants/theme";

export default function Card({ children, style }) {
  return <View style={[s.card, style]}>{children}</View>;
}
