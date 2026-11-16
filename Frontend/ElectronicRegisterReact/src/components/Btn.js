// src/components/Btn.js
// Il BOTTONE riusabile di tutta l'app. Invece di ridisegnare un bottone in ogni schermata,
// si scrive <Btn label="Salva" onPress={...} /> e questo file pensa a colori e comportamento.

// ActivityIndicator = rotellina di caricamento; Text = testo; TouchableOpacity = elemento "cliccabile". (vedi GUIDA §4.4)
import { ActivityIndicator, Text, TouchableOpacity } from "react-native";
// C = tavolozza dei colori; s = raccolta di stili già pronti (definiti in constants/theme.js).
import { C, s } from "../constants/theme";

// Componente Btn. Le graffe nei parametri estraggono le "props" (vedi GUIDA §4.3 e destructuring §4.1):
//  - label: testo scritto sul bottone
//  - onPress: funzione da eseguire quando si tocca/clicca
//  - style / textStyle: stili extra opzionali per bottone e testo
//  - icon: simbolo opzionale davanti al testo
//  - variant: tipo di bottone; se non specificato vale "primary" (valore predefinito)
//  - loading: se vero mostra la rotellina e blocca il bottone
//  - disabled: se vero il bottone è spento
export default function Btn({ label, onPress, style, textStyle, icon, variant = "primary", loading, disabled }) {
  // Sceglie il colore di sfondo in base al tipo di bottone:
  // si crea una "tabella" nome→colore e poi si prende la voce che corrisponde a variant.
  const bg = {
    primary: C.primary, danger: C.danger, ghost: "transparent",
    success: C.success, secondary: C.secondary,
  }[variant];
  // Colore del testo: i bottoni "ghost" (trasparenti) hanno testo colorato, gli altri testo bianco.
  const tc = variant === "ghost" ? C.primary : C.white;

  return (
    <TouchableOpacity
      // Lo stile è una lista: prima lo stile base s.btn, poi quello calcolato qui, poi quello passato da fuori (che vince).
      // - ghost: ha un bordo di 1 pixel, gli altri nessun bordo.
      // - opacity 0.6 (più sbiadito) quando il bottone è disabilitato o sta caricando.
      style={[s.btn, { backgroundColor: bg, borderWidth: variant === "ghost" ? 1 : 0, borderColor: C.primary, opacity: disabled || loading ? 0.6 : 1 }, style]}
      // Cosa fare al tocco: eseguire la funzione ricevuta.
      onPress={onPress}
      // Durante il caricamento o se disabilitato, il tocco viene ignorato (evita doppi invii).
      disabled={disabled || loading}
    >
      {/* Operatore "? :" (vedi GUIDA §4.1): se sta caricando mostra la rotellina, altrimenti il testo. */}
      {loading ? <ActivityIndicator color={C.white} size="small" /> :
        // Il testo: se c'è un'icona la mette davanti seguita da due spazi, poi l'etichetta.
        <Text style={[s.btnText, { color: tc }, textStyle]}>{icon ? `${icon}  ` : ""}{label}</Text>}
    </TouchableOpacity>
  );
}
