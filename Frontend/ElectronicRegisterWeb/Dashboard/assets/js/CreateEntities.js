const GRADE_SELECT_VALUES = Array.from({ length: 21 }, (_, i) => (i * 0.5).toFixed(1));

function todayIsoDate() {
    return new Date().toISOString().slice(0, 10);
}

function buildGradeOptionsHtml() {
    let html = `<option value="" selected disabled>Seleziona voto</option>`;
    GRADE_SELECT_VALUES.forEach(v => {
        html += `<option value="${v}">${v}</option>`;
    });
    return html;
}

function showFeedback(elementId, message, isError) {
    const el = document.getElementById(elementId);
    el.textContent = message;
    el.style.color = isError ? "red" : "green";
}

// ======= ADMIN: create user =======
async function createUser() {
    const role = document.getElementById("new-user-role").value;
    const firstName = document.getElementById("new-user-firstname").value.trim();
    const lastName = document.getElementById("new-user-lastname").value.trim();
    const email = document.getElementById("new-user-email").value.trim();
    const password = document.getElementById("new-user-password").value;

    if (!firstName || !lastName || !email || !password) {
        showFeedback("create-user-feedback", "Compila tutti i campi.", true);
        return;
    }

    try {
        await sendApiRequest(`${API_BASE}/api/Auth/RegisterForAdmin`, "POST", {
            email, password, role, firstName, lastName
        });
        showFeedback("create-user-feedback", "Utente creato con successo.", false);
        document.getElementById("new-user-firstname").value = "";
        document.getElementById("new-user-lastname").value = "";
        document.getElementById("new-user-email").value = "";
        document.getElementById("new-user-password").value = "";
    } catch (e) {
        console.error(e);
        showFeedback("create-user-feedback", "Errore durante la creazione dell'utente.", true);
    }
}

// ======= ADMIN: create subject =======
async function createSubject() {
    const name = document.getElementById("new-subject-name").value.trim();
    const teacherId = document.getElementById("new-subject-teacher").value;

    if (!name || !teacherId) {
        showFeedback("create-subject-feedback", "Compila tutti i campi.", true);
        return;
    }

    try {
        await sendApiRequest(`${API_BASE}/api/Subject`, "POST", { name, teacherId });
        showFeedback("create-subject-feedback", "Materia creata con successo.", false);
        document.getElementById("new-subject-name").value = "";
    } catch (e) {
        console.error(e);
        showFeedback("create-subject-feedback", "Errore durante la creazione della materia.", true);
    }
}

// ======= ADMIN: create grade =======
async function createGrade() {
    const studentId = document.getElementById("new-grade-student").value;
    const subjectId = document.getElementById("new-grade-subject").value;
    const value = document.getElementById("new-grade-value").value;
    const date = document.getElementById("new-grade-date").value;

    if (!studentId || !subjectId || !value || !date) {
        showFeedback("create-grade-feedback", "Compila tutti i campi.", true);
        return;
    }

    try {
        await sendApiRequest(`${API_BASE}/api/Grade`, "POST", {
            studentId, subjectId, value: parseFloat(value), date
        });
        showFeedback("create-grade-feedback", "Voto creato con successo.", false);
        document.getElementById("new-grade-value").value = "";
    } catch (e) {
        console.error(e);
        showFeedback("create-grade-feedback", "Errore durante la creazione del voto.", true);
    }
}

async function initAdminSection() {
    document.getElementById("admin-section").style.display = "block";
    document.getElementById("new-grade-date").value = todayIsoDate();

    const teachers = await sendTokenForData(`${API_BASE}/api/Teacher`).catch(() => []);
    document.getElementById("new-subject-teacher").innerHTML = teachers
        .map(t => `<option value="${t.id}">${t.firstName} ${t.lastName}</option>`)
        .join("");

    const students = await sendTokenForData(`${API_BASE}/api/Student`).catch(() => []);
    document.getElementById("new-grade-student").innerHTML = students
        .map(s => `<option value="${s.id}">${s.firstName} ${s.lastName}</option>`)
        .join("");

    const subjects = await sendTokenForData(`${API_BASE}/api/Subject`).catch(() => []);
    document.getElementById("new-grade-subject").innerHTML = subjects
        .map(s => `<option value="${s.id}">${s.name}</option>`)
        .join("");

    document.getElementById("create-user-btn").addEventListener("click", createUser);
    document.getElementById("create-subject-btn").addEventListener("click", createSubject);
    document.getElementById("create-grade-btn").addEventListener("click", createGrade);
}

// ======= TEACHER: assign grades to students =======
async function saveTeacherGrade(studentId, subjectId) {
    const value = document.getElementById(`grade-select-${studentId}`).value;
    const date = document.getElementById("teacher-grade-date").value;

    if (!value) {
        alert("Seleziona un voto.");
        return;
    }

    try {
        await sendApiRequest(`${API_BASE}/api/Grade`, "POST", {
            studentId, subjectId, value: parseFloat(value), date
        });
        alert("Voto salvato con successo.");
    } catch (e) {
        console.error(e);
        alert("Errore durante il salvataggio del voto.");
    }
}

async function initTeacherSection(userData) {
    document.getElementById("teacher-section").style.display = "block";
    document.getElementById("teacher-grade-date").value = todayIsoDate();

    const tbody = document.getElementById("teacher-students-body");
    const subjects = await sendTokenForData(`${API_BASE}/api/Subject/byteacher/${userData.teacherId}`).catch(() => []);

    if (subjects.length === 0) {
        tbody.innerHTML = `<tr><td colspan="3">Nessuna materia assegnata.</td></tr>`;
        return;
    }

    let selectedSubjectId = subjects[0].id;

    if (subjects.length > 1) {
        document.getElementById("teacher-subject-wrapper").style.display = "block";
        const subjectSelect = document.getElementById("teacher-subject-select");
        subjectSelect.innerHTML = subjects.map(s => `<option value="${s.id}">${s.name}</option>`).join("");
        selectedSubjectId = subjectSelect.value;
        subjectSelect.addEventListener("change", () => {
            selectedSubjectId = subjectSelect.value;
        });
    }

    const students = await sendTokenForData(`${API_BASE}/api/Student`).catch(() => []);
    tbody.innerHTML = "";

    students.forEach(student => {
        const row = document.createElement("tr");
        row.innerHTML = `
            <td><p>${student.firstName} ${student.lastName}</p></td>
            <td>
                <div class="select-style-1">
                    <div class="select-position">
                        <select id="grade-select-${student.id}">${buildGradeOptionsHtml()}</select>
                    </div>
                </div>
            </td>
            <td><button type="button" class="main-btn primary-btn btn-hover">Salva</button></td>
        `;
        row.querySelector("button").addEventListener("click", () => saveTeacherGrade(student.id, selectedSubjectId));
        tbody.appendChild(row);
    });
}

async function initManagePage() {
    const userData = await getUserData();
    if (!userData) return;

    populateUserBadge(userData);
    const userBadgeRole = document.getElementById("user-badge-role");
    if (userBadgeRole) userBadgeRole.innerText = capitalize(userData.role);
    applyNavVisibility(userData);
    wireHeaderSearch();

    if (userData.role === "admin") {
        await initAdminSection();
    } else if (userData.role === "teacher") {
        await initTeacherSection(userData);
    } else {
        window.location.href = "index.html";
    }
}

initManagePage();
