// src/components/AppDialog.js
// Dialog dell'app (avvisi e conferme) al posto di window.alert / window.confirm e degli Alert di sistema.
//   showDialog({ title, message, buttons: [{ text, style: "primary" | "destructive" | "cancel", onPress, escape? }] })
// <DialogHost /> va montato una volta sola alla radice (index.js): mostra un dialog alla volta, in coda,
// sopra qualsiasi altra schermata o Modal.
// Tastiera (web): Esc annulla (pulsante "cancel", oppure chiude se è solo un avviso); Invio sceglie
// l'azione primaria, mai quella distruttiva. Su Android il tasto indietro annulla (onRequestClose).
import { useEffect, useState } from "react";
import { Modal, Platform, Text, View } from "react-native";
import { C, s } from "../constants/theme";
import Btn from "./Btn";

const queue = [];
let listener = null;

export function showDialog(dialog) {
  queue.push({ ...dialog, buttons: dialog.buttons?.length ? dialog.buttons : [{ text: "OK", style: "primary" }] });
  listener?.();
}

export function DialogHost() {
  const [current, setCurrent] = useState(null);

  useEffect(() => {
    listener = () => setCurrent(c => c || queue.shift() || null);
    listener();
    return () => { listener = null; };
  }, []);

  const close = (button) => {
    setCurrent(queue.shift() || null);
    button?.onPress?.();
  };
  const cancelButton = current?.buttons.find(b => b.style === "cancel" || b.escape); // escape: tasto che vale come "annulla"
  const primaryButton = current?.buttons.find(b => b.style === "primary");
  const onlyNotice = current && current.buttons.length === 1 && current.buttons[0].style !== "destructive";
  // Esc / tasto indietro: annulla; un semplice avviso si chiude come con OK
  const cancel = () => close(cancelButton || (onlyNotice ? current.buttons[0] : null));

  useEffect(() => {
    if (!current || Platform.OS !== "web") return;
    const onKey = (e) => {
      if (e.key === "Enter" && primaryButton) { e.preventDefault(); e.stopPropagation(); close(primaryButton); }
    };
    document.addEventListener("keydown", onKey, true);
    return () => document.removeEventListener("keydown", onKey, true);
  }, [current]);

  if (!current) return null;
  return (
    // onRequestClose: Esc su web (react-native-web), tasto indietro su Android
    <Modal visible transparent animationType="fade" onRequestClose={cancel}>
      <View style={{ flex: 1, backgroundColor: "rgba(0,0,0,0.45)", alignItems: "center", justifyContent: "center", padding: 16 }}>
        <View accessibilityRole="alert" style={[s.card, { width: "100%", maxWidth: 440, padding: 22 }]}>
          <Text style={{ color: C.text, fontSize: 18, fontWeight: "800" }}>{current.title}</Text>
          {!!current.message && <Text style={{ color: C.textMuted, fontSize: 15, marginTop: 8, lineHeight: 21 }}>{current.message}</Text>}
          <View style={{ flexDirection: "row", flexWrap: "wrap", justifyContent: "flex-end", gap: 8, marginTop: 20 }}>
            {current.buttons.map((b, i) => (
              <Btn key={`${b.text}-${i}`} label={b.text} onPress={() => close(b)}
                variant={b.style === "destructive" ? "danger" : b.style === "cancel" ? "ghost" : "primary"}
                style={[s.smBtn, { paddingHorizontal: 16, paddingVertical: 10 }]} textStyle={s.smBtnText} />
            ))}
          </View>
        </View>
      </View>
    </Modal>
  );
}
