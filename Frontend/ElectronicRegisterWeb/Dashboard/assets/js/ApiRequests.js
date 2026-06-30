async function sendTokenForData(url) {

    const token = localStorage.getItem("jwt");

    const res = await fetch(url, {
        headers: {
            Authorization: `Bearer ${token}`
        }
    });

    if (!res.ok) {
        throw new Error("API error");
    }

    return await res.json();
}

async function sendApiRequest(url, method, data = null) {

    const token = localStorage.getItem("jwt");

    const options = {
        method,
        headers: {
            "Content-Type": "application/json",
            "Authorization": `Bearer ${token}`
        }
    };

    if (data) {
        options.body = JSON.stringify(data);
    }

    const res = await fetch(url, options);

    if (!res.ok) {
        const errorText = await res.text();
        throw new Error(`API error ${res.status}: ${errorText}`);
    }

    // Gestione 204 No Content
    if (res.status === 204) {
        return null;
    }

    return await res.json();
}

async function getUserData() {
    try{
        const userData = await sendTokenForData(`${API_BASE}/api/Auth/me`);
        return userData;
    } catch(error) {
        console.error(error.message);
    }
}