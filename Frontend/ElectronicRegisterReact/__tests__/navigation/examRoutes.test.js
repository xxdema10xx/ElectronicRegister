// Rotte degli esami (URL ⇄ rotta), pila di navigazione sull'app, guardia d'uscita e titolo delle pagine interne.
import { act, renderHook } from "@testing-library/react-native";
import {
  TAB_SLUGS, examHref, examUrl, guardedNavigate, parseExamUrl, setLeaveGuard, setPageHeader, useExamRoute, usePageHeader,
  useStudentRoute,
} from "../../src/navigation/examRoutes";

describe("URL degli esami", () => {
  test.each([
    ["/esami", "", { name: "list", tab: null }],
    ["/esami", "?tab=bozze", { name: "list", tab: "drafts" }],
    ["/esami", "?tab=storico", { name: "list", tab: "history" }],
    ["/esami/nuovo", "?step=2", { name: "new", step: 2 }],
    ["/esami/nuovo", "?step=9", { name: "new", step: null }],
    ["/esami/recuperi", "?classe=c1", { name: "retakes", classId: "c1" }],
    ["/esami/abc", "", { name: "view", id: "abc" }],
    ["/esami/abc/modifica", "?step=3", { name: "edit", id: "abc", step: 3 }],
    ["/esami/abc/consegne", "", { name: "submissions", id: "abc" }],
    ["/esami/abc/consegne/s%201", "", { name: "review", id: "abc", submissionId: "s 1" }],
    ["/esami/a/b/c/d/e", "", { name: "list", tab: null }],
  ])("%s%s", (path, search, route) => {
    expect(parseExamUrl(path, search)).toEqual(route);
  });

  test("percorsi fuori dagli esami → null", () => {
    expect(parseExamUrl("/studenti", "")).toBeNull();
    expect(parseExamUrl("", "")).toBeNull();
  });

  test.each([
    [{ name: "list", tab: "active" }, "/esami?tab=attivi"],
    [{ name: "list", tab: null }, "/esami"],
    [{ name: "new", step: 1 }, "/esami/nuovo"],
    [{ name: "new", step: 2 }, "/esami/nuovo?step=2"],
    [{ name: "retakes", classId: "c1" }, "/esami/recuperi?classe=c1"],
    [{ name: "view", id: "a b" }, "/esami/a%20b"],
    [{ name: "edit", id: "e1", step: 3 }, "/esami/e1/modifica?step=3"],
    [{ name: "submissions", id: "e1" }, "/esami/e1/consegne"],
    [{ name: "review", id: "e1", submissionId: "s1" }, "/esami/e1/consegne/s1"],
  ])("examUrl(%o) = %s", (route, url) => {
    expect(examUrl(route)).toBe(url);
  });

  test("ida e ritorno per ogni scheda", () => {
    for (const tab of Object.keys(TAB_SLUGS)) {
      const [path, search] = examUrl({ name: "list", tab }).split("?");
      expect(parseExamUrl(path, `?${search}`)).toEqual({ name: "list", tab });
    }
  });

  test("href solo su web", () => {
    expect(examHref({ name: "view", id: "e1" })).toBeUndefined();
  });
});

describe("pila delle schermate sull'app", () => {
  test("push, replace e back; back senza schermate precedenti va al ripiego", () => {
    const { result } = renderHook(() => useExamRoute());
    expect(result.current.route).toEqual({ name: "list", tab: null });
    act(() => result.current.push({ name: "view", id: "e1" }));
    act(() => result.current.push({ name: "submissions", id: "e1" }));
    expect(result.current.route).toEqual({ name: "submissions", id: "e1" });
    act(() => result.current.replace({ name: "review", id: "e1", submissionId: "s1" }));
    act(() => result.current.back());
    expect(result.current.route).toEqual({ name: "view", id: "e1" });
    act(() => result.current.back());
    act(() => result.current.back({ name: "list", tab: "drafts" }));
    expect(result.current.route).toEqual({ name: "list", tab: "drafts" });
  });

  test("rotte degli studenti", () => {
    const { result } = renderHook(() => useStudentRoute());
    expect(result.current.route).toEqual({ name: "list" });
    act(() => result.current.push({ name: "grades", id: "s1" }));
    expect(result.current.route).toEqual({ name: "grades", id: "s1" });
    act(() => result.current.back());
    expect(result.current.route).toEqual({ name: "list" });
  });
});

describe("guardia d'uscita", () => {
  afterEach(() => setLeaveGuard(null));

  test("senza guardia si procede subito", () => {
    const proceed = jest.fn();
    guardedNavigate(proceed);
    expect(proceed).toHaveBeenCalled();
  });

  test("con modifiche non salvate decide la guardia", () => {
    const guard = jest.fn();
    const proceed = jest.fn();
    setLeaveGuard(guard);
    guardedNavigate(proceed);
    expect(proceed).not.toHaveBeenCalled();
    expect(guard).toHaveBeenCalledWith(proceed);
    guard.mock.calls[0][0]();
    expect(proceed).toHaveBeenCalled();
  });
});

describe("titolo delle pagine interne", () => {
  test("usePageHeader segue setPageHeader", () => {
    const { result } = renderHook(() => usePageHeader());
    expect(result.current).toBeNull();
    const header = { title: "Nuovo esame", onBack: jest.fn() };
    act(() => setPageHeader(header));
    expect(result.current).toBe(header);
    act(() => setPageHeader(null));
    expect(result.current).toBeNull();
  });
});
