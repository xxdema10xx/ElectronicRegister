// src/navigation/examRoutes.js
// URL delle sezioni e rotte degli esami lato docente, senza librerie di routing (scelta del progetto):
//  - web: URL veri con la History API (pushState/replaceState/popstate): ricarica, indietro e avanti del
//    browser funzionano. In produzione il server web deve rimandare ogni percorso a index.html (README).
//  - app: una pila di schermate in memoria. Editor e dettaglio sono Modal: il tasto indietro di Android
//    arriva come onRequestClose (con un Modal aperto BackHandler non riceve eventi, docs RN 0.81), e su
//    web lo stesso callback scatta con Esc (react-native-web 0.21). BackHandler su web non va usato.
//
// Rotte degli esami:  /esami?tab=bozze|attivi|storico · /esami/nuovo · /esami/recuperi?classe=… · /esami/{id} ·
//                     /esami/{id}/modifica · /esami/{id}/consegne · /esami/{id}/consegne/{idConsegna}
// Nell'editor lo step è nell'URL (?step=2) con replaceState: "indietro" torna alla lista, non allo step prima.
import { useCallback, useEffect, useRef, useState } from "react";
import { Platform } from "react-native";

export const isWeb = Platform.OS === "web" && typeof window !== "undefined" && !!window.history;

// ── Sezioni dell'app (AppNav) ───────────────────────────────────────────────────────────────
const SCREEN_PATHS = {
  dashboard: "/", exams: "/esami", grades: "/voti", students: "/studenti", myClasses: "/classi", classes: "/classi",
  teachers: "/professori", subjects: "/materie", users: "/membri", profile: "/profilo",
};

function screenFromPath(pathname, role) {
  const first = `/${(pathname || "/").split("/").filter(Boolean)[0] || ""}`;
  if (first === "/classi") return role === "admin" ? "classes" : "myClasses";
  return Object.keys(SCREEN_PATHS).find(k => SCREEN_PATHS[k] === first) || null;
}

// Su web l'URL segue la sezione scelta (sidebar, tab, Home) e, all'avvio, la sceglie: ricaricando su
// /esami/123 si riapre Esami. Si usa replaceState: cambiare sezione non aggiunge voci alla cronologia.
// Lo studente che arriva su /esami/… (rotte del docente) resta sulla sua schermata esami, su /esami.
export function useScreenUrl(screen, setScreen, allowedKeys, role) {
  const started = useRef(false);
  useEffect(() => {
    if (!isWeb) return;
    if (!started.current) {
      started.current = true;
      const key = screenFromPath(window.location.pathname, role);
      if (key && key !== screen && allowedKeys.includes(key)) { setScreen(key); return; }
    }
    const { pathname, search } = window.location;
    if (screen === "exams") {
      if (role === "student") { if (pathname !== "/esami" || search) window.history.replaceState(null, "", "/esami"); }
      else if (!pathname.startsWith("/esami")) window.history.replaceState(null, "", "/esami");
      return; // il resto dell'URL lo gestisce useExamRoute (ExamsScreen)
    }
    const target = SCREEN_PATHS[screen] || "/";
    if (pathname !== target || search) window.history.replaceState(null, "", target);
  }, [screen]);
}

// ── Rotte degli esami ───────────────────────────────────────────────────────────────────────
export const TAB_SLUGS = { drafts: "bozze", active: "attivi", history: "storico" };
const TAB_FROM_SLUG = Object.fromEntries(Object.entries(TAB_SLUGS).map(([k, v]) => [v, k]));

// URL → rotta { name: "list"|"new"|"view"|"edit", id?, tab?, step? }; null se non è un URL degli esami
export function parseExamUrl(pathname, search) {
  const parts = (pathname || "").split("/").filter(Boolean).map(p => { try { return decodeURIComponent(p); } catch { return p; } });
  if (parts[0] !== "esami") return null;
  const q = new URLSearchParams(search || "");
  const n = Number(q.get("step"));
  const step = n === 1 || n === 2 || n === 3 ? n : null;
  if (parts.length === 1) return { name: "list", tab: TAB_FROM_SLUG[q.get("tab")] || null };
  if (parts.length === 2 && parts[1] === "nuovo") return { name: "new", step };
  if (parts.length === 2 && parts[1] === "recuperi") return { name: "retakes", classId: q.get("classe") || null };
  if (parts.length === 2) return { name: "view", id: parts[1] };
  if (parts.length === 3 && parts[2] === "modifica") return { name: "edit", id: parts[1], step };
  if (parts.length === 3 && parts[2] === "consegne") return { name: "submissions", id: parts[1] };
  if (parts.length === 4 && parts[2] === "consegne") return { name: "review", id: parts[1], submissionId: parts[3] };
  return { name: "list", tab: null };
}

