// src/components/NotificationDot.js
import { View } from "react-native";
import { C } from "../constants/theme";

// Pallino rosso posizionato in alto a destra del contenitore (che deve avere position: "relative").
export default function NotificationDot({ visible, style }) {
  if (!visible) return null;
  return (
    <View pointerEvents="none" style={[{
      position: "absolute", top: 0, right: 0, width: 14, height: 14, borderRadius: 7,
      backgroundColor: C.danger, borderWidth: 2, borderColor: C.white,
    }, style]} />
  );
}
