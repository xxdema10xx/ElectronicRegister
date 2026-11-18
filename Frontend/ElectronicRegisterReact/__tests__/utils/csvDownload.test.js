// CSV scaricabili dalla pagina dell'import: modello e elenco dei docenti.
import { calendarTemplateCsv, csvCell, teachersCsv } from "../../src/utils/csvDownload";
import { issueMessage } from "../../src/api/calendarImportService";

test("modello: UTF-8 con BOM, separatore ;, solo le intestazioni", () => {
  expect(calendarTemplateCsv()).toBe("\uFEFFData;Ora inizio;Ora fine;Classe;Modulo;Docente;Aula;Descrizione\r\n");
});

test("elenco docenti: Cognome;Nome;ID in ordine alfabetico, valori con ; tra virgolette", () => {
  const csv = teachersCsv([{ id: "g2", firstName: "Luca", lastName: "Bianchi" }, { id: "g1", firstName: "Anna", lastName: "De Rossi; Neri" }]);
  expect(csv).toBe('\uFEFFCognome;Nome;ID\r\nBianchi;Luca;g2\r\n"De Rossi; Neri";Anna;g1\r\n');
  expect(csvCell('a"b')).toBe('"a""b"');
});

test("formule di Excel neutralizzate con un apostrofo, prima delle virgolette", () => {
  expect(csvCell("=1+1")).toBe("'=1+1");
  expect(csvCell("+39 333")).toBe("'+39 333");
  expect(csvCell("-x")).toBe("'-x");
  expect(csvCell("@a")).toBe("'@a");
  expect(csvCell("\tx")).toBe("'\tx");
  expect(csvCell("\rx")).toBe("\"'\rx\""); // CR: apostrofo, poi tra virgolette
  expect(csvCell('=HYPERLINK("x";"y")')).toBe('"\'=HYPERLINK(""x"";""y"")"');
  expect(csvCell("a=b")).toBe("a=b"); // solo all'inizio
  expect(csvCell("Rossi")).toBe("Rossi");
  // elenco docenti scaricabile
  const csv = teachersCsv([{ id: "g1", firstName: "+39…", lastName: "=1+1" }, { id: "g2", firstName: "@a", lastName: "-x" }]);
  expect(csv).toBe("﻿Cognome;Nome;ID\r\n'-x;'@a;g2\r\n'=1+1;'+39…;g1\r\n");
});

test("errori di riga dell'import tradotti", () => {
  const msg = (code) => issueMessage({ code, field: "Teacher" });
  expect(msg("calendar.reason-required")).toMatch(/non è il titolare/);
  ["calendar.teacher-unavailable", "calendar.module-unavailable", "calendar.ambiguous-module", "calendar.class-subject-required",
    "calendar.row-outside-month", "calendar.row-outside-scope", "calendar.under-scheduled", "calendar.over-scheduled"]
    .forEach(code => expect(msg(code)).not.toMatch(/calendar\./));
  expect(msg("calendar.under-scheduled")).toBe("Ore sotto il previsto");
  expect(msg("calendar.over-scheduled")).toBe("Ore oltre il previsto");
});
