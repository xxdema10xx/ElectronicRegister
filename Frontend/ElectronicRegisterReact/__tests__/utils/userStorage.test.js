// Archivio locale separato per utente: sull'app vive in memoria ("local"), su web usa local/sessionStorage.

function fakeStorage() {
  const data = new Map();
  return {
    getItem: (k) => (data.has(k) ? data.get(k) : null),
    setItem: (k, v) => data.set(k, String(v)),
    removeItem: (k) => data.delete(k),
    key: (i) => [...data.keys()][i] ?? null,
    get length() { return data.size; },
    _data: data,
  };
}

describe("userStorage sull'app (memoria)", () => {
  let st;
  beforeEach(() => { jest.isolateModules(() => { st = require("../../src/utils/userStorage"); }); });

  test("senza utente collegato non legge e non scrive", () => {
    st.writeUser("local", "k", 1);
    expect(st.readUser("local", "k", "fallback")).toBe("fallback");
  });

  test("dati separati per utente", () => {
    st.setStorageUser("a");
    st.writeUser("local", "k", { v: 1 });
    st.setStorageUser("b");
    expect(st.readUser("local", "k", null)).toBeNull();
    st.setStorageUser("a");
    expect(st.readUser("local", "k", null)).toEqual({ v: 1 });
  });

  test("'session' sull'app non conserva nulla", () => {
    st.setStorageUser("a");
    st.writeUser("session", "k", 1);
    expect(st.readUser("session", "k", "none")).toBe("none");
  });

  test("removeUser e clearUserStorage", () => {
    st.setStorageUser("a");
    st.writeUser("local", "x", 1);
    st.writeUser("local", "y", 2);
    st.removeUser("local", "x");
    expect(st.readUser("local", "x", null)).toBeNull();
    st.clearUserStorage();
    expect(st.readUser("local", "y", null)).toBeNull();
  });

  test("isGuid", () => {
    expect(st.isGuid("3f2b8c1e-4a7d-4e9b-9c21-5d6f7a8b9c0d")).toBe(true);
    expect(st.isGuid("non-un-guid")).toBe(false);
    expect(st.isGuid(42)).toBe(false);
  });
});

describe("userStorage su web (localStorage / sessionStorage)", () => {
  let st, local, session;
  beforeEach(() => {
    local = fakeStorage();
    session = fakeStorage();
    global.window.localStorage = local;
    global.window.sessionStorage = session;
    jest.isolateModules(() => {
      require("react-native").Platform.OS = "web";
      st = require("../../src/utils/userStorage");
    });
  });
  afterEach(() => {
    require("react-native").Platform.OS = "ios";
    delete global.window.localStorage;
    delete global.window.sessionStorage;
  });

  test("scrive JSON con chiave per utente", () => {
    st.setStorageUser("u1");
    st.writeUser("local", "draft", { a: 1 });
    st.writeUser("session", "tab", "bozze");
    expect(local.getItem("er:u:u1:draft")).toBe('{"a":1}');
    expect(st.readUser("session", "tab", null)).toBe("bozze");
  });

  test("clearUserStorage toglie solo le chiavi dell'utente corrente", () => {
    st.setStorageUser("u1");
    st.writeUser("local", "a", 1);
    st.writeUser("session", "b", 2);
    local.setItem("er:u:u2:a", "1");
    local.setItem("altro", "x");
    st.clearUserStorage();
    expect([...local._data.keys()].sort()).toEqual(["altro", "er:u:u2:a"]);
    expect(session.length).toBe(0);
  });

  test("purgeLegacyStorage cancella le chiavi senza proprietario delle versioni precedenti", () => {
    local.setItem("examDraft4", "{}");
    session.setItem("studentExamRuns2", "{}");
    local.setItem("er:u:u1:x", "1");
    st.purgeLegacyStorage();
    expect(local.getItem("examDraft4")).toBeNull();
    expect(session.getItem("studentExamRuns2")).toBeNull();
    expect(local.getItem("er:u:u1:x")).toBe("1");
  });

  test("JSON non valido o archivio che lancia errori: si usa il valore di ripiego", () => {
    st.setStorageUser("u1");
    local.setItem("er:u:u1:bad", "{non json");
    expect(st.readUser("local", "bad", "fb")).toBe("fb");
    local.setItem = () => { throw new Error("pieno"); };
    expect(() => st.writeUser("local", "x", 1)).not.toThrow();
  });
});
