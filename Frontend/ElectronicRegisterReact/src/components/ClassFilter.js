// src/components/ClassFilter.js
// Filtro per classe compatto, per le barre dei filtri: "Classe: Tutte" a riposo; con una classe scelta
// diventa "attivo" (bordo e sfondo leggero nel colore primario) e mostra una ✕ per tornare a "Tutte".
// Scelta: su web con finestra larga un menu sotto il campo, largo quanto il campo (si chiude con Esc o
// cliccando fuori); su mobile e finestre strette la stessa finestra dal basso di SelectField (stessi stili
// del tema). Da tastiera: Tab per arrivarci, Invio o Spazio per aprire, Tab tra le voci, Esc per chiudere.
import { useEffect, useRef, useState } from "react";
import { FlatList, Modal, Platform, ScrollView, Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import { useLayout } from "../hooks/useLayout";

const ACTIVE_BG = "#E0EEF8"; // stesso azzurro della domanda selezionata nell'editor

// options: [{ id, name }]; value: id della classe scelta oppure null (= tutte)
export default function ClassFilter({ value, options = [], onChange, style, height = 40 }) {
  const [open, setOpen] = useState(false);
  const selected = options.find(o => o.id === value) || null;
  const active = !!selected;
  const choose = (id) => { onChange(id); setOpen(false); };
  const items = [{ id: null, name: "Tutte le classi" }, ...options];
  const { isDesktop } = useLayout();
  const dropdown = Platform.OS === "web" && isDesktop;

  // Menu a tendina (web largo): chiusura con Esc o con un clic fuori dal filtro
  const boxRef = useRef(null);
  useEffect(() => {
    if (!open || !dropdown) return;
    const onKey = (e) => { if (e.key === "Escape") setOpen(false); };
    const onDown = (e) => { if (boxRef.current && !boxRef.current.contains(e.target)) setOpen(false); };
    document.addEventListener("keydown", onKey);
    document.addEventListener("mousedown", onDown);
    return () => { document.removeEventListener("keydown", onKey); document.removeEventListener("mousedown", onDown); };
  }, [open, dropdown]);

  const option = (item) => {
    const isSelected = item.id === (selected?.id ?? null);
    return (
      <TouchableOpacity key={String(item.id)} style={[s.selectOption, isSelected && s.selectOptionActive, dropdown && { paddingHorizontal: 12, paddingVertical: 10 }]}
        onPress={() => choose(item.id)} accessibilityRole="button" accessibilityState={{ selected: isSelected }}>
        <Text style={[s.selectOptionText, isSelected && s.selectOptionTextActive]}>{item.name}</Text>
        {isSelected && <Text style={{ color: C.primary, fontWeight: "700" }}>✓</Text>}
      </TouchableOpacity>
    );
  };

  return (
    <View ref={boxRef} style={[{ flexDirection: "row", alignItems: "center", height, borderRadius: 10, borderWidth: 1,
      borderColor: active ? C.primary : C.border, backgroundColor: active ? ACTIVE_BG : C.bg, zIndex: open ? 50 : 0 }, style]}>
      <TouchableOpacity onPress={() => setOpen(o => (dropdown ? !o : true))} activeOpacity={0.7} accessibilityRole="button" accessibilityState={{ expanded: open }}
        accessibilityLabel={`Filtro classe: ${selected ? selected.name : "tutte"}. Apri per cambiare`}
        style={{ flex: 1, flexDirection: "row", alignItems: "center", gap: 8, paddingHorizontal: 12, height: "100%" }}>
        <Ionicons name="school-outline" size={18} color={active ? C.primary : C.textMuted} />
        <Text style={{ flex: 1, fontSize: 14, color: active ? C.primary : C.text, fontWeight: active ? "700" : "500" }} numberOfLines={1}>
          Classe: {selected ? selected.name : "Tutte"}
        </Text>
        {!active && <Ionicons name="chevron-down" size={16} color={C.textLight} />}
      </TouchableOpacity>
      {active && (
        <TouchableOpacity onPress={() => onChange(null)} accessibilityRole="button" accessibilityLabel="Togli il filtro classe"
          style={{ paddingHorizontal: 10, height: "100%", justifyContent: "center" }}>
          <Ionicons name="close" size={18} color={C.primary} />
        </TouchableOpacity>
      )}

      {open && dropdown && (
        <View accessibilityRole="menu" style={{ position: "absolute", top: height + 4, left: -1, right: -1, zIndex: 50, backgroundColor: C.white,
          borderWidth: 1, borderColor: C.border, borderRadius: 10, paddingVertical: 4, maxHeight: 320, overflow: "hidden",
          shadowColor: "#000", shadowOpacity: 0.12, shadowRadius: 12, shadowOffset: { width: 0, height: 4 }, elevation: 6 }}>
          <ScrollView>{items.map(option)}</ScrollView>
        </View>
      )}

      <Modal visible={open && !dropdown} animationType="slide" transparent onRequestClose={() => setOpen(false)}>
        <View style={s.modalOverlay}>
          <View style={[s.modalCard, { maxHeight: "75%" }]}>
            <View style={s.modalHeader}>
              <Text style={s.modalTitle}>Classe</Text>
              <TouchableOpacity onPress={() => setOpen(false)} accessibilityRole="button" accessibilityLabel="Chiudi">
                <Text style={s.modalClose}>✕</Text>
              </TouchableOpacity>
            </View>
            <FlatList
              data={items}
              keyExtractor={(o) => String(o.id)}
              renderItem={({ item }) => option(item)}
            />
          </View>
        </View>
      </Modal>
    </View>
  );
}
