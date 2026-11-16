import { formatDateForDisplay, parseISODate, toISODate } from "../../src/utils/dateUtils";
import { getPasswordErrors } from "../../src/utils/passwordValidation";
import { filterBySearch } from "../../src/utils/search";
import { examErrorMessage } from "../../src/utils/examErrors";
import { tipPosition, tipWidth } from "../../src/utils/tipPosition";
import { userDisplayName } from "../../src/utils/userName";

describe("dateUtils", () => {
  test("toISODate usa la data locale con zeri iniziali", () => {
    expect(toISODate(new Date(2026, 0, 5))).toBe("2026-01-05");
    expect(toISODate(new Date(2026, 11, 31))).toBe("2026-12-31");
  });

  test("parseISODate legge AAAA-MM-GG senza spostamenti di fuso", () => {
    const d = parseISODate("2026-03-09");
    expect([d.getFullYear(), d.getMonth(), d.getDate()]).toEqual([2026, 2, 9]);
  });

  test("parseISODate con valori vuoti o non validi restituisce oggi", () => {
    const today = new Date();
    for (const v of ["", null, undefined, "abc", "2026-xx-01"]) {
      const d = parseISODate(v);
      expect(d.toDateString()).toBe(today.toDateString());
    }
  });

  test("ida e ritorno tra le due funzioni", () => {
    expect(toISODate(parseISODate("2025-07-14"))).toBe("2025-07-14");
  });

  test("formatDateForDisplay mostra GG/MM/AAAA, stringa vuota se manca", () => {
    expect(formatDateForDisplay("2026-11-16")).toBe("16/11/2026");
    expect(formatDateForDisplay("")).toBe("");
  });
});

describe("passwordValidation (stesse regole del server)", () => {
  test("password valida, conferma uguale: nessun errore", () => {
    expect(getPasswordErrors("Segreta1!", "Segreta1!")).toEqual({});
  });

  test("basta un carattere speciale: maiuscole e numeri non sono obbligatori", () => {
    expect(getPasswordErrors("solo-minuscole", "")).toEqual({});
  });

  test("almeno 8 caratteri", () => {
    expect(getPasswordErrors("ab!", "").newPassword).toBe("La password deve avere almeno 8 caratteri.");
  });

  test("almeno un carattere speciale tra quelli ammessi dal server", () => {
    expect(getPasswordErrors("password1", "").newPassword).toMatch(/carattere speciale/);
    expect(getPasswordErrors("password.1", "").newPassword).toMatch(/carattere speciale/); // il punto non è tra gli ammessi
  });

  test("al massimo 72 byte UTF-8 (le lettere accentate valgono 2 byte)", () => {
    expect(getPasswordErrors("a!" + "x".repeat(70), "")).toEqual({});
    expect(getPasswordErrors("a!" + "x".repeat(71), "").newPassword).toMatch(/troppo lunga/);
    expect(getPasswordErrors("a!" + "è".repeat(36), "").newPassword).toMatch(/troppo lunga/);
  });

  test("la nuova password deve essere diversa da quella attuale", () => {
    expect(getPasswordErrors("Segreta1!", "", "Segreta1!").newPassword).toMatch(/diversa/);
  });

  test("conferma diversa", () => {
    expect(getPasswordErrors("Segreta1!", "Segreta2!").confirmPassword).toBe("Le password non coincidono");
  });

  test("campi vuoti: nessun errore (si mostrano solo mentre si scrive)", () => {
    expect(getPasswordErrors("", "")).toEqual({});
  });
});

