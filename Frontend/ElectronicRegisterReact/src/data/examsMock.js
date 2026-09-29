// src/data/examsMock.js
// Esami, test e domande FISSI di prova, mostrati quando il backend non ha ancora /Exam e /Test.
// Da eliminare quando gli endpoint esisteranno.

const exam = (n, name, subjectName, date, isRetake = false) =>
  ({ id: `demo-exam-${n}`, name, subjectId: `demo-subject-${n}`, subjectName, date, isRetake, demo: true });

export const DEMO_EXAMS = [
  exam(1, "Letteratura italiana", "Italiano", "2026-10-14"),
  exam(2, "Programmazione web", "Programmazione", "2026-10-20"),
  exam(3, "Basi di dati", "Basi di dati", "2026-11-03"),
  exam(4, "Inglese tecnico", "Inglese", "2026-11-10", true),
  exam(5, "Matematica applicata", "Matematica", "2026-11-17"),
];

// ── Domande: stessa forma prodotta dall'editor (src/components/TestBuilder.js) ──
let seq = 0;
const options = (texts) => texts.map(text => ({ id: `o${++seq}`, text }));
const closedQuestion = (type, text, texts, correctIdx, points) => {
  const opts = options(texts);
  return { id: `q${++seq}`, text, type, options: opts, correct: correctIdx.map(i => opts[i].id), points, required: true };
};
// Una sola risposta giusta
const single = (text, texts, correctIdx, points) => closedQuestion("single", text, texts, [correctIdx], points);
// Una o più risposte giuste
const multiple = (text, texts, correctIdx, points) => closedQuestion("multiple", text, texts, correctIdx, points);
// Risposta scritta
const open = (text, points) => ({ id: `q${++seq}`, text, type: "open", options: [], correct: [], points, required: true });

// Test con conteggi calcolati dalle domande, come li salverebbe l'editor
const test = (id, examN, name, description, numberOfStudents, questions) => {
  const closedQuestions = questions.filter(q => q.type !== "open").length;
  return {
    id: `demo-test-${id}`, examId: `demo-exam-${examN}`, name, description, numberOfStudents, questions,
    totalQuestions: questions.length, closedQuestions, openQuestions: questions.length - closedQuestions,
    totalPoints: questions.reduce((sum, q) => sum + q.points, 0), status: "Confirmed",
  };
};

// Test di ogni esame di prova (l'esame 5 non ne ha, per provare lo stato vuoto)
export const DEMO_TESTS = {
  "demo-exam-1": [
    test(1, 1, "Verifica su Manzoni", "I Promessi Sposi: trama, personaggi e temi.", 22, [
      single("In quale anno esce l'edizione definitiva dei Promessi Sposi?", ["1821", "1827", "1840", "1861"], 2, 3),
      single("Chi ostacola il matrimonio tra Renzo e Lucia?", ["Don Rodrigo", "Don Abbondio", "Fra Cristoforo", "L'Innominato"], 0, 3),
      multiple("Quali di questi sono personaggi del romanzo?", ["Lucia Mondella", "Agnese", "Gertrude", "Beatrice"], [0, 1, 2], 4),
      open("Spiega il ruolo della Provvidenza nel romanzo.", 10),
      open("Riassumi in massimo dieci righe l'assalto ai forni.", 10),
    ]),
    test(2, 1, "Verifica su Dante", "Divina Commedia: struttura dell'Inferno e canti scelti.", 22, [
      single("Quanti sono i cerchi dell'Inferno?", ["7", "9", "12"], 1, 4),
      single("Chi guida Dante attraverso l'Inferno e il Purgatorio?", ["Virgilio", "Beatrice", "San Bernardo"], 0, 4),
      multiple("Quali personaggi incontra Dante nell'Inferno?", ["Paolo e Francesca", "Ulisse", "Catone", "Ugolino"], [0, 1, 3], 6),
      open("Commenta l'episodio di Paolo e Francesca (canto V).", 10),
    ]),
  ],
  "demo-exam-2": [
    test(3, 2, "Quiz HTML e CSS", "Struttura di una pagina e stili di base.", 18, [
      single("Quale tag definisce il titolo principale di una pagina?", ["<h1>", "<head>", "<title>", "<header>"], 0, 3),
      single("Quale proprietà CSS cambia il colore del testo?", ["color", "font-color", "text-color"], 0, 3),
      multiple("Quali sono elementi semantici di HTML5?", ["<section>", "<article>", "<div>", "<nav>"], [0, 1, 3], 4),
      single("Quale unità CSS è relativa al font dell'elemento?", ["em", "px", "cm"], 0, 3),
      open("Spiega la differenza tra margin e padding.", 7),
    ]),
    test(4, 2, "Test JavaScript", "Variabili, tipi, funzioni e array.", 18, [
      single("Quale parola chiave dichiara una costante?", ["var", "let", "const"], 2, 3),
      single("Cosa restituisce typeof null?", ["\"null\"", "\"object\"", "\"undefined\""], 1, 3),
      multiple("Quali di questi valori sono \"falsy\"?", ["0", "\"\" (stringa vuota)", "\"0\"", "null"], [0, 1, 3], 4),
      single("Quale metodo crea un nuovo array applicando una funzione a ogni elemento?", ["forEach", "map", "push"], 1, 3),
      open("Che cos'è una closure? Fai un esempio.", 10),
      open("Spiega la differenza tra == e ===.", 7),
    ]),
    test(5, 2, "Test React", "Componenti, props e hook.", 18, [
      single("Quale hook gestisce lo stato in un componente funzionale?", ["useState", "useEffect", "useRef"], 0, 4),
      multiple("Quali affermazioni sulle props sono vere?", ["Sono di sola lettura", "Passano dati dal padre al figlio", "Il figlio può modificarle direttamente"], [0, 1], 5),
      single("Quando gira useEffect con array di dipendenze vuoto?", ["Solo dopo il primo render", "A ogni render", "Mai"], 0, 4),
      open("Descrivi il ciclo di vita di un componente usando gli hook.", 12),
    ]),
  ],
  "demo-exam-3": [
    test(6, 3, "Modello relazionale", "Chiavi, comandi SQL e normalizzazione.", 20, [
      single("Cos'è una chiave primaria?", ["Un campo che identifica in modo univoco ogni riga", "Un campo che può contenere duplicati", "Un indice su più tabelle"], 0, 3),
      multiple("Quali sono comandi SQL di manipolazione dei dati (DML)?", ["SELECT", "INSERT", "CREATE TABLE", "UPDATE"], [0, 1, 3], 4),
      single("Quale join restituisce solo le righe con corrispondenza in entrambe le tabelle?", ["INNER JOIN", "LEFT JOIN", "CROSS JOIN"], 0, 3),
      open("Cosa si intende per normalizzazione e perché è utile?", 5),
      open("Scrivi una query che conti gli studenti per ogni classe.", 5),
    ]),
  ],
  "demo-exam-4": [
    test(7, 4, "Recupero grammatica", "Present simple, past simple e modali.", 6, [
      single("Complete: \"She ___ to school every day.\"", ["go", "goes", "going"], 1, 3),
      single("What is the past simple of \"to write\"?", ["wrote", "writed", "written"], 0, 3),
      multiple("Which of these are modal verbs?", ["can", "must", "run", "should"], [0, 1, 3], 4),
      open("Write three sentences using the present perfect.", 8),
    ]),
  ],
};
