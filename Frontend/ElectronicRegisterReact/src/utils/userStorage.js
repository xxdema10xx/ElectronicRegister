// src/utils/userStorage.js
// Dati dell'app nel browser, sempre separati per utente E per istituto: su un PC condiviso nessuno deve vedere
// quelli di un altro, e chi lavora in due istituti non deve ritrovare in uno le bozze dell'altro.
// Chiave: er:u:<userId>:<institutionId>:<nome>. Utente e istituto li imposta App.js dopo la selezione
// dell'istituto; senza entrambi non si legge e non si scrive nulla.
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
let institutionId = null;
export function setStorageUser(id, institution) {
  userId = id || null;
  institutionId = institution || null;
}
const active = () => !!userId && !!institutionId;

const webStore = (kind) => (kind === "session" ? window.sessionStorage : window.localStorage);
const keyOf = (name) => `${PREFIX}${userId}:${institutionId}:${name}`;

export function readUser(kind, name, fallback) {
  if (!active()) return fallback;
  try {
    if (!isWeb) return kind === "local" && memory.has(keyOf(name)) ? memory.get(keyOf(name)) : fallback;
    return JSON.parse(webStore(kind).getItem(keyOf(name))) ?? fallback;
  } catch { return fallback; }
}

export function writeUser(kind, name, value) {
  if (!active()) return;
  try {
    if (isWeb) webStore(kind).setItem(keyOf(name), JSON.stringify(value));
    else if (kind === "local") memory.set(keyOf(name), value);
  } catch { /* archivio pieno o non disponibile: si continua senza salvare */ }
}

export function removeUser(kind, name) {
  if (!active()) return;
  try {
    if (isWeb) webStore(kind).removeItem(keyOf(name));
    else if (kind === "local") memory.delete(keyOf(name));
  } catch { /* niente da togliere */ }
}

// Al logout: via tutto ciò che appartiene all'utente corrente, in tutti i suoi istituti
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

// Chiavi del formato precedente, per utente ma senza istituto (er:u:<userId>:<nome>): non si può sapere a
// quale istituto appartenessero, quindi si cancellano invece di lasciarle leggibili come bozze "orfane".
// Il formato attuale ha sempre quattro segmenti dopo "er" (u, utente, istituto, nome).
const isUserScopedWithoutInstitution = (key) => key.startsWith(PREFIX) && key.split(":").length === 4;

// All'avvio: cancella i dati del mock, quelli senza proprietario e quelli senza istituto. Dopo il primo
// avvio con questa versione non ne restano: le passate successive non trovano più nulla da togliere.
export function purgeLegacyStorage() {
  if (!isWeb) return;
  for (const store of [window.localStorage, window.sessionStorage]) {
    try {
      LEGACY_KEYS.forEach(key => store.removeItem(key));
      for (let i = store.length - 1; i >= 0; i--) {
        const key = store.key(i);
        if (key && isUserScopedWithoutInstitution(key)) store.removeItem(key);
      }
    } catch { /* archivio non disponibile */ }
  }
}
