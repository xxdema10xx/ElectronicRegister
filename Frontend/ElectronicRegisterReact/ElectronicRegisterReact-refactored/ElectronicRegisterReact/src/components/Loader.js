// src/components/Loader.js
import { ActivityIndicator, View } from "react-native";
import { C, s } from "../constants/theme";

export default function Loader() {
  return (
    <View style={s.loaderWrap}>
      <ActivityIndicator size="large" color={C.primary} />
    </View>
  );
}
