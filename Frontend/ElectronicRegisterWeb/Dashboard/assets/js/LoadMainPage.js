function setSubjectNameError(show) {
    const input = document.getElementById("modal-subject-name");
    const error = document.getElementById("modal-subject-name-error");
    if (!input || !error) return;
    input.style.border = show ? "1px solid red" : "";
    error.style.display = show ? "block" : "none";
}

document.getElementById("modal-subject-name")?.addEventListener("input", function () {
    if (this.value.trim().length >= 5) setSubjectNameError(false);
});

async function formatCardsData(requests, titles) {
    requests.forEach(async (request, index) => {
        try {
            const data = await sendTokenForData(request);
            document.getElementById(`card-${index + 1}-data`).innerText = data;
            document.getElementById(`card-${index + 1}-title`).innerText = titles[index];
        } catch (err) {
            console.error(err);
            document.getElementById(`card-${index + 1}-data`).innerText = "Errore";
            document.getElementById(`card-${index + 1}-title`).innerText = titles[index];
        }
    });
}

async function loadCardsData(userData) {
    const role = userData.role;
    const card1Data = document.getElementById("card-1-data");
    const card2Data = document.getElementById("card-2-data");
    const card3Data = document.getElementById("card-3-data");
    const card4Data = document.getElementById("card-4-data");
    const card1Title = document.getElementById("card-1-title");
    const card2Title = document.getElementById("card-2-title");
    const card3Title = document.getElementById("card-3-title");
    const card4Title = document.getElementById("card-4-title");
    const chartTitle = document.getElementById("chart-title");

    switch (role) {
        case "admin":
            if (chartTitle) chartTitle.innerText = "Andamento Media Voti Annuale";
            formatCardsData(
                [
                    `${API_BASE}/Student/count`,
                    `${API_BASE}/Teacher/count`,
                    `${API_BASE}/Grade/count`,
                    `${API_BASE}/Users/count`
                ],
                ["Allievi", "Insegnanti", "Voti Totali", "Utenti"]
            );
            const card3 = document.getElementById("card-3");
            if (card3) {
                card3.style.cursor = "pointer";
                card3.onclick = () => window.location.href = "grades.html";
            }
            break;
        case "teacher":
            if (chartTitle) chartTitle.innerText = "Media annuale dei voti da te inseriti";
            formatCardsData(
                [
                    `${API_BASE}/Subject/count`,
                    `${API_BASE}/Student/count`,
                    `${API_BASE}/Grade/count`
                ],
                ["Le Tue Materie", "I Tuoi Alunni", "I Tuoi Voti"]
            );
            const card4 = document.getElementById("card-4");
            if (card4) card4.remove();
            break;
        case "student":
            if (chartTitle) chartTitle.innerText = "Andamento Media Voti Annuale";
            try {
                const subjectsCount = await sendTokenForData(`${API_BASE}/Subject/count`);
                const studentGrades = await sendTokenForData(`${API_BASE}/Grade`);
                let gradesCount = 0;
                let total = 0;
                studentGrades.forEach(grade => {
                    gradesCount++;
                    total += grade.value;
                }); 
                const averageGrade = ( total / gradesCount ).toFixed(1);

                // Raggruppa i voti per materia e calcola la media di ciascuna
                const subjectAverages = {};
                studentGrades.forEach(grade => {
                    if (!subjectAverages[grade.subjectName]) {
                        subjectAverages[grade.subjectName] = { total: 0, count: 0 };
                    }
                    subjectAverages[grade.subjectName].total += grade.value;
                    subjectAverages[grade.subjectName].count++;
                });

                const bestSubject = studentGrades.length > 0
                    ? Object.entries(subjectAverages).reduce((best, [subject, data]) => {
                        const avg = data.total / data.count;
                        return avg > best.avg ? { name: subject, avg } : best;
                    }, { name: "N/A", avg: -1 }).name
                    : "N/A";

                card1Data.innerText = subjectsCount;
                card2Data.innerText = bestSubject;
                card3Data.innerText = averageGrade;
                card4Data.innerText = studentGrades.length > 0 ? Math.max(...studentGrades.map(g => g.value)).toFixed(1) : "N/A";
                card1Title.innerText = "Materie";
                card2Title.innerText = "Miglior Materia";
                card3Title.innerText = "Media Voti";
                card4Title.innerText = "Miglior Voto";

            } catch (err) {
                console.error(err);
                card1Data.innerText = "Errore";
                card2Data.innerText = "Errore";
                card3Data.innerText = "Errore";
                card4Data.innerText = "Errore";
            }
            break;
        default:
            break;
    }
}


