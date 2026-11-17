// src/components/DateField.js
import { useState } from "react";
import { Modal, Platform, Text, TouchableOpacity, View } from "react-native";
import DateTimePicker from "@react-native-community/datetimepicker";
import { C, s } from "../constants/theme";
import { formatDateForDisplay, parseISODate, toISODate } from "../utils/dateUtils";
import Btn from "./Btn";

export default function DateField({ label, value, onChange, placeholder = "Seleziona data…" }) {
  const [show, setShow] = useState(false);
  const [tempDate, setTempDate] = useState(parseISODate(value));

  function openPicker() {
    setTempDate(parseISODate(value));
    setShow(true);
  }

  function handleAndroidChange(event, selectedDate) {
    setShow(false);
    if (event.type === "set" && selectedDate) {
      onChange(toISODate(selectedDate));
    }
  }

  function confirmIOS() {
    onChange(toISODate(tempDate));
    setShow(false);
  }

  return (
    <View style={{ marginBottom: 14 }}>
      {label && <Text style={s.label}>{label}</Text>}

      {Platform.OS === "web" ? (
        <View style={s.selectBox}>
          <input
            type="date"
            value={value || ""}
            onChange={(e) => onChange(e.target.value)}
            style={{
              width: "100%",
              height: "100%",
              border: "none",
              outline: "none",
              background: "transparent",
              fontFamily: "inherit",
              fontSize: "15px",
              color: value ? C.text : C.textLight,
              cursor: "pointer",
            }}
          />
        </View>
      ) : (
        <TouchableOpacity style={s.selectBox} onPress={openPicker} activeOpacity={0.7}>
          <Text style={{ fontSize: 15, color: value ? C.text : C.textLight, flex: 1 }}>
            {value ? formatDateForDisplay(value) : placeholder}
          </Text>
          <Text style={{ color: C.textLight, fontSize: 16 }}>📅</Text>
        </TouchableOpacity>
      )}

      {show && Platform.OS === "android" && (
        <DateTimePicker value={tempDate} mode="date" display="calendar" onChange={handleAndroidChange} />
      )}

      {Platform.OS === "ios" && (
        <Modal visible={show} animationType="slide" transparent onRequestClose={() => setShow(false)}>
          <View style={s.modalOverlay}>
            <View style={s.modalCard}>
              <View style={s.modalHeader}>
                <Text style={s.modalTitle}>{label || "Seleziona data"}</Text>
                <TouchableOpacity onPress={() => setShow(false)}>
                  <Text style={s.modalClose}>✕</Text>
                </TouchableOpacity>
              </View>
              <DateTimePicker
                value={tempDate}
                mode="date"
                display="inline"
                onChange={(event, selectedDate) => selectedDate && setTempDate(selectedDate)}
                locale="it-IT"
              />
              <Btn label="Conferma" onPress={confirmIOS} style={{ marginTop: 12 }} />
            </View>
          </View>
        </Modal>
      )}
    </View>
  );
}