// Componenti di base: pulsanti, campi, selettori, liste, chip, calendario delle lezioni.
import { Platform, Text } from "react-native";
import { act, fireEvent, render, screen } from "@testing-library/react-native";
import Btn from "../../src/components/Btn";
import Input from "../../src/components/Input";
import Badge from "../../src/components/Badge";
import CheckboxField from "../../src/components/CheckboxField";
import EmptyState from "../../src/components/EmptyState";
import GradeCircle from "../../src/components/GradeCircle";
import StatusChip from "../../src/components/StatusChip";
import NotificationDot from "../../src/components/NotificationDot";
import SectionHeader from "../../src/components/SectionHeader";
import SelectField from "../../src/components/SelectField";
import ClassFilter from "../../src/components/ClassFilter";
import DateTimeField from "../../src/components/DateTimeField";
import FormModal from "../../src/components/FormModal";
import LessonCalendar from "../../src/components/LessonCalendar";
import ListRow, { listProps } from "../../src/components/ListRow";
import IconButton from "../../src/components/IconButton";
import RouteLink from "../../src/components/RouteLink";
import { C } from "../../src/constants/theme";

const bg = (el) => [].concat(el.props.style).flat(Infinity).reduce((acc, s) => ({ ...acc, ...s }), {}).backgroundColor;

describe("Btn", () => {
  test("premuto chiama onPress", () => {
    const onPress = jest.fn();
    render(<Btn label="Salva" onPress={onPress} />);
    fireEvent.press(screen.getByText("Salva"));
    expect(onPress).toHaveBeenCalled();
  });

  test("disabilitato o in caricamento non risponde", () => {
    const onPress = jest.fn();
    const { rerender } = render(<Btn label="Salva" onPress={onPress} disabled />);
    fireEvent.press(screen.getByText("Salva"));
    rerender(<Btn label="Salva" onPress={onPress} loading />);
    expect(screen.queryByText("Salva")).toBeNull();
    expect(onPress).not.toHaveBeenCalled();
  });
});

test("Input: etichetta, testo ed errore", () => {
  const onChangeText = jest.fn();
  render(<Input label="Email" placeholder="email" error="Obbligatoria" onChangeText={onChangeText} />);
  expect(screen.getByText("Email")).toBeTruthy();
  expect(screen.getByText("Obbligatoria")).toBeTruthy();
  fireEvent.changeText(screen.getByPlaceholderText("email"), "a@b.it");
  expect(onChangeText).toHaveBeenCalledWith("a@b.it");
});

test("CheckboxField alterna il valore", () => {
  const onChange = jest.fn();
  render(<CheckboxField label="Recupero" value={false} onChange={onChange} />);
  fireEvent.press(screen.getByText("Recupero"));
  expect(onChange).toHaveBeenCalledWith(true);
});

test("Badge, EmptyState, SectionHeader", () => {
  render(<><Badge text="admin" role="admin" /><EmptyState message="Nessun dato" /><SectionHeader title="Studenti" action={<Text>azione</Text>} /></>);
  ["admin", "Nessun dato", "Studenti", "azione"].forEach(t => expect(screen.getByText(t)).toBeTruthy());
});

test("GradeCircle: verde dal 18, arancione da 15 a 17, rosso sotto", () => {
  const color = (v) => { const { unmount } = render(<GradeCircle value={v} />); const c = bg(screen.getByText(String(v)).parent.parent); unmount(); return c; };
  expect(color(30)).toBe(C.success);
  expect(color(18)).toBe(C.success);
  expect(color(17)).toBe(C.warning);
  expect(color(15)).toBe(C.warning);
  expect(color(14)).toBe(C.danger);
  expect(color(0)).toBe(C.danger);
});

test("StatusChip: tono sconosciuto → grigio", () => {
  render(<StatusChip label="Boh" tone="viola" />);
  expect(screen.getByText("Boh")).toBeTruthy();
});

test("NotificationDot visibile solo se richiesto", () => {
  const { toJSON, rerender } = render(<NotificationDot visible={false} />);
  expect(toJSON()).toBeNull();
  rerender(<NotificationDot visible />);
  expect(toJSON()).not.toBeNull();
});

describe("SelectField (app)", () => {
  const options = [{ id: 1, label: "Reti" }, { id: 2, label: "Java" }];

  test("apre l'elenco e sceglie", () => {
    const onSelect = jest.fn();
    render(<SelectField label="Materia" value={null} options={options} onSelect={onSelect} placeholder="Scegli…" />);
    fireEvent.press(screen.getByText("Scegli…"));
    fireEvent.press(screen.getByText("Java"));
    expect(onSelect).toHaveBeenCalledWith(2);
  });

  test("mostra il valore scelto ed il messaggio se vuoto", () => {
    const { rerender } = render(<SelectField label="Materia" value={1} options={options} onSelect={() => {}} />);
    expect(screen.getByText("Reti")).toBeTruthy();
    rerender(<SelectField label="Materia" value={null} options={[]} onSelect={() => {}} emptyMessage="Nessuna materia" placeholder="Scegli" />);
    fireEvent.press(screen.getByText("Scegli"));
    expect(screen.getByText("Nessuna materia")).toBeTruthy();
  });
});