async function loadChartData() {
    const statistics = await sendTokenForData(`${API_BASE}/Grade/statistics`);
    const yearlyAverage = statistics.yearlyAverage;
    const monthlyAverage = statistics.monthlyAverage.map(avg => avg !== null ? avg.toFixed(2) : 0);
    document.getElementById("average-yearly-grades").innerText = yearlyAverage.toFixed(1);
    chart1.data.datasets[0].data = monthlyAverage;
    chart1.update();
}

function renderUsersBodyAdmin(tbody, usersData) {
    const roleOrder = { teacher: 1, student: 2 };

    usersData
        .filter(user => user.role !== "admin")
        .sort((a, b) => roleOrder[a.role] - roleOrder[b.role])
        .forEach(user => {
            const row = document.createElement("tr");
            row.innerHTML = `
                <td class="min-width">
                    <div class="lead">
                        <div class="lead-text">
                            <p id="email-${user.id}" style="color: blue;">${user.email}</p>
                        </div>
                    </div>
                </td>
                <td class="min-width">
                    <p style="${user.studentId ? 'color: green;' : 'color: orange;'}">${user.role}</p>
                </td>
                <td class="min-width">
                    <p id="first-name-${user.id}" style="color: #2f2f2f;">${user.studentId ? user.studentFirstName : user.teacherFirstName}</p>
                </td>
                <td class="min-width">
                    <p id="last-name-${user.id}" style="color: #2f2f2f;">${user.studentId ? user.studentLastName : user.teacherLastName}</p>
                </td>
                <td>
                    <div class="action">
                        <button class="text-success" data-id="${user.id}" onclick="showModal('Users', this.dataset.id)">
                            <i class="lni lni-pencil-alt"></i>
                        </button>
                        <button class="text-danger" onclick="deleteUser('${user.id}')">
                            <i class="lni lni-trash-can"></i>
                        </button>
                    </div>
                </td>
            `;
            tbody.appendChild(row);
        });
}

function renderGradesBodyStudent(tbody, gradesData) {
    gradesData
    .sort((a, b) => new Date(b.date) - new Date(a.date))
    .forEach(grade => {
        const row = document.createElement("tr");
        row.innerHTML = `
            <td><p style="color: blue;">${grade.subjectName}</p></td>
            <td><p style="${grade.value >= 6 ? 'color: green;' : 'color: red;'}">${grade.value.toFixed(1)}</p></td>
            <td><p>${grade.date}</p></td>
        `;
        tbody.appendChild(row);
    });
}

function renderStudentsBodyAdmin(tbody, studentsData) {
    studentsData.forEach(student => {
        const row = document.createElement("tr");
        row.innerHTML = `
            <td class="min-width">
                <p style="color: blue;">${student.id}</p>
            </td>
            <td class="min-width">
                <p id="first-name-${student.id}">${student.firstName}</p>
            </td>
            <td class="min-width">
                <p id="last-name-${student.id}">${student.lastName}</p>
            </td>
            <td>
                <div class="action">
                    <button class="text-success" data-id="${student.id}" onclick="showModal('Student', this.dataset.id)">
                        <i class="lni lni-pencil-alt"></i>
                    </button>
                    <button class="text-danger" data-id="${student.id}" onclick="deleteStudent(this.dataset.id)">
                        <i class="lni lni-trash-can"></i>
                    </button>
                </div>
            </td>
        `;
        tbody.appendChild(row);
    });
}