// rotta → URL
export function examUrl(route) {
  const q = new URLSearchParams();
  let path = "/esami";
  if (route.name === "list") { if (route.tab) q.set("tab", TAB_SLUGS[route.tab]); }
  else if (route.name === "new") path += "/nuovo";
  else if (route.name === "retakes") { path += "/recuperi"; if (route.classId) q.set("classe", route.classId); }
  else if (route.name === "view") path += `/${encodeURIComponent(route.id)}`;
  else if (route.name === "edit") path += `/${encodeURIComponent(route.id)}/modifica`;
  else if (route.name === "submissions") path += `/${encodeURIComponent(route.id)}/consegne`;
  else if (route.name === "review") path += `/${encodeURIComponent(route.id)}/consegne/${encodeURIComponent(route.submissionId)}`;
  if ((route.name === "new" || route.name === "edit") && route.step && route.step > 1) q.set("step", String(route.step));
  const qs = q.toString();
  return qs ? `${path}?${qs}` : path;
}

const initialRoute = () => (isWeb && parseExamUrl(window.location.pathname, window.location.search)) || { name: "list", tab: null };

// Rotta corrente degli esami e funzioni per muoversi.
//   push(r)     apre una schermata sopra la corrente (web: nuova voce di cronologia)
//   replace(r)  sostituisce la corrente (web: replaceState)
//   back(f)     torna alla precedente; se non c'è (es. dopo una ricarica su /esami/nuovo) va a f
// Con modifiche non salvate (guardia d'uscita, vedi sotto) l'indietro/avanti del browser chiede conferma.
export function useExamRoute() {
  const [stack, setStack] = useState(() => [initialRoute()]);
  const route = stack[stack.length - 1];
  const routeRef = useRef(route);
  routeRef.current = route;
  const skipGuardRef = useRef(false); // "indietro" chiesto dall'app dopo la sua conferma: niente seconda domanda

  useEffect(() => {
    if (!isWeb) return;
    // Arrivando dalla sidebar l'URL è "/": si allinea alla lista
    if (!parseExamUrl(window.location.pathname)) window.history.replaceState({ exam: true }, "", examUrl(routeRef.current));
    const onPop = () => {
      const next = parseExamUrl(window.location.pathname, window.location.search);
      const skip = skipGuardRef.current;
      skipGuardRef.current = false;
      if (!next) return; // si è usciti dagli esami: la sezione la decide AppNav
      if (!skip && leaveGuard) {
        // Il browser non permette di annullare "indietro": si rimette subito l'URL dell'editor e si chiede;
        // scegliendo di uscire si torna di nuovo indietro, stavolta senza domande.
        window.history.pushState({ exam: true, pushed: true }, "", examUrl(routeRef.current));
        leaveGuard(() => { skipGuardRef.current = true; window.history.back(); });
        return;
      }
      setStack([next]);
    };
    window.addEventListener("popstate", onPop);
    return () => window.removeEventListener("popstate", onPop);
  }, []);

  const push = useCallback((r) => {
    if (isWeb) { window.history.pushState({ exam: true, pushed: true }, "", examUrl(r)); setStack([r]); }
    else setStack(s => [...s, r]);
  }, []);

  const replace = useCallback((r) => {
    if (isWeb) { window.history.replaceState({ exam: true, pushed: !!window.history.state?.pushed }, "", examUrl(r)); setStack([r]); }
    else setStack(s => [...s.slice(0, -1), r]);
  }, []);

  const back = useCallback((fallback = { name: "list", tab: null }) => {
    if (isWeb) {
      if (window.history.state?.pushed) { skipGuardRef.current = true; window.history.back(); }
      else replace(fallback);
      return;
    }
    setStack(s => (s.length > 1 ? s.slice(0, -1) : [fallback]));
  }, [replace]);

  return { route, push, replace, back };
}

// ── Guardia d'uscita (tutta l'app) ───────────────────────────────────────────────────────────
// Una pagina con modifiche non salvate registra guard(proceed): mostra la conferma e, se si esce, chiama
// proceed(). La usano indietro/avanti del browser (qui sopra), sidebar, tab, Home ed "Esci" (AppNav).
let leaveGuard = null;
export function setLeaveGuard(fn) { leaveGuard = fn || null; }
export function guardedNavigate(proceed) {
  if (leaveGuard) leaveGuard(proceed);
  else proceed();
}

// ── Titolo delle pagine interne ─────────────────────────────────────────────────────────────
// Dettaglio, scelta della classe ed editor non hanno una barra con il loro breadcrumb: lo mostra l'header
// di AppNav, al posto del titolo della sezione ("← Esami / Nuovo esame"). onBack è il ritorno alla lista
// (con la conferma, se serve). null = header normale della sezione.
let pageHeader = null;
const headerListeners = new Set();
export function setPageHeader(header) {
  pageHeader = header || null;
  headerListeners.forEach(fn => fn(pageHeader));
}
export function usePageHeader() {
  const [header, setHeader] = useState(pageHeader);
  useEffect(() => {
    headerListeners.add(setHeader);
    setHeader(pageHeader);
    return () => headerListeners.delete(setHeader);
  }, []);
  return header;
}

// href di una rotta per i link veri (solo web; sull'app non serve)
export const examHref = (route) => (isWeb ? examUrl(route) : undefined);

// Avviso del browser alla chiusura o ricarica del tab (solo web) finché `active` è vero
export function useBeforeUnload(active) {
  useEffect(() => {
    if (!isWeb || !active) return;
    const onUnload = (e) => { e.preventDefault(); e.returnValue = ""; };
    window.addEventListener("beforeunload", onUnload);
    return () => window.removeEventListener("beforeunload", onUnload);
  }, [active]);
}
