// src/components/RouteLink.js
// Elemento toccabile che su web è un link vero (<a href>): Ctrl/Cmd/Maiusc+clic, rotella e "Apri in nuova
// scheda" li gestisce il browser; il clic normale resta nella stessa scheda, senza ricaricare la pagina.
import { Platform, TouchableOpacity } from "react-native";

export default function RouteLink({ href, onPress, style, accessibilityLabel, children }) {
  const link = Platform.OS === "web" && !!href;
  const press = (e) => {
    const ne = e?.nativeEvent || e;
    if (link && (ne?.metaKey || ne?.ctrlKey || ne?.shiftKey)) return;
    e?.preventDefault?.();
    onPress?.();
  };
  return (
    <TouchableOpacity onPress={press} activeOpacity={0.7} style={style} accessibilityLabel={accessibilityLabel}
      accessibilityRole={link ? "link" : "button"} {...(link ? { href } : null)}>
      {children}
    </TouchableOpacity>
  );
}
