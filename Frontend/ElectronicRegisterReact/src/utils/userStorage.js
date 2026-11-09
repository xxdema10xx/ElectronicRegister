// src/utils/userStorage.js
// Dati dell'app nel browser, sempre separati per utente: su un PC condiviso nessuno deve vedere quelli di un
// altro. L'utente corrente lo imposta App.js. Senza utente collegato non si legge e non si scrive nulla.
// kind: "session" (sessionStorage) o "local" (localStorage). Sull'app non esiste un archivio persistente:
// "local" vive in memoria finché l'app è aperta, "session" non conserva nulla.
import { Platform } from "react-native";

const isWeb = Platform.OS === "web";
const PREFIX = "er:u:";
const memory = new Map();

// Chiavi senza proprietario lasciate dalle versioni precedenti: non si può sapere di chi siano
const LEGACY_KEYS = ["mockExamState3", "lessonsCalendar2", "studentExamSeen2", "studentExamRuns2", "examDraft4"];

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export const isGuid = (v) => typeof v === "string" && GUID.test(v);

let userId = null;
export function setStorageUser(id) { userId = id || null; }

const webStore = (kind) => (kind === "session" ? window.sessionStorage : window.localStorage);
const keyOf = (name) => `${PREFIX}${userId}:${name}`;

export function readUser(kind, name, fallback) {
  if (!userId) return fallback;
  try {
    if (!isWeb) return kind === "local" && memory.has(keyOf(name)) ? memory.get(keyOf(name)) : fallback;
    return JSON.parse(webStore(kind).getItem(keyOf(name))) ?? fallback;
  } catch { return fallback; }
}

export function writeUser(kind, name, value) {
  if (!userId) return;
  try {
    if (isWeb) webStore(kind).setItem(keyOf(name), JSON.stringify(value));
    else if (kind === "local") memory.set(keyOf(name), value);
  } catch { /* archivio pieno o non disponibile: si continua senza salvare */ }
}

export function removeUser(kind, name) {
  if (!userId) return;
  try {
    if (isWeb) webStore(kind).removeItem(keyOf(name));
    else if (kind === "local") memory.delete(keyOf(name));
  } catch { /* niente da togliere */ }
}

// Al logout: via tutto ciò che appartiene all'utente corrente
export function clearUserStorage() {
  if (!userId) return;
  const prefix = `${PREFIX}${userId}:`;
  for (const key of [...memory.keys()]) if (key.startsWith(prefix)) memory.delete(key);
  if (!isWeb) return;
  for (const store of [window.localStorage, window.sessionStorage]) {
    try {
      for (let i = store.length - 1; i >= 0; i--) {
        const key = store.key(i);
        if (key?.startsWith(prefix)) store.removeItem(key);
      }
    } catch { /* archivio non disponibile */ }
  }
}

// All'avvio: cancella i dati del mock e quelli senza proprietario
export function purgeLegacyStorage() {
  if (!isWeb) return;
  for (const store of [window.localStorage, window.sessionStorage]) {
    try { LEGACY_KEYS.forEach(key => store.removeItem(key)); } catch { /* archivio non disponibile */ }
  }
}
