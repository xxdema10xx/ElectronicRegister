// src/data/studentExams.js
// Test assegnati allo studente (dati di PROVA) e stato locale: notifiche già viste e consegne.
// Da sostituire con le API del backend quando esisteranno; lo stato è salvato nel browser (localStorage).
import { useEffect, useState } from "react";
import { Platform } from "react-native";
import { DEMO_EXAMS, DEMO_TESTS } from "./examsMock";

// Test da svolgere, con la durata in minuti
const ASSIGNED = [
  { testId: "demo-test-1", durationMinutes: 20 },
  { testId: "demo-test-4", durationMinutes: 30 },
  { testId: "demo-test-6", durationMinutes: 25 },
];

const allTests = Object.values(DEMO_TESTS).flat();

const KEY = "studentExamState";
const empty = () => ({ seen: [], submissions: {} });

function load() {
  try { return Platform.OS === "web" ? { ...empty(), ...JSON.parse(window.localStorage.getItem(KEY)) } : empty(); }
  catch { return empty(); }
}

let state = load();
const listeners = new Set();

function commit(next) {
  state = next;
  try { if (Platform.OS === "web") window.localStorage.setItem(KEY, JSON.stringify(state)); } catch {}
  listeners.forEach(l => l());
}

// Segna come viste le notifiche dei test (fa sparire il pallino)
export function markSeen(testIds) {
  const missing = testIds.filter(id => !state.seen.includes(id));
  if (missing.length) commit({ ...state, seen: [...state.seen, ...missing] });
}

// Registra la consegna: { answers, submittedAt, violations, autoSubmitted, reason }
export function submitExam(testId, submission) {
  commit({ ...state, submissions: { ...state.submissions, [testId]: submission } });
}

// Solo per la prova: riporta tutto allo stato iniziale (test da svolgere e pallino acceso)
export function resetStudentDemo() { commit(empty()); }

function snapshot() {
  const items = ASSIGNED.map(a => {
    const test = allTests.find(t => t.id === a.testId);
    const exam = DEMO_EXAMS.find(e => e.id === test.examId);
    return { ...a, test, exam, submission: state.submissions[a.testId] || null, isNew: !state.seen.includes(a.testId) };
  });
  return { items, unseenCount: items.filter(i => i.isNew && !i.submission).length };
}

// Test assegnati + numero di notifiche non ancora viste; si aggiorna da solo quando lo stato cambia
export function useStudentExams() {
  const [, force] = useState(0);
  useEffect(() => {
    const l = () => force(n => n + 1);
    listeners.add(l);
    return () => { listeners.delete(l); };
  }, []);
  return snapshot();
}
