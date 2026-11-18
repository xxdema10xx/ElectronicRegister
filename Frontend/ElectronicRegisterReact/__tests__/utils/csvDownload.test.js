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

test("errori di riga dell'import tradotti", () => {
  const msg = (code) => issueMessage({ code, field: "Teacher" });
  expect(msg("calendar.reason-required")).toMatch(/non è il titolare/);
  ["calendar.teacher-unavailable", "calendar.module-unavailable", "calendar.ambiguous-module", "calendar.class-subject-required",
    "calendar.row-outside-month", "calendar.row-outside-scope", "calendar.under-scheduled", "calendar.over-scheduled"]
    .forEach(code => expect(msg(code)).not.toMatch(/calendar\./));
  expect(msg("calendar.under-scheduled")).toBe("Ore sotto il previsto");
  expect(msg("calendar.over-scheduled")).toBe("Ore oltre il previsto");
});