function renderTeachersBodyAdmin(tbody, teachersData) {
    teachersData.forEach(teacher => {
        const row = document.createElement("tr");
        row.innerHTML = `
            <td class="min-width">
                <p style="color: blue;">${teacher.id}</p>
            </td>
            <td class="min-width">
                <p id="first-name-${teacher.id}">${teacher.firstName}</p>
            </td>
            <td class="min-width">
                <p id="last-name-${teacher.id}">${teacher.lastName}</p>
            </td>
            <td>
                <div class="action">
                    <button class="text-success" data-id="${teacher.id}" onclick="showModal('Teacher', this.dataset.id)">
                        <i class="lni lni-pencil-alt"></i>
                    </button>
                    <button class="text-danger" data-id="${teacher.id}" onclick="deleteTeacher(this.dataset.id)">
                        <i class="lni lni-trash-can"></i>
                    </button>
                </div>
            </td>
        `;
        tbody.appendChild(row);
    });
}

function renderSubjectsBodyAdmin(tbody, subjectsData) {
    subjectsData.forEach(subject => {
        const row = document.createElement("tr");
        row.innerHTML = `
            <td class="min-width">
                <p id="subject-name-${subject.id}" style="color: blue;">${subject.name}</p>
            </td>
            <td class="min-width">
                <p id="subject-teacher-${subject.id}" data-teacher-id="${subject.teacherId}">${subject.teacherFirstName} ${subject.teacherLastName}</p>
            </td>
            <td>
                <div class="action">
                    <button class="text-success" data-id="${subject.id}" onclick="showModal('Subject', this.dataset.id)">
                        <i class="lni lni-pencil-alt"></i>
                    </button>
                    <button class="text-danger" data-id="${subject.id}" onclick="deleteSubject(this.dataset.id)">
                        <i class="lni lni-trash-can"></i>
                    </button>
                </div>
            </td>
        `;
        tbody.appendChild(row);
    });
}

function renderSubjectsBodyStudent(tbody, subjectsData) {
    subjectsData.forEach(subject => {
        const row = document.createElement("tr");
        row.innerHTML = `
            <td class="min-width">
                <p style="color: blue;">${subject.name}</p>
            </td>
            <td class="min-width">
                <p style="color: orange;">${subject.teacherFirstName} ${subject.teacherLastName}</p>
            </td>
        `;
        tbody.appendChild(row);
    });
}

async function renderSubjectAveragesBodyStudent(tbody, subjectsData) {
    const grades = await sendTokenForData(`${API_BASE}/Grade`);
    grades.forEach(grade => {
        const subject = subjectsData.find(s => s.id === grade.subjectId);
        if (subject) {
            if (!subject.total) {
                subject.total = 0;
                subject.count = 0;
            }
            subject.total += grade.value;
            subject.count++;
        }
    });
    subjectsData.forEach(subject => {
        averageGrade = subject.count > 0 ? subject.total / subject.count : 0;
        const row = document.createElement("tr");
        row.innerHTML = `
            <td class="min-width">
                <p style="color: blue;">${subject.name}</p>
            </td>
            <td class="min-width">
                <p style="${averageGrade < 6 ? 'color: red;' : 'color: green;'}">${averageGrade.toFixed(1)}</p>
            </td>
        `;
        tbody.appendChild(row);
    });
}

function renderSubjectsBodyTeacher(tbody, subjectsData) {
    subjectsData.forEach(subject => {
        const row = document.createElement("tr");
        row.innerHTML = `
            <td class="min-width">
                <p style="color: orange;">${subject.name}</p>
            </td>
        `;
        tbody.appendChild(row);
    });
}

function renderStudentsBodyTeacher(tbody, studentsData) {
    studentsData.forEach(student => {
        const row = document.createElement("tr");
        row.innerHTML = `
            <td class="min-width">
                <p>${student.firstName}</p>
            </td>
            <td class="min-width">
                <p>${student.lastName}</p>
            </td>
        `;
        tbody.appendChild(row);
    });
}

