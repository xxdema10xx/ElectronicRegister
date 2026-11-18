// src/api/classService.js
// Flusso Classi: biennio → area di studio → corso di studio → classe → dettaglio (docs/API.md §9–14).
// Il catalogo è riservato all'admin; il docente vede le proprie classi tramite le classi-materie.
// Gli elenchi del catalogo rifiutano parametri di query non previsti: si passano solo pageNumber/pageSize e i filtri ammessi.
import axiosClient, { authHeader } from "./axiosClient";

const PAGE = 100;
const get = async (path, token, params) => (await axiosClient.get(path, { headers: authHeader(token), params })).data;

// Tutte le pagine di un elenco { items, totalCount } oppure di un array semplice
async function getAll(path, token, filters = {}) {
  const out = [];
  for (let pageNumber = 1; ; pageNumber++) {
    const data = await get(path, token, { ...filters, pageNumber, pageSize: PAGE });
    const items = Array.isArray(data) ? data : (data?.items || []);
    out.push(...items);
    if (items.length < PAGE) return out;
  }
}

const year = (d) => String(d || "").slice(0, 4);

// Bienni: id, startYear, endYear (anni, ricavati dalle date del biennio)
export async function getActiveBienni(token) {
  const list = await getAll("/institution/biennia", token);
  return list.map(b => ({ ...b, startYear: year(b.startYear), endYear: year(b.endYear) }))
    .sort((a, b) => b.startYear.localeCompare(a.startYear));
}

// Aree di studio del biennio: id (bienniumStudyAreaId), name, description
export async function getStudyAreas(bienniumId, token) {
  const [links, areas] = await Promise.all([getAll("/institution/biennium-study-areas", token), getAll("/institution/study-areas", token)]);
  return links.filter(l => l.bienniumId === bienniumId).map(l => {
    const area = areas.find(a => a.id === l.studyAreaId);
    return { id: l.id, studyAreaId: l.studyAreaId, name: area?.name || "—", description: area?.description };
  });
}

// Corsi di studio dell'area: id (bienniumStudyPathId), name, description
export async function getStudyPaths(bienniumStudyAreaId, token) {
  const [links, paths] = await Promise.all([getAll("/institution/biennium-study-paths", token), getAll("/institution/study-paths", token)]);
  return links.filter(l => l.bienniumStudyAreaId === bienniumStudyAreaId).map(l => {
    const path = paths.find(p => p.id === l.studyPathId);
    return { id: l.id, studyPathId: l.studyPathId, name: path?.name || "—", description: path?.description };
  });
}

// Tutte le classi dell'istituto: id, name
export const getAllClasses = (token) => getAll("/institution/classes", token);

// Tutte le classi-materie dell'istituto (leggibili anche dallo staff, a differenza del catalogo delle classi)
export const getAllClassSubjects = (token) => getAll("/institution/class-subjects", token);

// Classi del corso: id, name
export const getClasses = (bienniumStudyPathId, token) => getAll("/institution/classes", token, { bienniumStudyPathId });

// Assegnazioni classe-materia-docente (forma usata dalle schermate)
const toSubject = cs => ({
  classSubjectId: cs.id, subjectId: cs.subjectId, subjectName: cs.subjectName,
  teacherId: cs.teacherId, teacherFirstName: cs.teacherFirstName, teacherLastName: cs.teacherLastName, revision: cs.revision,
});

// Dettaglio classe: { id, name, students: [{id, firstName, lastName}],
//   subjects: [{classSubjectId, subjectId, subjectName, teacherId, teacherFirstName, teacherLastName}] }
export async function getClassDetail(classId, className, token) {
  const [students, subjects] = await Promise.all([
    get("/institution/students", token, { classId }),
    getAll("/institution/class-subjects", token, { classId }),
  ]);
  return { id: classId, name: className, students: Array.isArray(students) ? students : [], subjects: subjects.map(toSubject) };
}

