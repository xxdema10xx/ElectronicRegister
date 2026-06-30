//checkToken() viene chiamato in assets/js/main.js all caricamento della pagina



async function loadCardsData(userData) {
    const role = userData.role;
    const studentsAmount = document.getElementById("card-1-data");
    const teachersAmount = document.getElementById("card-2-data");
    const gradesAmount = document.getElementById("card-3-data");
    const usersAmount = document.getElementById("card-4-data");
    const card1Title = document.getElementById("card-1-title");
    const card2Title = document.getElementById("card-2-title");
    const card3Title = document.getElementById("card-3-title");
    const card4Title = document.getElementById("card-4-title");

    switch (role) {
        case "admin":
            try {
                const studentsCount = await sendTokenForData(`${API_BASE}/api/Student/count`);
                const teachersCount = await sendTokenForData(`${API_BASE}/api/Teacher/count`);
                const usersCount = await sendTokenForData(`${API_BASE}/api/Users/count`);
                const gradesCount = await sendTokenForData(`${API_BASE}/api/Grade/count`);

                studentsAmount.innerText = studentsCount;
                teachersAmount.innerText = teachersCount;
                gradesAmount.innerText = gradesCount;
                usersAmount.innerText = usersCount;
                card1Title.innerText = "Allievi";
                card2Title.innerText = "Insegnanti";
                card3Title.innerText = "Voti Totali";
                card4Title.innerText = "Utenti";

            } catch (err) {
                console.error(err);
                studentsAmount.innerText = "Errore";
                teachersAmount.innerText = "Errore";
                gradesAmount.innerText = "Errore";
                usersAmount.innerText = "Errore";
            }
            break;
        case "teacher":

            break;
        case "student":
            
            break;
        default:
            break;
    }

    try {

        const studentsCount = await sendTokenForData(`${API_BASE}/api/Student/count`);
        const teachersCount = await sendTokenForData(`${API_BASE}/api/Teacher/count`);
        const usersCount = await sendTokenForData(`${API_BASE}/api/Users/count`);
        const gradesCount = await sendTokenForData(`${API_BASE}/api/Grade/count`);

        studentsAmount.innerText = studentsCount;
        teachersAmount.innerText = teachersCount;
        gradesAmount.innerText = gradesCount;
        usersAmount.innerText = usersCount;

    } catch (err) {
        console.error(err);
        studentsAmount.innerText = "Errore";
        teachersAmount.innerText = "Errore";
        gradesAmount.innerText = "Errore";
        usersAmount.innerText = "Errore";
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
        monthlyCounts[i] > 0 ? (sum / monthlyCounts[i]).toFixed(2) : 0
    );
    const yearlyAverage =
        grades.length > 0
        ? (grades.reduce((sum, g) => sum + g.value, 0) / grades.length).toFixed(2)
        : 0;

    document.getElementById("average-yearly-grades").innerText = yearlyAverage;
    chart1.data.datasets[0].data = averages;
    chart1.update();
}

async function loadTeachersTable() {
    const tbody = document.getElementById("active-teachers-tbody");
    const studentsData = await sendTokenForData(`${API_BASE}/api/Teacher`);
    tbody.innerHTML = "";
    studentsData.forEach(teacher => {
        const row = document.createElement("tr");
        row.innerHTML = `
        <td>
            <p style="color: blue;">${teacher.id}</p>
        </td>
        <td>
            <p id="first-name-${teacher.id}">${teacher.firstName}</p>
        </td>
        <td>
            <p id="last-name-${teacher.id}">${teacher.lastName}</p>
        </td>
         <td>
            <div class="action">
            <button class="text-success" data-id="${teacher.id}" onclick="showModal('Teacher', this.dataset.id)">
                <i class="lni lni-pencil-alt"></i>
            </button>
            <button class="text-danger" data-id="${teacher.id}" onclick="deleteStudent(this.dataset.id)">
                <i class="lni lni-trash-can"></i>
            </button>
            </div>
        </td>
        `;
        tbody.appendChild(row);
    });
}

async function loadStudentsTable() {
    const tbody = document.getElementById("active-students-tbody");
    const studentsData = await sendTokenForData(`${API_BASE}/api/Student`);
    tbody.innerHTML = "";
    studentsData.forEach(student => {
        const row = document.createElement("tr");
        row.innerHTML = `
        <td>
            <p style="color: blue;">${student.id}</p>
        </td>
        <td>
            <p id="first-name-${student.id}">${student.firstName}</p>
        </td>
        <td>
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

async function loadUsersTable() {
    const tbody = document.getElementById("users-table-body");

    const usersData = await sendTokenForData(`${API_BASE}/api/Users`);

    const roleOrder = {
        teacher: 1,
        student: 2
    };

    tbody.innerHTML = "";

    usersData
        .filter(user => user.role !== "admin")
        .sort((a, b) => roleOrder[a.role] - roleOrder[b.role])
        .forEach(user => {
        
        if(user.role !== "admin") {

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
        }
    });
}

async function loadAdminsTable() {
    const tbody = document.getElementById("admins-tbody");

    const usersData = await sendTokenForData(`${API_BASE}/api/Users`);

    tbody.innerHTML = "";

    usersData
        .filter(user => user.role == "admin")
        .forEach(user => {
        const row = document.createElement("tr");
        row.innerHTML = `
        <tr>
            <td class="min-width">
            <p style="color: blue;">${user.id}</p>
            <td class="min-width">
            <p>${user.email}</p>
            </td>
        </tr>`;

        tbody.appendChild(row);
    });
}

const capitalize = (str = '') => {
    return str.length
        ? str.charAt(0).toUpperCase() + str.slice(1)
        : '';
};

function populateFields(ids, values) {
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
    userBadgeRole.innerText =  userData.role ?  userData.role : "ERROR"
    let fullName = "";
    if(userData.studentId) { 
        fullName = `${capitalize(userData.studentFirstName)} ${capitalize(userData.studentLastName)}`;
        populateFields(
            ["dropdown-badge-name", "dropdown-badge-email", "user-badge-name"],
            [fullName, userData.email, fullName]
        );
    }
    else if(userData.teacherId) {
        fullName = `${capitalize(userData.teacherFirstName)} ${capitalize(userData.teacherLastName)}`;
        populateFields(
            ["dropdown-badge-name", "dropdown-badge-email", "user-badge-name"],
            [fullName, userData.email, fullName]
        );
    }
    else if(userData.role === "admin") {
        fullName = "Admin"
        populateFields(
            ["dropdown-badge-name", "dropdown-badge-email", "user-badge-name"],
            [fullName, userData.email, fullName]
        );
    }
    await loadCardsData(userData);
    await loadChartData();
    await loadUsersTable();
    await loadStudentsTable();
    await loadTeachersTable();
    await loadAdminsTable();
}

loadPage();