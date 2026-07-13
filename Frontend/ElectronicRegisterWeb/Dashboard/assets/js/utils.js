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

// Nome e cognome per studenti/insegnanti; per l'admin (che non ha un nome
// registrato) mostra la parte dell'email prima della @.
function getDisplayName(userData) {
    if (userData.studentId) {
        return `${capitalize(userData.studentFirstName)} ${capitalize(userData.studentLastName)}`;
    }
    if (userData.teacherId) {
        return `${capitalize(userData.teacherFirstName)} ${capitalize(userData.teacherLastName)}`;
    }
    return userData.email.split("@")[0];
}

function populateUserBadge(userData) {
    const displayName = getDisplayName(userData);
    const userBadgeName = document.getElementById("user-badge-name");
    const dropdownBadgeName = document.getElementById("dropdown-badge-name");
    const dropdownBadgeEmail = document.getElementById("dropdown-badge-email");
    if (userBadgeName) userBadgeName.innerText = displayName;
    if (dropdownBadgeName) dropdownBadgeName.innerText = displayName;
    if (dropdownBadgeEmail) dropdownBadgeEmail.innerText = userData.email;
}

// Filtra le righe di tutte le tabelle nel contenuto principale in base al
// testo digitato nella barra di ricerca dell'header. Generico: non richiede
// conoscere quali tabelle sono presenti nella pagina corrente.
function wireHeaderSearch() {
    const input = document.querySelector(".header-search input");
    if (!input) return;

    const form = input.closest("form");
    if (form) form.addEventListener("submit", e => e.preventDefault());

    input.addEventListener("input", () => {
        const query = input.value.trim().toLowerCase();
        document.querySelectorAll(".main-wrapper table tbody").forEach(tbody => {
            Array.from(tbody.rows).forEach(row => {
                row.style.display = !query || row.textContent.toLowerCase().includes(query) ? "" : "none";
            });
        });
    });
}