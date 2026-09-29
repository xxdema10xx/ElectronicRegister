// src/data/classesMock.js
// Dati FISSI di prova per il flusso Classi (biennio → area → corso → classe → dettaglio).
// La forma segue il contratto proposto per il backend; da eliminare quando gli endpoint esisteranno.

export const BIENNI = [
  { id: "bn-2025", startYear: 2025, endYear: 2027 },
  { id: "bn-2026", startYear: 2026, endYear: 2028 },
];

// Aree di studio offerte in ogni biennio (id = bienniumStudyAreaId)
export const AREAS = {
  "bn-2025": [
    { id: "bsa-2025-ict", name: "Tecnologie dell'informazione e della comunicazione", description: "Sviluppo software, cloud e sicurezza informatica" },
    { id: "bsa-2025-mec", name: "Nuove tecnologie per il Made in Italy", description: "Meccatronica e automazione industriale" },
  ],
  "bn-2026": [
    { id: "bsa-2026-ict", name: "Tecnologie dell'informazione e della comunicazione", description: "Sviluppo software, dati e intelligenza artificiale" },
  ],
};

// Corsi di studio di ogni area (id = bienniumStudyPathId)
export const PATHS = {
  "bsa-2025-ict": [
    { id: "bsp-2025-sw", name: "Sviluppo software", description: "Programmazione, database e architetture web" },
    { id: "bsp-2025-cloud", name: "Cloud e cybersecurity", description: "Infrastrutture cloud e sicurezza delle reti" },
  ],
  "bsa-2025-mec": [
    { id: "bsp-2025-aut", name: "Automazione industriale", description: "PLC, robotica e sistemi di controllo" },
  ],
  "bsa-2026-ict": [
    { id: "bsp-2026-sw", name: "Sviluppo software", description: "Programmazione, database e architetture web" },
    { id: "bsp-2026-ai", name: "Data e intelligenza artificiale", description: "Analisi dei dati e machine learning" },
  ],
};

// Classi di ogni corso
export const CLASSES = {
  "bsp-2025-sw": [{ id: "cl-25-sw-a", name: "SW-25A" }, { id: "cl-25-sw-b", name: "SW-25B" }],
  "bsp-2025-cloud": [{ id: "cl-25-cl-a", name: "CLD-25A" }],
  "bsp-2025-aut": [{ id: "cl-25-au-a", name: "AUT-25A" }],
  "bsp-2026-sw": [{ id: "cl-26-sw-a", name: "SW-26A" }],
  "bsp-2026-ai": [{ id: "cl-26-ai-a", name: "AI-26A" }, { id: "cl-26-ai-b", name: "AI-26B" }],
};

// Insegnanti e materie del dettaglio di ogni classe (una materia = un insegnante)
const T = {
  rossi: { id: "t1", firstName: "Marco", lastName: "Rossi" },
  bianchi: { id: "t2", firstName: "Laura", lastName: "Bianchi" },
  verdi: { id: "t3", firstName: "Giulia", lastName: "Verdi" },
  neri: { id: "t4", firstName: "Paolo", lastName: "Neri" },
  gallo: { id: "t5", firstName: "Anna", lastName: "Gallo" },
  fontana: { id: "t6", firstName: "Luca", lastName: "Fontana" },
};
const subj = (id, name, teacher) => ({ subjectId: id, subjectName: name, teacherId: teacher.id, teacherFirstName: teacher.firstName, teacherLastName: teacher.lastName });

const SUBJECTS = {
  sw: [
    subj("s1", "Programmazione", T.rossi), subj("s2", "Basi di dati", T.bianchi),
    subj("s3", "Inglese tecnico", T.verdi), subj("s4", "Matematica applicata", T.neri), subj("s5", "Sistemi e reti", T.gallo),
  ],
  cloud: [
    subj("s6", "Cloud computing", T.fontana), subj("s7", "Sicurezza informatica", T.gallo),
    subj("s3", "Inglese tecnico", T.verdi), subj("s5", "Sistemi e reti", T.gallo),
  ],
  aut: [
    subj("s8", "Automazione e PLC", T.fontana), subj("s9", "Robotica", T.rossi),
    subj("s4", "Matematica applicata", T.neri), subj("s3", "Inglese tecnico", T.verdi),
  ],
  ai: [
    subj("s10", "Machine learning", T.bianchi), subj("s11", "Statistica", T.neri),
    subj("s1", "Programmazione", T.rossi), subj("s3", "Inglese tecnico", T.verdi),
  ],
};

const st = (n, firstName, lastName) => ({ id: `st-${n}`, firstName, lastName });

// Dettaglio classe: { id, name, students, subjects }
export const CLASS_DETAILS = {
  "cl-25-sw-a": { subjects: SUBJECTS.sw, students: [
    st(1, "Alessia", "Conti"), st(2, "Davide", "Marini"), st(3, "Elena", "Ferrari"), st(4, "Giorgio", "Bruno"),
    st(5, "Irene", "Costa"), st(6, "Matteo", "Greco"), st(7, "Sara", "Lombardi"), st(8, "Tommaso", "Ricci"),
  ] },
  "cl-25-sw-b": { subjects: SUBJECTS.sw, students: [
    st(9, "Beatrice", "Moretti"), st(10, "Carlo", "Barbieri"), st(11, "Federica", "Gatti"), st(12, "Lorenzo", "Serra"),
    st(13, "Martina", "Fabbri"), st(14, "Nicola", "Leone"),
  ] },
  "cl-25-cl-a": { subjects: SUBJECTS.cloud, students: [
    st(15, "Chiara", "Villa"), st(16, "Edoardo", "Mancini"), st(17, "Francesca", "Testa"), st(18, "Jacopo", "Pellegrini"),
    st(19, "Valentina", "Rinaldi"),
  ] },
  "cl-25-au-a": { subjects: SUBJECTS.aut, students: [
    st(20, "Andrea", "Caruso"), st(21, "Bruno", "D'Angelo"), st(22, "Cristina", "Monti"), st(23, "Diego", "Silvestri"),
  ] },
  "cl-26-sw-a": { subjects: SUBJECTS.sw, students: [
    st(24, "Emma", "Sala"), st(25, "Filippo", "Colombo"), st(26, "Ginevra", "Parisi"), st(27, "Hugo", "Vitale"),
    st(28, "Ilaria", "Orlando"), st(29, "Leonardo", "Amato"), st(30, "Noemi", "Bassi"),
  ] },
  "cl-26-ai-a": { subjects: SUBJECTS.ai, students: [
    st(31, "Aurora", "Neri"), st(32, "Christian", "Valentini"), st(33, "Daria", "Longo"), st(34, "Enrico", "Guerra"),
    st(35, "Gaia", "Coppola"),
  ] },
  "cl-26-ai-b": { subjects: SUBJECTS.ai, students: [
    st(36, "Kevin", "Piras"), st(37, "Luisa", "Battaglia"), st(38, "Michele", "Farina"), st(39, "Olivia", "De Luca"),
  ] },
};