function renderGradesBodyTeacher(tbody, gradesData) {
    gradesData
    .sort((a, b) => new Date(b.date) - new Date(a.date))
    .forEach(grade => {
        const row = document.createElement("tr");
        row.innerHTML = `
            <td><p id="subject-${grade.id}" style="color: blue;">${grade.subjectName}</p></td>
            <td><p id="grade-${grade.id}" style="${grade.value >= 6 ? 'color: green;' : 'color: red;'}">${grade.value.toFixed(1)}</p></td>
            <td><p>${grade.student?.firstName} ${grade.student?.lastName}</p></td>
            <td><p id="date-${grade.id}">${grade.date}</p></td>
            <td>
                <div class="action">
                    <button class="text-success" data-id="${grade.id}" onclick="showModal('Grade', this.dataset.id)">
                        <i class="lni lni-pencil-alt"></i>
                    </button>
                </div>
            </td>
        `;
        tbody.appendChild(row);
    });
}

async function loadTable(ids, title, description, tableHeadHtml, dataUrl, renderBody) {
    document.getElementById(ids.title).innerText = title;
    document.getElementById(ids.desc).innerText = description;
    document.getElementById(ids.head).innerHTML = tableHeadHtml;
    const tbody = document.getElementById(ids.body);
    tbody.innerHTML = "";
    const data = await sendTokenForData(`${API_BASE}${dataUrl}`);
    console.log(`Data fetched for ${title}:`, data);
    renderBody(tbody, data);
}

const PAGINATED_GRADES_PAGE_SIZE = 20;

// Come loadTable, ma per la tabella Voti di teacher/student: carica i primi
// 20 voti e ne carica altri 20 ogni volta che si scrolla vicino al fondo
// della tabella, mostrando uno spinner durante il caricamento.
async function loadPaginatedGradesTable(ids, title, description, tableHeadHtml, renderBody) {
    document.getElementById(ids.title).innerText = title;
    document.getElementById(ids.desc).innerText = description;
    document.getElementById(ids.head).innerHTML = tableHeadHtml;

    const tbody = document.getElementById(ids.body);
    const container = tbody.closest(".table-responsive");
    tbody.innerHTML = "";

    let page = 0;
    let total = 0;
    let loadedCount = 0;
    let loading = false;

    function showSpinnerRow() {
        const row = document.createElement("tr");
        row.id = "grades-pagination-spinner-row";
        row.innerHTML = `
            <td colspan="100%" class="text-center" style="padding: 15px 0;">
                <div class="grades-spinner mx-auto" style="width: 24px; height: 24px; border-width: 3px;"></div>
            </td>
        `;
        tbody.appendChild(row);
    }

    function hideSpinnerRow() {
        const row = document.getElementById("grades-pagination-spinner-row");
        if (row) row.remove();
    }

    async function loadNextPage() {
        if (loading || (page > 0 && loadedCount >= total)) return;
        loading = true;
        showSpinnerRow();

        try {
            const nextPage = page + 1;
            const data = await sendTokenForData(
                `${API_BASE}/Grade/paged?pageNumber=${nextPage}&pageSize=${PAGINATED_GRADES_PAGE_SIZE}`
            );
            page = nextPage;
            total = data.totalCount;
            loadedCount += data.items.length;
            hideSpinnerRow();
            renderBody(tbody, data.items);
        } finally {
            loading = false;
            hideSpinnerRow();
        }
    }

    if (container) {
        container.addEventListener("scroll", () => {
            const nearBottom = container.scrollTop + container.clientHeight >= container.scrollHeight - 100;
            if (nearBottom) loadNextPage();
        });
    }

    await loadNextPage();
}

