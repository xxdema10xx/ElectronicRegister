// Scala dei voti in trentesimi: interi 0–30, sufficienza 18, nessuna lode.
import {
  GRADE_ERROR, GRADE_LABEL, GRADE_MAX, GRADE_MIN, GRADE_PASS, gradeTone, isValidGrade, parseGrade,
} from "../../src/constants/gradeScale";

test("costanti della scala", () => {
  expect([GRADE_MIN, GRADE_MAX, GRADE_PASS]).toEqual([0, 30, 18]);
  expect(GRADE_LABEL).toBe("Voto (0-30)");
  expect(GRADE_ERROR).toBe("Il voto deve essere un numero intero tra 0 e 30.");
});

test.each([
  ["0", 0], ["18", 18], ["30", 30], [" 24 ", 24], ["07", 7], [24, 24],
])("voto valido %p → %p", (text, value) => {
  expect(parseGrade(text)).toBe(value);
  expect(isValidGrade(text)).toBe(true);
});

test.each(["", " ", "31", "-1", "24,5", "24.5", "18.0", "30L", "lode", "1e1", null, undefined, "100"])(
  "voto non valido %p", (text) => {
    expect(parseGrade(text)).toBeNull();
    expect(isValidGrade(text)).toBe(false);
  });

test("tono del voto: sufficiente da 18, vicino alla soglia 15–17, insufficiente sotto", () => {
  expect([30, 18, 17, 15, 14, 0].map(gradeTone)).toEqual(["pass", "pass", "near", "near", "fail", "fail"]);
  expect(gradeTone(undefined)).toBe("neutral");
});