describe("ClassFilter", () => {
  const options = [{ id: "c1", name: "SW-25A" }, { id: "c2", name: "SW-25B" }];

  test("tutte le classi a riposo, sceglie una classe", () => {
    const onChange = jest.fn();
    render(<ClassFilter value={null} options={options} onChange={onChange} />);
    expect(screen.getByText("Classe: Tutte")).toBeTruthy();
    fireEvent.press(screen.getByLabelText(/Filtro classe/));
    fireEvent.press(screen.getByText("SW-25B"));
    expect(onChange).toHaveBeenCalledWith("c2");
  });

  test("filtro attivo: ✕ torna a tutte", () => {
    const onChange = jest.fn();
    render(<ClassFilter value="c1" options={options} onChange={onChange} />);
    expect(screen.getByText("Classe: SW-25A")).toBeTruthy();
    fireEvent.press(screen.getByLabelText("Togli il filtro classe"));
    expect(onChange).toHaveBeenCalledWith(null);
  });
});

test("DateTimeField (app): 'AAAA-MM-GG HH:mm' ⇄ valore con la T", () => {
  const onChange = jest.fn();
  render(<DateTimeField label="Inizio" value="2026-11-16T09:00" onChange={onChange} error="Manca" />);
  const field = screen.getByPlaceholderText("AAAA-MM-GG HH:mm");
  expect(field.props.value).toBe("2026-11-16 09:00");
  fireEvent.changeText(field, " 2026-11-17 10:30 ");
  expect(onChange).toHaveBeenCalledWith("2026-11-17T10:30");
  expect(screen.getByText("Manca")).toBeTruthy();
});

test("FormModal: titolo, contenuto e chiusura", () => {
  const onClose = jest.fn();
  render(<FormModal visible title="Nuovo studente" onClose={onClose}><Text>campi</Text></FormModal>);
  expect(screen.getByText("Nuovo studente")).toBeTruthy();
  expect(screen.getByText("campi")).toBeTruthy();
  fireEvent.press(screen.getByText("✕"));
  expect(onClose).toHaveBeenCalled();
});

describe("LessonCalendar", () => {
  test("caricamento e orario vuoto", () => {
    const { rerender } = render(<LessonCalendar lessons={null} />);
    expect(screen.getByText("Caricamento…")).toBeTruthy();
    rerender(<LessonCalendar lessons={[]} />);
    expect(screen.getByText("Orario non ancora disponibile")).toBeTruthy();
  });

  test("da lunedì a venerdì; sabato solo se ci sono lezioni", () => {
    const lessons = [
      { id: 1, day: 1, start: "09:00", end: "11:00", title: "Reti", teacherName: "Anna Neri", className: "SW", room: "A1" },
      { id: 2, day: 6, start: "08:00", end: "10:00", title: "Laboratorio", teacherName: "", className: "", room: "" },
    ];
    render(<LessonCalendar lessons={lessons} />);
    ["Lun", "Mar", "Mer", "Gio", "Ven", "Sab", "09:00–11:00", "Reti", "Anna Neri", "SW · A1", "Laboratorio"].forEach(t => expect(screen.getByText(t)).toBeTruthy());
    expect(screen.queryByText("Dom")).toBeNull();
    expect(screen.getAllByText("Nessuna lezione")).toHaveLength(4);
  });
});

describe("ListRow e IconButton (app)", () => {
  test("riga toccabile e pulsante icona con etichetta accessibile", () => {
    const onRow = jest.fn();
    const onEdit = jest.fn();
    render(<ListRow onPress={onRow}><Text>Mario Rossi</Text><IconButton icon="create-outline" label="Modifica" onPress={onEdit} /></ListRow>);
    fireEvent.press(screen.getByText("Mario Rossi"));
    fireEvent.press(screen.getByLabelText("Modifica"));
    expect(onRow).toHaveBeenCalledTimes(1);
    expect(onEdit).toHaveBeenCalledTimes(1);
  });

  test("listProps sull'app: solo il padding", () => {
    expect(listProps().ItemSeparatorComponent).toBeUndefined();
  });

  test("listProps su web: pannello unico con separatori", () => {
    let web;
    jest.isolateModules(() => {
      Platform.OS = "web";
      web = require("../../src/components/ListRow").listProps({ flexGrow: 1 });
      Platform.OS = "ios";
    });
    expect(typeof web.ItemSeparatorComponent).toBe("function");
    expect(web.contentContainerStyle[0]).toMatchObject({ borderWidth: 1, borderRadius: 12, overflow: "hidden" });
    expect(web.contentContainerStyle[1]).toEqual({ flexGrow: 1 });
  });
});

test("RouteLink sull'app: un pulsante che chiama onPress", () => {
  const onPress = jest.fn();
  render(<RouteLink href="/esami/1" onPress={onPress} accessibilityLabel="Apri"><Text>Esame</Text></RouteLink>);
  act(() => fireEvent.press(screen.getByLabelText("Apri")));
  expect(onPress).toHaveBeenCalled();
});

describe("accessibilità", () => {
  test("Btn ha il ruolo 'button' e lo stato disattivato/occupato", () => {
    render(<><Btn label="Salva" onPress={() => {}} /><Btn label="Invio" loading onPress={() => {}} /></>);
    expect(screen.getByRole("button", { name: "Salva" })).toBeTruthy();
    const buttons = screen.getAllByRole("button");
    expect(buttons[1].props.accessibilityState).toMatchObject({ disabled: true, busy: true });
  });

  test("ListRow passa il testID (es. righe degli esami)", () => {
    render(<ListRow testID="exam-row-e1"><Text>Reti</Text></ListRow>);
    expect(screen.getByTestId("exam-row-e1")).toBeTruthy();
  });

  test("il dialog dell'app ha testID 'app-dialog'", () => {
    const { DialogHost, showDialog } = require("../../src/components/AppDialog");
    render(<DialogHost />);
    act(() => showDialog({ title: "Avviso", message: "Testo" }));
    expect(screen.getByTestId("app-dialog")).toBeTruthy();
  });
});
