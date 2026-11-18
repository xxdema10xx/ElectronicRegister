// Settimana scelta e lezioni: le risposte di una settimana lasciata vengono ignorate.
import { act, renderHook } from "@testing-library/react-native";
import { useLessons, useWeek } from "../../src/hooks/useWeekSchedule";
import { currentWeek, shiftWeek } from "../../src/api/lessonService";

function deferred() {
  let resolve;
  const promise = new Promise(r => { resolve = r; });
  return { promise, resolve };
}

test("useWeek: parte dalla settimana corrente e si sposta avanti e indietro", () => {
  const { result } = renderHook(() => useWeek());
  const now = currentWeek();
  expect(result.current.week).toEqual(now);
  act(() => result.current.next());
  expect(result.current.week).toEqual(shiftWeek(now.from, 1));
  act(() => { result.current.prev(); result.current.prev(); });
  expect(result.current.week).toEqual(shiftWeek(now.from, -1));
  act(() => result.current.today());
  expect(result.current.week).toEqual(now);
});

test("useLessons: la risposta della settimana precedente, arrivata in ritardo, non sostituisce quella nuova", async () => {
  const slow = deferred(), fast = deferred();
  const signals = [];
  const load = jest.fn((week, signal) => { signals.push(signal); return week === "a" ? slow.promise : fast.promise; });
  const { result, rerender } = renderHook(({ week }) => useLessons((signal) => load(week, signal), [week]), { initialProps: { week: "a" } });
  rerender({ week: "b" });
  expect(signals[0].aborted).toBe(true); // la richiesta lasciata viene annullata
  await act(async () => { fast.resolve([{ id: "b1" }]); });
  await act(async () => { slow.resolve([{ id: "a1" }]); });
  expect(result.current.lessons).toEqual([{ id: "b1" }]);
});

test("useLessons: errore → settimana vuota con l'errore; reload ricarica", async () => {
  const load = jest.fn().mockRejectedValueOnce(new Error("rete")).mockResolvedValueOnce([{ id: 1 }]);
  const { result } = renderHook(() => useLessons((s) => load(s), []));
  await act(async () => {});
  expect(result.current.lessons).toEqual([]);
  expect(result.current.error.message).toBe("rete");
  await act(async () => { result.current.reload(); });
  expect(result.current.lessons).toEqual([{ id: 1 }]);
  expect(load).toHaveBeenCalledTimes(2);
});
