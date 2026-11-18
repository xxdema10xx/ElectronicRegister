// src/hooks/useIntentId.js
// eventId di un comando del calendario: generato all'apertura del modulo, riusato nei retry dello stesso
// payload (il server riconosce il doppio invio), nuovo appena l'utente cambia i dati o dopo un successo.
// Stesso eventId con un payload diverso darebbe 409 dal server.
import { useCallback, useRef } from "react";
import { newEventId } from "../api/calendarImportService";

export function useIntentId() {
  const ref = useRef(null);
  if (ref.current === null) ref.current = { key: undefined, id: newEventId() };
  // payload: i dati del comando senza eventId. Restituisce l'eventId da usare per questo invio.
  const idFor = useCallback((payload) => {
    const key = JSON.stringify(payload);
    if (ref.current.key === undefined) ref.current.key = key;
    else if (ref.current.key !== key) ref.current = { key, id: newEventId() };
    return ref.current.id;
  }, []);
  const reset = useCallback(() => { ref.current = { key: undefined, id: newEventId() }; }, []);
  return { idFor, reset };
}
