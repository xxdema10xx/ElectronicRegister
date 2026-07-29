const PAGE_SIZE = 20;

let gradesCache = [];
let currentPage = 0;
let totalCount = 0;
let isLoading = false;

function renderGradesTable(gradesData, append) {
    const tbody = document.getElementById("grades-table-body");
    if (!append) tbody.innerHTML = "";

    gradesData.forEach(grade => {
        const row = document.createElement("tr");
        row.innerHTML = `
            <td><p style="${grade.value >= 6 ? 'color: green;' : 'color: red;'}">${grade.value.toFixed(1)}</p></td>
            <td><p>${grade.student ? `${grade.student.firstName} ${grade.student.lastName}` : ""}</p></td>
            <td><p style="color: blue;">${grade.subjectName}</p></td>
            <td><p>${grade.date}</p></td>
            <td>
                <div class="action">
                    <button class="text-success" data-id="${grade.id}" onclick="showGradeModal(this.dataset.id)">
                        <i class="lni lni-pencil-alt"></i>
                    </button>
                    <button class="text-danger" data-id="${grade.id}" onclick="deleteGrade(this.dataset.id)">
                        <i class="lni lni-trash-can"></i>
                    </button>
                </div>
            </td>
        `;
        tbody.appendChild(row);
    });
}

function currentFilterParams() {
    const subjectId = document.getElementById("filter-subject").value;
    const studentId = document.getElementById("filter-student").value;
    const date = document.getElementById("filter-date").value;

    const params = new URLSearchParams();
    if (subjectId) params.set("subjectId", subjectId);
    if (studentId) params.set("studentId", studentId);
    if (date) params.set("date", date);
    return params;
}

async function loadFilters() {
    const filters = await sendTokenForData(`${API_BASE}/Grade/filters`).catch(() => ({ subjects: [], students: [] }));

    const subjectSelect = document.getElementById("filter-subject");
    subjectSelect.innerHTML = `<option value="">Tutte le materie</option>` +
        filters.subjects.map(s => `<option value="${s.id}">${s.name}</option>`).join("");

    const studentSelect = document.getElementById("filter-student");
    studentSelect.innerHTML = `<option value="">Tutti gli allievi</option>` +
        filters.students.map(s => `<option value="${s.id}">${s.firstName} ${s.lastName}</option>`).join("");
}

async function loadFirstPage() {
    const loadingEl = document.getElementById("grades-loading");
    const tableWrapperEl = document.getElementById("grades-table-wrapper");

    currentPage = 0;
    gradesCache = [];
    totalCount = 0;

    loadingEl.style.display = "block";
    tableWrapperEl.style.display = "none";

    try {
        await loadNextPage();
    } catch (e) {
        console.error(e);
        document.getElementById("grades-table-body").innerHTML =
            `<tr><td colspan="5">Errore nel caricamento dei voti.</td></tr>`;
    } finally {
        loadingEl.style.display = "none";
        tableWrapperEl.style.display = "block";
    }
}

async function loadNextPage() {
    if (isLoading) return;
    if (currentPage > 0 && gradesCache.length >= totalCount) return;

    isLoading = true;
    const loadingMoreEl = document.getElementById("grades-loading-more");
    if (loadingMoreEl) loadingMoreEl.style.display = "block";

    try {
        const nextPage = currentPage + 1;
        const params = currentFilterParams();
        params.set("pageNumber", nextPage);
        params.set("pageSize", PAGE_SIZE);

        const result = await sendTokenForData(`${API_BASE}/Grade/paged?${params.toString()}`);
        currentPage = nextPage;
        totalCount = result.totalCount;
        gradesCache = gradesCache.concat(result.items);
        renderGradesTable(result.items, true);
    } finally {
        isLoading = false;
        if (loadingMoreEl) loadingMoreEl.style.display = "none";
    }
}

function handleScroll() {
    const nearBottom = window.innerHeight + window.scrollY >= document.body.offsetHeight - 300;
    if (nearBottom) loadNextPage();
}

async function showGradeModal(id) {
    const grade = gradesCache.find(g => g.id === id);
    if (!grade) return;

    const subjects = await sendTokenForData(`${API_BASE}/Subject`).catch(() => []);
    const select = document.getElementById("grade-modal-subject");
    select.innerHTML = subjects.map(s => `<option value="${s.id}">${s.name}</option>`).join("");
    select.value = grade.subjectId;

    document.getElementById("grade-modal-value").value = grade.value;
    document.getElementById("grade-modal-date").value = grade.date;

    document.getElementById("grade-modal-save-btn").onclick = () => updateGrade(id);
    new bootstrap.Modal(document.getElementById("edit-grade-modal")).show();
}

async function updateGrade(id) {
    const subjectId = document.getElementById("grade-modal-subject").value;
    const value = document.getElementById("grade-modal-value").value;
    const date = document.getElementById("grade-modal-date").value;

    try {
        await sendApiRequest(`${API_BASE}/Grade/update/${id}`, "PUT", {
            subjectId, value: parseFloat(value), date
        });
        bootstrap.Modal.getInstance(document.getElementById("edit-grade-modal")).hide();
        document.activeElement.blur();
        loadFilters();
        loadFirstPage();
    } catch (e) {
        console.error(e);
        alert("Errore durante la modifica del voto.");
    }
}

async function deleteGrade(id) {
    if (!confirm("Sei sicuro di voler eliminare questo voto?")) return;

    try {
        await sendApiRequest(`${API_BASE}/Grade/${id}`, "DELETE");
        loadFilters();
        loadFirstPage();
    } catch (e) {
        console.error(e);
        alert("Errore durante l'eliminazione del voto.");
    }
}

async function initGradesPage() {
    const userData = await getUserData();
    if (!userData || userData.role !== "admin") {
        window.location.href = "index.html";
        return;
    }

    populateUserBadge(userData);
    const userBadgeRole = document.getElementById("user-badge-role");
    if (userBadgeRole) userBadgeRole.innerText = capitalize(userData.role);
    wireHeaderSearch();

    document.getElementById("filter-subject").addEventListener("change", loadFirstPage);
    document.getElementById("filter-student").addEventListener("change", loadFirstPage);
    document.getElementById("filter-date").addEventListener("change", loadFirstPage);
    window.addEventListener("scroll", handleScroll);

    await loadFilters();
    loadFirstPage();
}

initGradesPage();