const entityFieldsConfig = {
    Users: [
        { key: "email",     domSuffix: "email",      inputId: "modal-email" },
        { key: "firstName", domSuffix: "first-name",  inputId: "modal-first-name" },
        { key: "lastName",  domSuffix: "last-name",   inputId: "modal-last-name" }
    ],
    Student: [
        { key: "firstName", domSuffix: "first-name", inputId: "modal-first-name" },
        { key: "lastName",  domSuffix: "last-name",  inputId: "modal-last-name" }
    ],
    Teacher: [
        { key: "firstName", domSuffix: "first-name", inputId: "modal-first-name" },
        { key: "lastName",  domSuffix: "last-name",  inputId: "modal-last-name" }
    ],
    Grade: [
        { key: "subjectId", domSuffix: "subject", inputId: "modal-subject", type: "select" },
        { key: "value",     domSuffix: "grade",   inputId: "modal-grade" },
        { key: "date",      domSuffix: "date",    inputId: "modal-date" }
    ],
    Subject: [
        { key: "name",      domSuffix: "subject-name",    inputId: "modal-subject-name" },
        { key: "teacherId", domSuffix: "subject-teacher", inputId: "modal-subject-teacher", type: "teacherSelect", readAttr: "teacherId" }
    ]
};

// Popola una <select> con le materie prese dal db,
// selezionando l'opzione il cui nome combacia con selectedName
async function populateSubjectSelect(selectId, selectedName) {
    const select = document.getElementById(selectId);
    select.innerHTML = "";
    const userData = await getUserData();
    try {
        const subjects = await sendTokenForData(`${API_BASE}/Subject/byteacher/${userData.teacherId}`);
        subjects.forEach(subject => {
            const option = document.createElement("option");
            option.value = subject.id;
            option.textContent = subject.name;
            select.appendChild(option);
        });

        // selectedName è il NOME (letto dalla tabella), va cercato l'id corrispondente
        const matching = subjects.find(s => s.name === selectedName);
        if (matching) {
            select.value = matching.id;
        }
    } catch (e) {
        console.error("Errore nel recupero delle materie:", e);
    }
}

// Popola una <select> con tutti gli insegnanti presenti nel db,
// selezionando l'opzione il cui id combacia con selectedTeacherId
async function populateTeacherSelect(selectId, selectedTeacherId) {
    const select = document.getElementById(selectId);
    select.innerHTML = "";
    try {
        const teachers = await sendTokenForData(`${API_BASE}/Teacher`);
        teachers.forEach(teacher => {
            const option = document.createElement("option");
            option.value = teacher.id;
            option.textContent = `${teacher.firstName} ${teacher.lastName}`;
            select.appendChild(option);
        });

        if (selectedTeacherId) {
            select.value = selectedTeacherId;
        }
    } catch (e) {
        console.error("Errore nel recupero degli insegnanti:", e);
    }
}

async function showModal(type, id) {
    const config = entityFieldsConfig[type];
    if (!config) {
        console.error(`Tipo di entità non supportato: ${type}`);
        return;
    }

    document.getElementById("student-teacher-fields").style.display =
        (type === "Student" || type === "Teacher" || type === "Users") ? "block" : "none";
    document.getElementById("teacher-fields").style.display =
        type === "Teacher" ? "block" : "none";
    document.getElementById("grade-fields").style.display =
        type === "Grade" ? "block" : "none";
    document.getElementById("user-fields").style.display =
        type === "Users" ? "block" : "none";
    document.getElementById("subject-fields").style.display =
        type === "Subject" ? "block" : "none";
    if (type === "Subject") setSubjectNameError(false);

    for (const field of config) {
        if (field.skipRead) {
            document.getElementById(field.inputId).value = "";
            continue;
        }

        const sourceElId = `${field.domSuffix}-${id}`;
        const sourceEl = document.getElementById(sourceElId);
        const value = field.readAttr
            ? (sourceEl ? sourceEl.dataset[field.readAttr] : "")
            : (sourceEl ? sourceEl.textContent.trim() : "");

        if (field.type === "select") {
            await populateSubjectSelect(field.inputId, value);
        } else if (field.type === "teacherSelect") {
            await populateTeacherSelect(field.inputId, value);
        } else {
            document.getElementById(field.inputId).value = value;
        }
    }

    document.getElementById("save-modal-btn").onclick = () => updateEntity(type, id);
    new bootstrap.Modal(document.getElementById("edit-entity-modal")).show();
}

