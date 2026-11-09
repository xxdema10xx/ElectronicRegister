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

// Classi del corso: id, name
export const getClasses = (bienniumStudyPathId, token) => getAll("/institution/classes", token, { bienniumStudyPathId });

// Assegnazioni classe-materia-docente (forma usata dalle schermate)
const toSubject = cs => ({
  classSubjectId: cs.id, subjectId: cs.subjectId, subjectName: cs.subjectName,
  teacherId: cs.teacherId, teacherFirstName: cs.teacherFirstName, teacherLastName: cs.teacherLastName,
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
