// src/data/examsMock.js
// Dati di PROVA per gli esami, nella stessa forma del backend (ExamController). Servono solo quando il
// server non espone /Exam (vedi src/api/examApi.js) e si possono eliminare quando il backend è collegato.

// Classi-materia (ClassSubject) e studenti di ogni classe
export const MOCK_CLASS_SUBJECTS = [
  { id: "cs-it", classId: "cl-sw-a", label: "SW-25A · Italiano" },
  { id: "cs-prog", classId: "cl-sw-a", label: "SW-25A · Programmazione" },
  { id: "cs-db", classId: "cl-sw-a", label: "SW-25A · Basi di dati" },
  { id: "cs-en", classId: "cl-sw-a", label: "SW-25A · Inglese tecnico" },
  { id: "cs-ml", classId: "cl-ai-a", label: "AI-26A · Machine learning" },
];

const stu = (id, firstName, lastName) => ({ id, firstName, lastName });
export const MOCK_CLASS_STUDENTS = {
  "cl-sw-a": [
    stu("stu-a1", "Alessia", "Conti"), stu("stu-a2", "Davide", "Marini"), stu("stu-a3", "Elena", "Ferrari"), stu("stu-a4", "Giorgio", "Bruno"),
    stu("stu-a5", "Irene", "Costa"), stu("stu-a6", "Matteo", "Greco"), stu("stu-a7", "Sara", "Lombardi"), stu("stu-a8", "Tommaso", "Ricci"),
  ],
  "cl-ai-a": [
    stu("stu-i1", "Aurora", "Neri"), stu("stu-i2", "Christian", "Valentini"), stu("stu-i3", "Daria", "Longo"), stu("stu-i4", "Enrico", "Guerra"),
  ],
};

// ── Domande (forma dell'editor: vedi examContract.questionFromDto) ─────────────────────────
let seq = 0;
const options = (texts) => texts.map(text => ({ id: `mo${++seq}`, text }));
const closedQuestion = (type, text, texts, correctIdx, points, required = true) => {
  const opts = options(texts);
  return { id: `mq${++seq}`, text, type, options: opts, correct: correctIdx.map(i => opts[i].id), points: String(points), required };
};
const single = (text, texts, correctIdx, points) => closedQuestion("single", text, texts, [correctIdx], points);
const multiple = (text, texts, correctIdx, points) => closedQuestion("multiple", text, texts, correctIdx, points);
const open = (text, points) => ({ id: `mq${++seq}`, text, type: "open", options: [], correct: [], points: String(points), required: false });

// Esami di prova. Date relative a "adesso" al momento della creazione dei dati: vedi mockExamBackend.
// status: draft | published | closed
export const MOCK_EXAMS = [
  { key: "manzoni", classSubjectId: "cs-it", name: "Verifica su Manzoni", description: "I Promessi Sposi: trama, personaggi e temi.",
    duration: 20, passing: 60, status: "published", questions: [
      single("In quale anno esce l'edizione definitiva dei Promessi Sposi?", ["1821", "1827", "1840", "1861"], 2, 3),
      single("Chi ostacola il matrimonio tra Renzo e Lucia?", ["Don Rodrigo", "Don Abbondio", "Fra Cristoforo", "L'Innominato"], 0, 3),
      multiple("Quali di questi sono personaggi del romanzo?", ["Lucia Mondella", "Agnese", "Gertrude", "Beatrice"], [0, 1, 2], 4),
      open("Spiega il ruolo della Provvidenza nel romanzo.", 10),
      open("Riassumi in massimo dieci righe l'assalto ai forni.", 10),
    ] },
  { key: "js", classSubjectId: "cs-prog", name: "Test JavaScript", description: "Variabili, tipi, funzioni e array.",
    duration: 30, passing: 60, status: "published", questions: [
      single("Quale parola chiave dichiara una costante?", ["var", "let", "const"], 2, 3),
      single("Cosa restituisce typeof null?", ["\"null\"", "\"object\"", "\"undefined\""], 1, 3),
      multiple("Quali di questi valori sono \"falsy\"?", ["0", "\"\" (stringa vuota)", "\"0\"", "null"], [0, 1, 3], 4),
      single("Quale metodo crea un nuovo array applicando una funzione a ogni elemento?", ["forEach", "map", "push"], 1, 3),
      open("Che cos'è una closure? Fai un esempio.", 10),
      open("Spiega la differenza tra == e ===.", 7),
    ] },
  { key: "db", classSubjectId: "cs-db", name: "Modello relazionale", description: "Chiavi, comandi SQL e normalizzazione.",
    duration: 25, passing: 60, status: "published", questions: [
      single("Cos'è una chiave primaria?", ["Un campo che identifica in modo univoco ogni riga", "Un campo che può contenere duplicati", "Un indice su più tabelle"], 0, 3),
      multiple("Quali sono comandi SQL di manipolazione dei dati (DML)?", ["SELECT", "INSERT", "CREATE TABLE", "UPDATE"], [0, 1, 3], 4),
      single("Quale join restituisce solo le righe con corrispondenza in entrambe le tabelle?", ["INNER JOIN", "LEFT JOIN", "CROSS JOIN"], 0, 3),
      open("Cosa si intende per normalizzazione e perché è utile?", 5),
      open("Scrivi una query che conti gli studenti per ogni classe.", 5),
    ] },
  { key: "grammar", classSubjectId: "cs-en", name: "Grammatica inglese", description: "Solo domande chiuse: la valutazione è immediata.",
    duration: 15, passing: 60, status: "published", questions: [
      single("Complete: \"She ___ to school every day.\"", ["go", "goes", "going"], 1, 3),
      single("What is the past simple of \"to write\"?", ["wrote", "writed", "written"], 0, 3),
      multiple("Which of these are modal verbs?", ["can", "must", "run", "should"], [0, 1, 3], 4),
    ] },
  { key: "grammar-retake", classSubjectId: "cs-en", name: "Recupero grammatica", description: "Prova di recupero.",
    duration: 15, passing: 60, status: "draft", retakeOf: "grammar", questions: [
      single("Choose the correct form: \"They ___ finished.\"", ["has", "have", "is"], 1, 5),
      single("Which sentence is in the present perfect?", ["I have seen it", "I saw it", "I see it"], 0, 5),
    ] },
  { key: "html", classSubjectId: "cs-prog", name: "Quiz HTML e CSS", description: "Struttura di una pagina e stili di base.",
    duration: 20, passing: 60, status: "draft", questions: [
      single("Quale tag definisce il titolo principale di una pagina?", ["<h1>", "<head>", "<title>", "<header>"], 0, 3),
      single("Quale proprietà CSS cambia il colore del testo?", ["color", "font-color", "text-color"], 0, 3),
      multiple("Quali sono elementi semantici di HTML5?", ["<section>", "<article>", "<div>", "<nav>"], [0, 1, 3], 4),
      open("Spiega la differenza tra margin e padding.", 7),
    ] },
  { key: "react", classSubjectId: "cs-prog", name: "Test React", description: "Componenti, props e hook.",
    duration: 25, passing: 60, status: "closed", questions: [
      single("Quale hook gestisce lo stato in un componente funzionale?", ["useState", "useEffect", "useRef"], 0, 4),
      multiple("Quali affermazioni sulle props sono vere?", ["Sono di sola lettura", "Passano dati dal padre al figlio", "Il figlio può modificarle direttamente"], [0, 1], 5),
      open("Descrivi il ciclo di vita di un componente usando gli hook.", 12),
    ] },
];
