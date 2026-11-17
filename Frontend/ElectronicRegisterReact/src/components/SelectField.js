// src/components/SelectField.js
import { useState } from "react";
import { FlatList, Modal, Platform, Text, TouchableOpacity, View } from "react-native";
import { C, s } from "../constants/theme";
import EmptyState from "./EmptyState";

export default function SelectField({
  label,
  value,
  options = [],
  onSelect,
  placeholder = "Seleziona…",
  getLabel = (o) => o.label,
  getValue = (o) => o.id,
  emptyMessage = "Nessun elemento disponibile",
}) {
  const [open, setOpen] = useState(false);
  const selected = options.find(o => getValue(o) === value);

  // Sul web si usa il <select> nativo del browser (tastiera, ricerca per iniziale, ecc.)
  if (Platform.OS === "web") {
    return (
      <View style={{ marginBottom: 14 }}>
        {label && <Text style={s.label}>{label}</Text>}
        <View style={s.selectBox}>
          <select
            value={selected ? String(value) : ""}
            onChange={(e) => {
              const opt = options.find(o => String(getValue(o)) === e.target.value);
              if (opt) onSelect(getValue(opt));
            }}
            style={{ width: "100%", height: "100%", border: "none", outline: "none", background: "transparent", fontFamily: "inherit",
              fontSize: "15px", color: selected ? C.text : C.textLight, cursor: "pointer" }}
          >
            <option value="" disabled>{options.length === 0 ? emptyMessage : placeholder}</option>
            {options.map(o => <option key={String(getValue(o))} value={String(getValue(o))}>{getLabel(o)}</option>)}
          </select>
        </View>
      </View>
    );
  }

  return (
    <View style={{ marginBottom: 14 }}>
      {label && <Text style={s.label}>{label}</Text>}
      <TouchableOpacity style={s.selectBox} onPress={() => setOpen(true)} activeOpacity={0.7}>
        <Text style={{ fontSize: 15, color: selected ? C.text : C.textLight, flex: 1 }} numberOfLines={1}>
          {selected ? getLabel(selected) : placeholder}
        </Text>
        <Text style={{ color: C.textLight, fontSize: 12 }}>▾</Text>
      </TouchableOpacity>

      <Modal visible={open} animationType="slide" transparent onRequestClose={() => setOpen(false)}>
        <View style={s.modalOverlay}>
          <View style={[s.modalCard, { maxHeight: "75%" }]}>
            <View style={s.modalHeader}>
              <Text style={s.modalTitle}>{label || "Seleziona"}</Text>
              <TouchableOpacity onPress={() => setOpen(false)}><Text style={s.modalClose}>✕</Text></TouchableOpacity>
            </View>
            {options.length === 0 ? (
              <EmptyState message={emptyMessage} />
            ) : (
              <FlatList
                data={options}
                keyExtractor={(o) => String(getValue(o))}
                renderItem={({ item }) => {
                  const isSelected = getValue(item) === value;
                  return (
                    <TouchableOpacity
                      style={[s.selectOption, isSelected && s.selectOptionActive]}
                      onPress={() => { onSelect(getValue(item)); setOpen(false); }}
                    >
                      <Text style={[s.selectOptionText, isSelected && s.selectOptionTextActive]}>
                        {getLabel(item)}
                      </Text>
                      {isSelected && <Text style={{ color: C.primary, fontWeight: "700" }}>✓</Text>}
                    </TouchableOpacity>
                  );
                }}
              />
            )}
          </View>
        </View>
      </Modal>
    </View>
  );
}
