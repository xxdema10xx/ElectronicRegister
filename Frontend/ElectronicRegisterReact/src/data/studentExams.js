// src/data/studentExams.js
// Esami dello studente: elenco dal backend (GET /Exam/assigned) più due piccoli stati locali al browser:
//  - le notifiche già viste (per il pallino "nuovo esame");
//  - la consegna in corso o appena conclusa di ogni esame: serve a riprendere dopo un refresh, perché il
//    server restituisce le domande solo all'avvio, e a ritrovare l'esito (il server non elenca gli id delle consegne).
import { useCallback, useEffect, useState } from "react";
import { useAuth } from "../hooks/useAuth";
import { createExamApi } from "../api/examApi";
import { readUser, writeUser } from "../utils/userStorage";

const SEEN_KEY = "studentExamSeen2";
const RUNS_KEY = "studentExamRuns2";

// Sempre riletti dall'archivio dell'utente collegato: niente copie in memoria che sopravvivano al cambio di utente
const readSeen = () => readUser("local", SEEN_KEY, []);
const readRuns = () => readUser("local", RUNS_KEY, {});
const listeners = new Set();
const emit = () => listeners.forEach(l => l());

// Segna come viste le notifiche degli esami (fa sparire il pallino)
export function markSeen(examIds) {
  const seen = readSeen();
  const missing = examIds.filter(id => !seen.includes(id));
  if (!missing.length) return;
  writeUser("local", SEEN_KEY, [...seen, ...missing]);
  emit();
}

// Consegna di un esame: { submissionId, startedAt, deadlineAt, exam, answers, revision, finished, result }
export const getRun = (examId) => readRuns()[examId] || null;
export function saveRun(examId, patch) {
  const runs = readRuns();
  writeUser("local", RUNS_KEY, { ...runs, [examId]: { ...(runs[examId] || {}), ...patch } });
}
export function notifyStudentExamsChanged() { emit(); }

// Esami assegnati allo studente + numero di notifiche non ancora viste; si aggiorna da solo
export function useStudentExams() {
  const { token, user } = useAuth();
  const role = user?.role?.toLowerCase();
  const [assigned, setAssigned] = useState([]);
  const [loaded, setLoaded] = useState(false);
  const [error, setError] = useState(null);
  const [, force] = useState(0);

  const reload = useCallback(async () => {
    if (role !== "student") return;
    try { setAssigned(await createExamApi(token).getAssigned()); setError(null); }
    catch (e) { setError(e.message); /* resta l'ultimo elenco noto */ }
    finally { setLoaded(true); }
  }, [token, user, role]);

  useEffect(() => {
    reload();
    const id = setInterval(reload, 60000);
    return () => clearInterval(id);
  }, [reload]);

  useEffect(() => {
    const l = () => { force(n => n + 1); reload(); };
    listeners.add(l);
    return () => { listeners.delete(l); };
  }, [reload]);

  const seen = readSeen();
  const unseenCount = assigned.filter(a => a.canStart && !seen.includes(a.id)).length;
  return { assigned, loaded, error, unseenCount, reload, isSeen: (id) => seen.includes(id) };
}
