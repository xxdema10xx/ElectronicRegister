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

    switch (role) {
        case "admin":
            formatCardsData(
                [
                    `${API_BASE}/api/Student/count`,
                    `${API_BASE}/api/Teacher/count`,
                    `${API_BASE}/api/Grade/count`,
                    `${API_BASE}/api/Users/count`
                ],
                ["Allievi", "Insegnanti", "Voti Totali", "Utenti"]
            );
            break;
        case "teacher":
            formatCardsData(
                [
                    `${API_BASE}/api/Subject/count`,
                    `${API_BASE}/api/Student/count`,
                    `${API_BASE}/api/Grade/count`
                ],
                ["Materie", "Alunni", "Voti"]
            );
            const card4 = document.getElementById("card-4");
            if (card4) card4.remove();
            break;
        case "student":
            try {
                const subjectsCount = await sendTokenForData(`${API_BASE}/api/Subject/count`);
                const studentGrades = await sendTokenForData(`${API_BASE}/api/Grade`);
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
    const grades = await sendTokenForData(`${API_BASE}/api/Grade`);

    // raggruppa i voti per mese e calcola la media
    const monthlyData = Array(12).fill(0);
    const monthlyCounts = Array(12).fill(0);

    grades.forEach(g => {
        const month = new Date(g.date).getMonth(); // 0-11
        monthlyData[month] += g.value;
        monthlyCounts[month]++;
    });

    const averages = monthlyData.map((sum, i) => 
        monthlyCounts[i] > 0 ? (sum / monthlyCounts[i]).toFixed(1) : 0
    );
    const yearlyAverage =
        grades.length > 0
        ? (grades.reduce((sum, g) => sum + g.value, 0) / grades.length).toFixed(1)
        : 0;

    document.getElementById("average-yearly-grades").innerText = yearlyAverage;
    chart1.data.datasets[0].data = averages;
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
                            <p style="color: blue;">${user.email}</p>
                        </div>
                    </div>
                </td>
                <td class="min-width">
                    <p style="${user.studentId ? 'color: green;' : 'color: orange;'}">${user.role}</p>
                </td>
                <td class="min-width">
                    <p style="color: #2f2f2f;">${user.studentId ? user.studentFirstName : user.teacherFirstName}</p>
                </td>
                <td class="min-width">
                    <p style="color: #2f2f2f;">${user.studentId ? user.studentLastName : user.teacherLastName}</p>
                </td>
                <td>
                    <div class="action">
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
                <p>${student.firstName}</p>
            </td>
            <td class="min-width">
                <p>${student.lastName}</p>
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
                <p>${teacher.firstName}</p>
            </td>
            <td class="min-width">
                <p>${teacher.lastName}</p>
            </td>
        `;
        tbody.appendChild(row);
    });
}

function renderAdminsBodyAdmin(tbody, adminsData) {
    adminsData
    .filter(admin => admin.role === "admin")
    .forEach(admin => {
        const row = document.createElement("tr");
        row.innerHTML = `
            <td class="min-width">
                <p style="color: blue;">${admin.id}</p>
            </td>
            <td class="min-width">
                <p>${admin.email}</p>
            </td>
            <td class="min-width">
                <p>${admin.role}</p>
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
    const grades = await sendTokenForData(`${API_BASE}/api/Grade`);
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

async function loadTable(ids, title, description, tableHeadHtml, dataUrl, renderBody) {
    document.getElementById(ids.title).innerText = title;
    document.getElementById(ids.desc).innerText = description;
    document.getElementById(ids.head).innerHTML = tableHeadHtml;
    const tbody = document.getElementById(ids.body);
    tbody.innerHTML = "";
    const data = await sendTokenForData(`${API_BASE}${dataUrl}`);
    renderBody(tbody, data);
}

const capitalize = (str = '') => {
    return str.length
        ? str.charAt(0).toUpperCase() + str.slice(1)
        : '';
};

function popolateFields(ids, values) {
    ids.forEach((id, i) => {
        const element = document.getElementById(id);
        if (element) element.innerText = values[i];
    });
}

function showModal(type, id) {
    const firstName = document.getElementById(`first-name-${id}`).textContent;
    const lastName = document.getElementById(`last-name-${id}`).textContent;
    document.getElementById("modal-first-name").value = firstName;
    document.getElementById("modal-last-name").value = lastName;
    document.getElementById("save-modal-btn").onclick = () => updateEntity(type, id);
    new bootstrap.Modal(document.getElementById("edit-entity-modal")).show();
}

async function updateEntity(type, id) {
    const data = {
        firstName: document.getElementById("modal-first-name").value || null,
        lastName: document.getElementById("modal-last-name").value || null
    };
    try{
        const res = await sendApiRequest(`${API_BASE}/api/${type}/update/${id}`, "PUT", data);
        bootstrap.Modal.getInstance(document.getElementById("edit-entity-modal")).hide();
        document.activeElement.blur();
        if (type === "Student") await loadStudentsTable();
        if (type === "Teacher") await loadTeachersTable();
        return res;
    } catch(e) {
        console.log(e);
        console.log(e.message);
    }
}

async function loadPage() {
    const userBadgeRole = document.getElementById("user-badge-role");
    const userData = await getUserData();
    userBadgeRole.innerText = capitalize(userData.role) ?? "ERROR";
    // Determina il fullName in base al tipo di utente
    let fullName = "Admin";
    if (userData.studentId) {
        fullName = `${capitalize(userData.studentFirstName)} ${capitalize(userData.studentLastName)}`;
    } else if (userData.teacherId) {
        fullName = `${capitalize(userData.teacherFirstName)} ${capitalize(userData.teacherLastName)}`;
    }

    popolateFields(
        ["dropdown-badge-name", "dropdown-badge-email", "user-badge-name"],
        [fullName, userData.email, fullName]
    );

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
                "Amministratori",
                "Elenco di tutti gli amministratori.",
                `<tr>
                    <th><h6>Id</h6></th>
                    <th><h6>Email</h6></th>
                    <th><h6>Ruolo</h6></th>
                </tr>`,
                "/api/Users",
                renderAdminsBodyAdmin
            );
        break;

        case "teacher":
            loadCardsData(userData);
            loadChartData();
        break;

        case "student":
            const bottomTableRow = document.getElementById("bottom-table-row");
            if (bottomTableRow) bottomTableRow.style.display = "none";
            await loadCardsData(userData);
            await loadChartData();
            loadTable(
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
                "/api/Grade",
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