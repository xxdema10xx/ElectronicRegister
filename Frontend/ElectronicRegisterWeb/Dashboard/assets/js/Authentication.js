const API_BASE = CONFIG.API_BASE

async function checkToken() {
    try {
        const token = localStorage.getItem("jwt");
        if(!token || token.trim().length === 0) {
            window.location.href = '../Authentication/login.html';
            return;
        }
        const res = await fetch(`${API_BASE}/api/Auth/me`, {
                headers: {
                Authorization: `Bearer ${token}`
            }
        });

        if(res.status === 401 || res.status === 403 || res.status === 500) {
            localStorage.removeItem("jwt");
            window.location.href = '../Authentication/login.html';
            return;
        }

        if (res.status === 200) return;
    } catch (e) {
        console.log(e);
        console.log(e.message);
        window.location.href = '../Authentication/login.html';
    }
}

function logout() {
    localStorage.removeItem("jwt");
    window.location.href = "../Authentication/login.html";
}

checkToken();