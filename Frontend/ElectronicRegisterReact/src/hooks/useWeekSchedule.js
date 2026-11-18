// src/hooks/useWeekSchedule.js
// Settimana mostrata dal calendario e lezioni di quella settimana.
//  - useWeek: settimana scelta (lunedì–domenica). Su web sta nell'URL (?settimana=AAAA-MM-GG, il lunedì),
//    così una ricarica non la perde; la settimana corrente non si scrive.
//  - useLessons: carica le lezioni e, a ogni cambio (settimana, classe…), annulla la richiesta precedente
//    e ne ignora la risposta: cliccando in fretta non compaiono le lezioni della settimana sbagliata.
import { useCallback, useEffect, useState } from "react";
import { currentWeek, parseYmd, shiftWeek, weekOf } from "../api/lessonService";
import { readSearchParams, replaceSearchParams } from "../navigation/examRoutes";

export const WEEK_PARAM = "settimana";

export function initialWeek(param = WEEK_PARAM) {
  const value = readSearchParams()[param];
  return parseYmd(value) ? weekOf(value) : currentWeek();
}

export function useWeek(param = WEEK_PARAM) {
  const [week, setWeek] = useState(() => initialWeek(param));
  useEffect(() => {
    replaceSearchParams({ [param]: week.from === currentWeek().from ? null : week.from });
  }, [week.from, param]);
  const prev = useCallback(() => setWeek(w => shiftWeek(w.from, -1)), []);
  const next = useCallback(() => setWeek(w => shiftWeek(w.from, 1)), []);
  const today = useCallback(() => setWeek(currentWeek()), []);
  const goTo = useCallback((date) => setWeek(weekOf(date)), []);
  return { week, prev, next, today, goTo };
}

// load(signal) → Promise<lezioni>. deps: quando cambiano si ricarica. reload(): ricarica a parità di deps.
export function useLessons(load, deps) {
  const [state, setState] = useState({ lessons: null, error: null });
  const [tick, setTick] = useState(0);
  useEffect(() => {
    const controller = typeof AbortController !== "undefined" ? new AbortController() : null;
    let alive = true;
    setState({ lessons: null, error: null });
    let request;
    try { request = Promise.resolve(load(controller?.signal)); } catch (e) { request = Promise.reject(e); }
    request
      .then(lessons => { if (alive) setState({ lessons: lessons || [], error: null }); })
      .catch(error => { if (alive) setState({ lessons: [], error }); });
    return () => { alive = false; controller?.abort(); };
  }, [...deps, tick]);
  const reload = useCallback(() => setTick(t => t + 1), []);
  return { ...state, reload };
}