async function updateEntity(type, id) {
    const config = entityFieldsConfig[type];
    if (!config) {
        console.error(`Tipo di entità non supportato: ${type}`);
        return;
    }

    if (type === "Subject") {
        const nameInput = document.getElementById("modal-subject-name");
        if (!nameInput.value.trim()) {
            setSubjectNameError(true);
            return;
        }
        setSubjectNameError(false);
    }

    const data = {};
    config.forEach(field => {
        const val = document.getElementById(field.inputId).value;
        data[field.key] = val || null;
    });

    try {
        const res = await sendApiRequest(`${API_BASE}/${type}/update/${id}`, "PUT", data);
        bootstrap.Modal.getInstance(document.getElementById("edit-entity-modal")).hide();
        document.activeElement.blur();
        loadPage();
        return res;
    } catch (e) {
        console.error(e);
    }
}

async function deleteUser(id) {
    if (!confirm("Sei sicuro di voler eliminare questo utente?")) return;

    try {
        await sendApiRequest(`${API_BASE}/Users/${id}`, "DELETE");
        loadPage();
    } catch (e) {
        console.error(e);
        alert("Errore durante l'eliminazione dell'utente.");
    }
}

async function deleteStudent(id) {
    if (!confirm("Sei sicuro di voler eliminare questo allievo?")) return;

    try {
        await sendApiRequest(`${API_BASE}/Student/${id}`, "DELETE");
        loadPage();
    } catch (e) {
        console.error(e);
        alert(extractApiErrorMessage(e, "Errore durante l'eliminazione dell'allievo."));
    }
}

async function deleteTeacher(id) {
    if (!confirm("Sei sicuro di voler eliminare questo insegnante?")) return;

    try {
        await sendApiRequest(`${API_BASE}/Teacher/${id}`, "DELETE");
        loadPage();
    } catch (e) {
        console.error(e);
        alert(extractApiErrorMessage(e, "Errore durante l'eliminazione dell'insegnante."));
    }
}

async function deleteSubject(id) {
    if (!confirm("Sei sicuro di voler eliminare questa materia?")) return;

    try {
        await sendApiRequest(`${API_BASE}/Subject/${id}`, "DELETE");
        loadPage();
    } catch (e) {
        console.error(e);
        alert(extractApiErrorMessage(e, "Errore durante l'eliminazione della materia."));
    }
}

