// src/components/ListRow.js
// Riga di un elenco (studenti, materie, classi, voti…).
// Su web le righe stanno in un unico pannello bordato, separate da una linea, con evidenziazione al
// passaggio del mouse: l'aspetto di una tabella di una pagina web. Su nativo restano card separate.
// Uso: <FlatList {...listProps()} renderItem={() => <ListRow onPress={…}>…</ListRow>} />
import { Platform, Pressable, TouchableOpacity, View } from "react-native";
import { C } from "../constants/theme";
import Card from "./Card";

const isWeb = Platform.OS === "web";
const ROW = { flexDirection: "row", alignItems: "center" };
export const HOVER_BG = "#F1F5F9";

const NATIVE_CONTENT = { paddingHorizontal: 16, paddingBottom: 32 };
const WEB_CONTENT = {
  marginHorizontal: 16, marginBottom: 32, backgroundColor: C.card,
  borderWidth: 1, borderColor: C.border, borderRadius: 12, overflow: "hidden",
};
const Separator = () => <View style={{ height: 1, backgroundColor: C.border }} />;

// Props per la FlatList che contiene le righe; `extra` si aggiunge allo stile del contenuto
export function listProps(extra) {
  return isWeb
    ? { contentContainerStyle: [WEB_CONTENT, extra], ItemSeparatorComponent: Separator }
    : { contentContainerStyle: [NATIVE_CONTENT, extra] };
}

export default function ListRow({ children, onPress, style }) {
  if (!isWeb) {
    const card = <Card style={[{ marginBottom: 10 }, ROW, style]}>{children}</Card>;
    return onPress ? <TouchableOpacity activeOpacity={0.7} onPress={onPress}>{card}</TouchableOpacity> : card;
  }
  const base = [ROW, { paddingVertical: 12, paddingHorizontal: 16, backgroundColor: C.card }];
  if (!onPress) return <View style={[base, style]}>{children}</View>;
  return (
    <Pressable onPress={onPress} style={({ hovered }) => [base, hovered && { backgroundColor: HOVER_BG }, style]}>
      {children}
    </Pressable>
  );
}
