const API_BASE = CONFIG.API_BASE;

// ── Elements ────────────────────────────────────────────
const emailInput  = document.getElementById('email');
const passInput   = document.getElementById('password');
const loginBtn    = document.getElementById('loginBtn');
const msLoginBtn  = document.getElementById('ms-login-btn');
const errorMsg    = document.getElementById('errorMsg');
const errorText   = document.getElementById('errorText');
const successMsg  = document.getElementById('successMsg');

// ── Helpers ─────────────────────────────────────────────
function setLoading(btn, on) {
  btn.disabled = on;
  btn.classList.toggle('loading', on);
}

function showError(msg) {
  errorText.textContent = msg;
  errorMsg.classList.add('show');
  emailInput.classList.add('error');
  passInput.classList.add('error');
}

function clearError() {
  errorMsg.classList.remove('show');
  emailInput.classList.remove('error');
  passInput.classList.remove('error');
}

function showSuccess() {
  successMsg.classList.add('show');
}

// ── Login ────────────────────────────────────────────────
async function doLogin() {
  clearError();

  const email    = emailInput.value.trim();
  const password = passInput.value;

  if (!email || !password) {
    showError('Compila tutti i campi.');
    return;
  }

  setLoading(loginBtn, true);

  try {
    const res = await fetch(`${API_BASE}/Auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    });

    if (res.ok) {
      const data = await res.json();
      const token = data.token;

      localStorage.setItem('jwt', token);
      showSuccess();

      setTimeout(() => window.location.href = '../Dashboard/index.html', 1500);

    } else if (res.status === 401) {
      showError('Email o password non corretti.');
    } else {
      const txt = await res.text();
      showError(txt || `Errore ${res.status}`);
    }

  } catch (err) {
    showError('Impossibile raggiungere il server. Verifica che l\'API sia in esecuzione.');
  } finally {
    setLoading(loginBtn, false);
  }
}

// ── Events ───────────────────────────────────────────────
loginBtn.addEventListener('click', doLogin);

msLoginBtn.addEventListener('click', () => {
  setLoading(msLoginBtn, true);
});

[emailInput, passInput].forEach(el => {
  el.addEventListener('keydown', e => {
    if (e.key === 'Enter') doLogin();
  });

  el.addEventListener('input', clearError);
});