async function loadPage() {
    const bottomTableRow = document.getElementById("bottom-table-row");
    const userBadgeRole = document.getElementById("user-badge-role");
    const userData = await getUserData();
    userBadgeRole.innerText = capitalize(userData.role) ?? "ERROR";
    populateUserBadge(userData);
    applyNavVisibility(userData);
    wireHeaderSearch();

    switch (userData.role) {
        case "admin":
            await loadCardsData(userData);
            await loadChartData();
            loadTable(
                {
                    title: "top-table-title",
                    desc: "top-table-desc",
                    head: "top-table-head",
                    body: "top-table-body"
                },
                "Utenti",
                "Elimina e modifica utenti.",
                `<tr>
                    <th><h6>Email</h6></th>
                    <th><h6>Ruolo</h6></th>
                    <th><h6>Nome</h6></th>
                    <th><h6>Cognome</h6></th>
                    <th><h6>Action</h6></th>
                </tr>`,
                "/api/Users",
                renderUsersBodyAdmin
            );
            loadTable(
                {
                    title: "center-left-table-title",
                    desc: "center-left-table-desc",
                    head: "center-left-table-head",
                    body: "center-left-table-body"
                },
                "Allievi",
                "Elenco di tutti gli allievi.",
                `<tr>
                    <th><h6>Id</h6></th>
                    <th><h6>Nome</h6></th>
                    <th><h6>Cognome</h6></th>
                    <th><h6>Action</h6></th>
                </tr>`,
                "/api/Student",
                renderStudentsBodyAdmin
            );
            loadTable(
                {
                    title: "center-right-table-title",
                    desc: "center-right-table-desc",
                    head: "center-right-table-head",
                    body: "center-right-table-body"
                },
                "Docenti",
                "Elenco di tutti i docenti.",
                `<tr>
                    <th><h6>Id</h6></th>
                    <th><h6>Nome</h6></th>
                    <th><h6>Cognome</h6></th>
                    <th><h6>Action</h6></th>
                </tr>`,
                "/api/Teacher",
                renderTeachersBodyAdmin
            );
            loadTable(
                {
                    title: "bottom-table-title",
                    desc: "bottom-table-desc",
                    head: "bottom-table-head",
                    body: "bottom-table-body"
                },
                "Materie",
                "Elimina e modifica materie.",
                `<tr>
                    <th><h6>Nome</h6></th>
                    <th><h6>Insegnante</h6></th>
                    <th><h6>Action</h6></th>
                </tr>`,
                "/api/Subject",
                renderSubjectsBodyAdmin
            );
        break;

        case "teacher":
            if (bottomTableRow) bottomTableRow.style.display = "none";
            loadCardsData(userData);
            loadChartData();
            loadPaginatedGradesTable(
                {
                    title: "top-table-title",
                    desc: "top-table-desc",
                    head: "top-table-head",
                    body: "top-table-body"
                },
                "Voti",
                "Elenco di tutti i voti da te inseriti.",
                `<tr>
                    <th><h6>Materia</h6></th>
                    <th><h6>Voto</h6></th>
                    <th><h6>Allievo</h6></th>
                    <th><h6>Data</h6></th>
                    <th><h6>Action</h6></th>
                </tr>`,
                renderGradesBodyTeacher
            );
            loadTable(
                {
                    title: "center-left-table-title",
                    desc: "center-left-table-desc",
                    head: "center-left-table-head",
                    body: "center-left-table-body"
                },
                "Materie",
                "Elenco di tutte le materie da te insegnate.",
                `<tr>
                    <th><h6>Materia</h6></th>
                </tr>`,
                "/api/Subject/byteacher/" + userData.teacherId,
                renderSubjectsBodyTeacher
            );
            loadTable(
                {
                    title: "center-right-table-title",
                    desc: "center-right-table-desc",
                    head: "center-right-table-head",
                    body: "center-right-table-body"
                },
                "Allievi",
                "Elenco di tutti gli allievi che hanno voti inseriti da te.",
                `<tr>
                    <th><h6>Nome</h6></th>
                    <th><h6>Cognome</h6></th>
                </tr>`,
                "/api/Student",
                renderStudentsBodyTeacher
            );
        break;

        case "student":
            if (bottomTableRow) bottomTableRow.style.display = "none";
            await loadCardsData(userData);
            await loadChartData();
            loadPaginatedGradesTable(
                {
                    title: "top-table-title",
                    desc: "top-table-desc",
                    head: "top-table-head",
                    body: "top-table-body"
                },
                "Voti",
                "Elenco di tutti i tuoi voti.",
                `<tr>
                    <th><h6>Materia</h6></th>
                    <th><h6>Voto</h6></th>
                    <th><h6>Data</h6></th>
                </tr>`,
                renderGradesBodyStudent
            );
            loadTable(
                {
                    title: "center-left-table-title",
                    desc: "center-left-table-desc",
                    head: "center-left-table-head",
                    body: "center-left-table-body"
                },
                "Materie",
                "Elenco di tutte le materie.",
                `<tr>
                    <th><h6>Name</h6></th>
                    <th><h6>Insegnante</h6></th>
                </tr>`,
                "/api/Subject",
                renderSubjectsBodyStudent
            );
            loadTable(
                {
                    title: "center-right-table-title",
                    desc: "center-right-table-desc",
                    head: "center-right-table-head",
                    body: "center-right-table-body"
                },
                "Media per Materia",
                "Elenco della media dei voti per materia.",
                `<tr>
                    <th><h6>Materia</h6></th>
                    <th><h6>Media</h6></th>
                </tr>`,
                "/api/Subject",
                renderSubjectAveragesBodyStudent
            );
        break;
    }
}

loadPage();