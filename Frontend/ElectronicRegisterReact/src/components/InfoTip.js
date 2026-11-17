// src/components/InfoTip.js
// Icona "i" con un testo di aiuto. Web: compare passando col mouse o con il focus (Tab), si chiude con Esc.
// Touch: si apre toccando l'icona e si chiude toccando fuori. Il testo sta fuori dall'albero della pagina
// (portale su web, Modal su nativo), quindi liste e ScrollView non lo tagliano.
import { useEffect, useRef, useState } from "react";
import { Dimensions, Modal, Platform, Pressable, Text, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C } from "../constants/theme";
import { tipPosition, tipWidth } from "../utils/tipPosition";

const isWeb = Platform.OS === "web";
const createPortal = isWeb ? require("react-dom").createPortal : null;
const TIP_WIDTH = 300;

export default function InfoTip({ text, size = 18 }) {
  const anchor = useRef(null);
  const hovered = useRef(false);
  const focused = useRef(false);
  const [rect, setRect] = useState(null); // posizione dell'icona: non null = tooltip aperto
  const [height, setHeight] = useState(0);

  const show = () => anchor.current?.measureInWindow((x, y, w, h) => setRect({ x, y, width: w, height: h }));
  const hide = () => setRect(null);
  // Il mouse che esce non chiude se l'icona ha ancora il focus, e viceversa
  const release = () => { if (!hovered.current && !focused.current) hide(); };

  const open = !!rect;
  useEffect(() => {
    if (!open || !isWeb) return;
    const onKey = (e) => { if (e.key === "Escape") hide(); };
    const onPointer = (e) => { if (!anchor.current?.contains(e.target)) hide(); };
    document.addEventListener("keydown", onKey);
    document.addEventListener("pointerdown", onPointer, true);
    // Il tooltip è fissato allo schermo: se la pagina si sposta non sarebbe più accanto all'icona
    window.addEventListener("scroll", hide, true);
    window.addEventListener("resize", hide);
    return () => {
      document.removeEventListener("keydown", onKey);
      document.removeEventListener("pointerdown", onPointer, true);
      window.removeEventListener("scroll", hide, true);
      window.removeEventListener("resize", hide);
    };
  }, [open]);

  const icon = (
    <View ref={anchor} collapsable={false}>
      <Pressable onPress={show} hitSlop={8} accessibilityRole="button" accessibilityLabel={text}
        onHoverIn={() => { hovered.current = true; show(); }}
        onHoverOut={() => { hovered.current = false; release(); }}
        onFocus={() => { focused.current = true; show(); }}
        onBlur={() => { focused.current = false; release(); }}
        style={(state) => ({ padding: 2, borderRadius: 999, backgroundColor: state.focused ? "#E0EEF8" : "transparent" })}>
        <Ionicons name="information-circle-outline" size={size} color={C.secondary} />
      </Pressable>
    </View>
  );
  if (!open) return icon;

  const screen = Dimensions.get("window");
  const width = tipWidth(TIP_WIDTH, screen.width);
  const pos = height ? tipPosition({ anchor: rect, width, height, screenWidth: screen.width, screenHeight: screen.height }) : null;
  // Invisibile finché non se ne conosce l'altezza, che decide se sta sotto o sopra l'icona
  const tip = (
    <View onLayout={(e) => setHeight(e.nativeEvent.layout.height)}
      style={{ position: isWeb ? "fixed" : "absolute", zIndex: 10000, width, left: pos?.left ?? 0, top: pos?.top ?? 0, opacity: pos ? 1 : 0, pointerEvents: "none",
        backgroundColor: C.text, borderRadius: 10, padding: 12, shadowColor: "#000", shadowOpacity: 0.2, shadowRadius: 12, shadowOffset: { width: 0, height: 4 }, elevation: 6 }}>
      <Text style={{ color: C.white, fontSize: 13, lineHeight: 18 }}>{text}</Text>
    </View>
  );

  return (
    <>
      {icon}
      {isWeb ? createPortal(tip, document.body) : (
        <Modal transparent visible onRequestClose={hide}>
          <Pressable style={{ flex: 1 }} onPress={hide} accessibilityLabel="Chiudi">{tip}</Pressable>
        </Modal>
      )}
    </>
  );
}
