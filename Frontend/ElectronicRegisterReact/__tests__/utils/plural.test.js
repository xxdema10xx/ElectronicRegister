// Plurali dei conteggi dell'interfaccia: un solo helper.
import { plural, pluralWord } from "../../src/utils/plural";

test("singolare solo per 1, plurale per 0 e per gli altri numeri", () => {
  expect(plural(1, "materia", "materie")).toBe("1 materia");
  expect(plural(0, "materia", "materie")).toBe("0 materie");
  expect(plural(3, "studente", "studenti")).toBe("3 studenti");
  expect(plural(undefined, "lezione", "lezioni")).toBe("0 lezioni");
  expect(pluralWord(1, "punto", "punti")).toBe("punto");
  expect(pluralWord(0.5, "punto", "punti")).toBe("punti");
  expect(pluralWord("1", "punto", "punti")).toBe("punto");
});
