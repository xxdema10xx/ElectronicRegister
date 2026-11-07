// src/screens/ExamEditorScreen.js
// Pagina dell'editor esami per le rotte /esami/nuovo e /esami/{id}/modifica, nell'area dei contenuti
// (sidebar visibile). Avvolge TestBuilder passandogli le stesse props di prima e aggiunge ciò che serve a
// una pagina con il suo URL:
//  - ricarica su /esami/{id}/modifica: riusa la bozza locale di quell'esame, altrimenti la carica;
//    id inesistente → messaggio e lista; esame non più in bozza → dettaglio;
//  - step nell'URL (?step=2), letto all'apertura e aggiornato quando si cambia step;
//  - uscita con modifiche non salvate sul server: dialog dell'app ("Esci senza salvare" / "Continua a
//    modificare") per "← Esami", indietro/avanti del browser, sidebar, "Esci" e tasto indietro di Android
//    (BackHandler: non è più un Modal); avviso del browser alla chiusura o ricarica del tab.
// La bozza e il salvataggio restano in ExamsScreen, come prima.
import { useEffect, useRef, useState } from "react";
import { BackHandler, Platform } from "react-native";
import Loader from "../components/Loader";
import TestBuilder, { validateDetails } from "../components/TestBuilder";
import { notify, confirmLeave } from "../utils/dialogs";
import { EXAM_STATUS, detailToDraft, fromEnum } from "../api/examContract";
import { setLeaveGuard, setPageHeader, useBeforeUnload } from "../navigation/examRoutes";

// La frase su "Riprendi bozza" solo quando la bozza resta davvero:
//  - tornando alla lista ("← Esami", indietro del browser, tasto indietro di Android) resta sempre;
//  - cambiando sezione o con "Esci" resta su web (sessionStorage), sull'app no (la schermata si chiude).
const KEPT_MESSAGE = Platform.OS === "web"
  ? "Le modifiche non sono ancora sul server. Restano in “Riprendi bozza” finché questa scheda è aperta."
  : "Le modifiche non sono ancora sul server. Restano in “Riprendi bozza” finché non cambi sezione o chiudi l'app.";
const LOST_MESSAGE = "Le modifiche non sono ancora sul server e andranno perse.";
const GUARD_MESSAGE = Platform.OS === "web" ? KEPT_MESSAGE : LOST_MESSAGE;

// Uguali a parte lo step? (cambiare step non è una modifica da salvare)
const sameButStep = (a, b) => !!a && !!b && JSON.stringify({ ...a, step: 0 }) === JSON.stringify({ ...b, step: 0 });

export default function ExamEditorScreen({ route, nav, api, draft, dirty, updateDraft, env, contextLabel, draftStatus, draftStatusShort, onSaveDraft, onSaveAndClose, destroyAction, saving, onExit }) {
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

  // Uscita: conferma solo se ci sono davvero modifiche non salvate sul server
  // Ref: il tasto indietro di Android è registrato una volta sola e deve vedere i valori attuali
  const latest = useRef({ dirty, onExit });
  latest.current = { dirty, onExit };
  function requestExit() {
    const { dirty: isDirty, onExit: exit } = latest.current;
    if (isDirty) confirmLeave(exit, { message: KEPT_MESSAGE });
    else exit();
  }
  // La stessa conferma per indietro/avanti del browser, sidebar, tab ed "Esci"
  useEffect(() => {
    setLeaveGuard(dirty ? (proceed) => confirmLeave(proceed, { message: GUARD_MESSAGE }) : null);
    return () => setLeaveGuard(null);
  }, [dirty]);
  useBeforeUnload(dirty);

  // Tasto indietro di Android: come "← Esami". Registrato dopo quello della lista, quindi chiamato per primo.
  useEffect(() => {
    if (Platform.OS !== "android") return;
    const sub = BackHandler.addEventListener("hardwareBackPress", () => { requestExit(); return true; });
    return () => sub.remove();
  }, []);

  // Titolo nell'header di AppNav: "← Esami / titolo dell'esame" (o "Nuovo esame")
  const title = draft?.title?.trim() || (route.name === "new" ? "Nuovo esame" : "Modifica esame");
  useEffect(() => {
    setPageHeader({ title, onBack: requestExit });
    return () => setPageHeader(null);
  }, [title]);

  if (!ready) return loadingExam ? <Loader /> : null;

  return (
    <TestBuilder contextLabel={contextLabel} draft={draft} env={env} draftStatus={draftStatus} draftStatusShort={draftStatusShort}
      onChange={onChange} onSaveDraft={onSaveDraft} onSaveAndClose={onSaveAndClose} destroyAction={destroyAction}
      saving={saving} onClose={requestExit} />
  );
}