// Materie assegnate alla classe di uno studente: [{ id, name }] (ordinate), oppure [] se lo studente non ha
// una classe. Lo studente non porta la classe nel DTO: la si trova tra le classi che hanno materie assegnate.
// Riservato all'admin (classi-materie): per gli altri ruoli la chiamata fallisce e il chiamante ripiega.
export async function getStudentClassSubjects(studentId, token) {
  const assignments = await getAll("/institution/class-subjects", token);
  const classIds = [...new Set(assignments.map(cs => cs.classId))];
  const rosters = await Promise.all(classIds.map(async classId => {
    const st = await get("/institution/students", token, { classId });
    return { classId, has: Array.isArray(st) && st.some(x => x.id === studentId) };
  }));
  const classId = rosters.find(r => r.has)?.classId;
  if (!classId) return [];
  const byId = new Map();
  for (const cs of assignments) if (cs.classId === classId) byId.set(cs.subjectId, { id: cs.subjectId, name: cs.subjectName });
  return [...byId.values()].sort((a, b) => String(a.name).localeCompare(String(b.name)));
}

// Classi in cui insegna il docente: { id, name, students, subjects } per ciascuna.
// "subjects" contiene solo le materie insegnate DA LUI in quella classe, non quelle dei colleghi.
export async function getMyClasses(teacherId, token) {
  if (!teacherId) return [];
  const assignments = (await getAll("/institution/class-subjects", token, { teacherId })).filter(cs => cs.teacherId === teacherId);
  const byClass = new Map();
  for (const cs of assignments) {
    if (!byClass.has(cs.classId)) byClass.set(cs.classId, { id: cs.classId, name: cs.className, students: [], subjects: [] });
    byClass.get(cs.classId).subjects.push(toSubject(cs));
  }
  const classes = [...byClass.values()];
  await Promise.all(classes.map(async k => {
    const st = await get("/institution/students", token, { classId: k.id }).catch(() => []);
    k.students = Array.isArray(st) ? st : [];
  }));
  return classes.sort((a, b) => a.name.localeCompare(b.name));
}

// ── Scritture del catalogo e delle assegnazioni (solo admin, docs/api/INSTITUTION_ACADEMIC_MANAGEMENT.md) ─────
// POST restituisce l'id (GUID). Duplicati o dipendenze: 409. I PUT richiedono If-Match con la revisione letta.
const post = async (path, body, token) => (await axiosClient.post(path, body, { headers: authHeader(token) })).data;
const del = async (path, token) => (await axiosClient.delete(path, { headers: authHeader(token) })).data;

// Biennio: date AAAA-MM-GG (DateOnly), fine non prima dell'inizio
export const createBiennium = ({ startDate, endDate }, token) => post("/institution/biennia", { startYear: startDate, endYear: endDate }, token);

// Aree e corsi di studio dell'istituto (riusabili tra i bienni) e loro collegamento al biennio / all'area
export const getAllStudyAreas = (token) => getAll("/institution/study-areas", token);
export const getAllStudyPaths = (token) => getAll("/institution/study-paths", token);
export const createStudyArea = ({ name, description }, token) => post("/institution/study-areas", { name: name.trim(), description: (description || "").trim() }, token);
export const createStudyPath = ({ name, description }, token) => post("/institution/study-paths", { name: name.trim(), description: description?.trim() || null }, token);
export const linkStudyArea = (bienniumId, studyAreaId, token) => post("/institution/biennium-study-areas", { bienniumId, studyAreaId }, token);
export const linkStudyPath = (bienniumStudyAreaId, studyPathId, token) => post("/institution/biennium-study-paths", { bienniumStudyAreaId, studyPathId }, token);

// Classe del corso (nome 1–20 caratteri)
export const createClass = ({ name, bienniumStudyPathId }, token) => post("/institution/classes", { name: name.trim(), bienniumStudyPathId }, token);
export const deleteClass = (id, token) => del(`/institution/classes/${id}`, token);

// Materia della classe con il suo docente. Cambiare docente o togliere la materia è bloccato (409) se ci sono
// esami o voti collegati.
export const addClassSubject = ({ classId, subjectId, teacherId }, token) => post("/institution/class-subjects", { classId, subjectId, teacherId }, token);
export async function changeClassSubjectTeacher(classSubjectId, teacherId, revision, token) {
  await axiosClient.put(`/institution/class-subjects/${classSubjectId}`, { teacherId },
    { headers: { ...authHeader(token), "If-Match": `"${revision}"` } });
}
export const removeClassSubject = (classSubjectId, token) => del(`/institution/class-subjects/${classSubjectId}`, token);