describe("filterBySearch", () => {
  const people = [
    { first: "Mario", last: "Rossi" },
    { first: "Giulia", last: "Bianchi" },
    { first: "Niccolò", last: "Verdi" },
  ];
  const fields = p => [p.first, p.last];

  test("ricerca vuota: tutti", () => {
    expect(filterBySearch(people, "  ", fields)).toBe(people);
  });

  test("pezzi di parola, senza maiuscole", () => {
    expect(filterBySearch(people, "ross", fields)).toEqual([people[0]]);
    expect(filterBySearch(people, "GIU", fields)).toEqual([people[1]]);
  });

  test("più parole in qualsiasi ordine devono comparire tutte", () => {
    expect(filterBySearch(people, "ros mar", fields)).toEqual([people[0]]);
    expect(filterBySearch(people, "mario bianchi", fields)).toEqual([]);
  });

  test("ignora gli accenti", () => {
    expect(filterBySearch(people, "niccolo", fields)).toEqual([people[2]]);
  });

  test("campi mancanti non fanno fallire la ricerca", () => {
    expect(filterBySearch([{ first: null, last: undefined }], "x", fields)).toEqual([]);
  });
});

describe("examErrorMessage", () => {
  test("404: l'esame non esiste più", () => {
    expect(examErrorMessage("publish", { status: 404 })).toBe("L'esame non esiste più.");
  });

  test("409: messaggio specifico dell'azione", () => {
    expect(examErrorMessage("publish", { status: 409 })).toMatch(/pubblicare/);
    expect(examErrorMessage("close", { status: 409 })).toMatch(/consegne in corso/);
    expect(examErrorMessage("cancel", { status: 409 })).toMatch(/annullare/);
    expect(examErrorMessage("remove", { status: 409 })).toMatch(/eliminare/);
    expect(examErrorMessage("save", { status: 409 })).toMatch(/non è più in bozza/);
    expect(examErrorMessage("start", { status: 409 })).toMatch(/non è disponibile/);
  });

  test("409 per un'azione sconosciuta e altri errori: messaggio dell'errore o generico", () => {
    expect(examErrorMessage("load", { status: 409, message: "Conflitto" })).toBe("Conflitto");
    expect(examErrorMessage("load", { status: 500, message: "Errore interno" })).toBe("Errore interno");
    expect(examErrorMessage("load", null)).toBe("Si è verificato un errore.");
  });
});

describe("tipPosition", () => {
  const screen = { screenWidth: 400, screenHeight: 800 };

  test("larghezza mai oltre lo schermo meno i margini", () => {
    expect(tipWidth(300, 1000)).toBe(300);
    expect(tipWidth(300, 200)).toBe(176);
  });

  test("centrato sotto l'icona se c'è spazio", () => {
    const pos = tipPosition({ anchor: { x: 190, y: 100, width: 20, height: 20 }, width: 100, height: 50, ...screen });
    expect(pos).toEqual({ left: 150, top: 128 });
  });

  test("sopra l'icona se sotto non c'è spazio", () => {
    const pos = tipPosition({ anchor: { x: 190, y: 760, width: 20, height: 20 }, width: 100, height: 50, ...screen });
    expect(pos.top).toBe(760 - 8 - 50);
  });

  test("sempre dentro lo schermo ai bordi", () => {
    expect(tipPosition({ anchor: { x: 0, y: 10, width: 10, height: 10 }, width: 100, height: 20, ...screen }).left).toBe(12);
    expect(tipPosition({ anchor: { x: 395, y: 10, width: 10, height: 10 }, width: 100, height: 20, ...screen }).left).toBe(400 - 100 - 12);
  });
});

describe("userDisplayName", () => {
  test("nome e cognome del profilo studente o docente", () => {
    expect(userDisplayName({ studentFirstName: "Mario", studentLastName: "Rossi" })).toBe("Mario Rossi");
    expect(userDisplayName({ teacherFirstName: "Anna", teacherLastName: "Neri", displayName: "x" })).toBe("Anna Neri");
  });

  test("nome visualizzato se è il nome di una persona", () => {
    expect(userDisplayName({ displayName: "Laura Bianchi", email: "l@x.it" })).toBe("Laura Bianchi");
  });

  test("identificativi tecnici: si ripiega sulla parte locale dell'email", () => {
    expect(userDisplayName({ displayName: "admin_01", email: "segreteria@scuola.it" })).toBe("segreteria");
  });

  test("senza dati: 'Utente'", () => {
    expect(userDisplayName(null)).toBe("Utente");
    expect(userDisplayName({})).toBe("Utente");
  });
});
