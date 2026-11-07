// src/screens/ExamEditorScreen.js
// Schermata dell'editor esami per le rotte /esami/nuovo e /esami/{id}/modifica. Avvolge TestBuilder
// passandogli le stesse props di prima e aggiunge ciò che serve a una schermata con il suo URL:
//  - ricarica su /esami/{id}/modifica: riusa la bozza locale di quell'esame, altrimenti la carica;
//    id inesistente → messaggio e lista; esame non più in bozza → dettaglio;
//  - step nell'URL (?step=2), letto all'apertura e aggiornato quando si cambia step;
//  - uscita con modifiche non salvate sul server: conferma (freccia, Esc, tasto indietro di Android,
//    indietro/avanti del browser) e avviso del browser alla chiusura o ricarica del tab.
// La bozza e il salvataggio restano in ExamsScreen, come prima.
import { useEffect, useRef, useState } from "react";
import { Modal, Platform, View } from "react-native";
import { C } from "../constants/theme";
import Loader from "../components/Loader";
import TestBuilder, { validateDetails } from "../components/TestBuilder";
import { notify, confirmAction } from "../utils/dialogs";
import { EXAM_STATUS, detailToDraft, fromEnum } from "../api/examContract";
import { useBeforeUnload } from "../navigation/examRoutes";

const LEAVE_TITLE = "Modifiche non salvate";
const LEAVE_MESSAGE = Platform.OS === "web"
  ? "Le modifiche non sono ancora sul server. Uscire? La bozza resta in “Riprendi bozza” finché questa scheda è aperta."
  : "Le modifiche non sono ancora sul server. Uscire? La bozza resta in “Riprendi bozza” finché non cambi sezione o chiudi l'app.";

// Uguali a parte lo step? (cambiare step non è una modifica da salvare)
const sameButStep = (a, b) => !!a && !!b && JSON.stringify({ ...a, step: 0 }) === JSON.stringify({ ...b, step: 0 });

export default function ExamEditorScreen({ route, nav, api, draft, dirty, updateDraft, env, contextLabel, draftStatus, onSave, saving, onDiscard, onExit }) {
  const [loadingExam, setLoadingExam] = useState(false);
  const ready = route.name === "edit" ? draft?.examId === route.id : !!draft && !draft.examId;

  // /esami/{id}/modifica: bozza locale di quell'esame, oppure caricamento dal server
  useEffect(() => {
    if (route.name !== "edit" || draft?.examId === route.id) return;
    if (draft && dirty) {
      notify("Bozza in corso", draft.examId
        ? "Hai modifiche non salvate su un altro esame: salvale o annullale prima di modificarne un altro."
        : "Hai un nuovo esame non salvato: salvalo o eliminalo prima di modificarne un altro.");
      nav.replace(draft.examId ? { name: "edit", id: draft.examId } : { name: "new" });
      return;
    }
    let cancelled = false;
    setLoadingExam(true);
    // TODO(API): in modalità prova risponde il mock; con il server vero è GET /institution/exams/{id}
    api.getExam(route.id)
      .then((detail) => {
        if (cancelled) return;
        if (fromEnum(detail?.status, EXAM_STATUS) !== "draft") {
          notify("Esame non modificabile", "È già pubblicato: si può solo consultare.");
          nav.replace({ name: "view", id: route.id });
          return;
        }
        updateDraft({ ...detailToDraft(detail), step: 1 }, { fromServer: true });
      })
      .catch((e) => {
        if (cancelled) return;
        notify("Esame non trovato", e.status === 404 || /404/.test(e.message) ? "L'esame non esiste o è stato eliminato." : e.message);
        nav.replace({ name: "list", tab: "drafts" });
      })
      .finally(() => { if (!cancelled) setLoadingExam(false); });
    return () => { cancelled = true; };
  }, [route.name, route.id]);

  // Step: dall'URL alla bozza una volta all'apertura, poi dalla bozza all'URL.
  // Senza dettagli validi non si va oltre lo step 1 (come con lo Stepper).
  const stepApplied = useRef(false);
  useEffect(() => {
    if (!ready) return;
    if (!stepApplied.current) {
      stepApplied.current = true;
      const wanted = route.step && route.step > 1 && !validateDetails(draft) ? route.step : 1;
      if (wanted !== (draft.step ?? 1)) { updateDraft({ ...draft, step: wanted }, { keepDirty: true }); return; }
    }
    if ((route.step || 1) !== (draft.step ?? 1)) nav.replace({ ...route, step: draft.step ?? 1 });
  }, [ready, draft?.step]);

  // Cambiare step non rende la bozza "da salvare"
  const onChange = (next) => updateDraft(next, { keepDirty: sameButStep(next, draft) });

  // Uscita: conferma se ci sono modifiche non salvate sul server
  function requestExit() {
    if (dirty) confirmAction(LEAVE_TITLE, LEAVE_MESSAGE, onExit, "Esci");
    else onExit();
  }
  useEffect(() => {
    nav.setLeaveGuard(dirty ? () => window.confirm(`${LEAVE_TITLE}\n\n${LEAVE_MESSAGE}`) : null);
    return () => nav.setLeaveGuard(null);
  }, [dirty]);
  useBeforeUnload(dirty);

  if (!ready) {
    return loadingExam ? (
      <Modal visible animationType="fade" onRequestClose={onExit}>
        <View style={{ flex: 1, backgroundColor: C.bg }}><Loader /></View>
      </Modal>
    ) : null;
  }

  return (
    <TestBuilder visible contextLabel={contextLabel} draft={draft} env={env} draftStatus={draftStatus}
      onChange={onChange} onSave={onSave} saving={saving} onClose={requestExit} onDiscard={onDiscard} />
  );
}
