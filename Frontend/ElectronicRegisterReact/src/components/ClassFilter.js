// src/components/ClassFilter.js
// Filtro per classe compatto, per le barre dei filtri: "Classe: Tutte" a riposo; con una classe scelta
// diventa "attivo" (bordo e sfondo leggero nel colore primario) e mostra una ✕ per tornare a "Tutte".
// La scelta si fa nella stessa finestra di SelectField (stessi stili del tema). Su web si usa anche da
// tastiera: Tab per arrivarci, Invio o Spazio per aprire, Esc per chiudere (onRequestClose del Modal).
import { useState } from "react";
import { FlatList, Modal, Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";

const ACTIVE_BG = "#E0EEF8"; // stesso azzurro della domanda selezionata nell'editor

// options: [{ id, name }]; value: id della classe scelta oppure null (= tutte)
export default function ClassFilter({ value, options = [], onChange, style, height = 40 }) {
  const [open, setOpen] = useState(false);
  const selected = options.find(o => o.id === value) || null;
  const active = !!selected;
  const choose = (id) => { onChange(id); setOpen(false); };
  const items = [{ id: null, name: "Tutte le classi" }, ...options];

  return (
    <View style={[{ flexDirection: "row", alignItems: "center", height, borderRadius: 10, borderWidth: 1,
      borderColor: active ? C.primary : C.border, backgroundColor: active ? ACTIVE_BG : C.bg }, style]}>
      <TouchableOpacity onPress={() => setOpen(true)} activeOpacity={0.7} accessibilityRole="button"
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

      <Modal visible={open} animationType="slide" transparent onRequestClose={() => setOpen(false)}>
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
              renderItem={({ item }) => {
                const isSelected = item.id === (selected?.id ?? null);
                return (
                  <TouchableOpacity style={[s.selectOption, isSelected && s.selectOptionActive]} onPress={() => choose(item.id)}
                    accessibilityRole="button" accessibilityState={{ selected: isSelected }}>
                    <Text style={[s.selectOptionText, isSelected && s.selectOptionTextActive]}>{item.name}</Text>
                    {isSelected && <Text style={{ color: C.primary, fontWeight: "700" }}>✓</Text>}
                  </TouchableOpacity>
                );
              }}
            />
          </View>
        </View>
      </Modal>
    </View>
  );
}
