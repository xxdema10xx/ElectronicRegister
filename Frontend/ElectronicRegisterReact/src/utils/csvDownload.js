// src/utils/csvDownload.js
// File CSV per l'import del calendario: modello vuoto ed elenco dei docenti con il loro ID.
// UTF-8 con BOM (Excel lo apre con gli accenti giusti), separatore ";", righe CRLF.
// Il download è solo web (Blob + link); sull'app i pulsanti non si mostrano.
import { Platform } from "react-native";

export const CALENDAR_TEMPLATE_HEADERS = ["Data", "Ora inizio", "Ora fine", "Classe", "Modulo", "Docente", "Aula", "Descrizione"];
const BOM = "﻿";

// Un valore con ; " o a capo va tra virgolette, con le virgolette raddoppiate
export function csvCell(value) {
  const v = value === null || value === undefined ? "" : String(value);
  return /[;"\r\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v;
}

export const csvText = (rows) => BOM + rows.map(r => r.map(csvCell).join(";")).join("\r\n") + "\r\n";

// Solo le intestazioni: la riga d'esempio è nel testo della pagina, non nel file
export const calendarTemplateCsv = () => csvText([CALENDAR_TEMPLATE_HEADERS]);

export function teachersCsv(teachers) {
  const rows = (teachers || [])
    .map(t => [t.lastName || "", t.firstName || "", t.id])
    .sort((a, b) => `${a[0]} ${a[1]}`.localeCompare(`${b[0]} ${b[1]}`));
  return csvText([["Cognome", "Nome", "ID"], ...rows]);
}

export const canDownload = () => Platform.OS === "web" && typeof document !== "undefined" && typeof Blob !== "undefined" && !!globalThis.URL?.createObjectURL;

export function downloadCsv(filename, text) {
  if (!canDownload()) return false;
  const url = URL.createObjectURL(new Blob([text], { type: "text/csv;charset=utf-8" }));
  const a = document.createElement("a");
  a.href = url; a.download = filename;
  document.body.appendChild(a); a.click(); a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 0);
  return true;
}
