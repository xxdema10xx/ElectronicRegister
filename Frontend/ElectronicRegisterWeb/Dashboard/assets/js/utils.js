const capitalize = (str = '') => {
    return str.length
        ? str.charAt(0).toUpperCase() + str.slice(1)
        : '';
};

// sendApiRequest lancia errori come "API error 400: <messaggio dal backend>";
// qui estraiamo solo il messaggio per mostrarlo all'utente, con un fallback
// generico se non riconosciamo il formato.
function extractApiErrorMessage(error, fallback) {
    const match = /^API error \d+: (.+)$/s.exec(error?.message || "");
    return match ? match[1] : fallback;
}

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

// Nasconde dalla sidebar le voci non pertinenti al ruolo: gli studenti non
// devono vedere "Gestione" né "Voti", gli insegnanti non devono vedere "Voti".
function applyNavVisibility(userData) {
    const gestioneLink = document.querySelector('.sidebar-nav a[href="gestione.html"]');
    const votiLink = document.querySelector('.sidebar-nav a[href="grades.html"]');

    if (userData.role === "student") {
        if (gestioneLink) gestioneLink.closest("li").style.display = "none";
        if (votiLink) votiLink.closest("li").style.display = "none";
    } else if (userData.role === "teacher") {
        if (votiLink) votiLink.closest("li").style.display = "none";
    }
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