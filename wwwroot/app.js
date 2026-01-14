const toggle = document.getElementById("togglePassword");
const passwordInput = document.querySelector("input[name='password']");
const usernameInput = document.querySelector("input[name='username']");
const notice = document.querySelector(".notice");
const form = document.querySelector(".form");
const dropdown = document.querySelector("[data-dropdown]");
const UNIT_STORAGE_KEY = "visorhr.unit";
const AUTH_STORAGE_KEY = "visorhr.auth";

if (toggle && passwordInput) {
  toggle.addEventListener("click", () => {
    const isHidden = passwordInput.type === "password";
    passwordInput.type = isHidden ? "text" : "password";
    toggle.textContent = isHidden ? "Hide" : "Show";
  });
}

if (dropdown) {
  const trigger = dropdown.querySelector(".dropdown-trigger");
  const list = dropdown.querySelector(".dropdown-list");
  const label = dropdown.querySelector("[data-dropdown-label]");
  const hiddenInput = dropdown.querySelector("input[name='unit']");
  const options = Array.from(list.querySelectorAll("li"));

  const closeDropdown = () => {
    dropdown.classList.remove("open");
    trigger.setAttribute("aria-expanded", "false");
  };

  const setSelection = (value) => {
    options.forEach((item) => {
      item.classList.toggle("is-active", item.getAttribute("data-value") === value);
    });
    if (label) {
      label.textContent = value || "Select a unit";
    }
    if (hiddenInput) {
      hiddenInput.value = value || "";
    }
    if (value) {
      localStorage.setItem(UNIT_STORAGE_KEY, value);
    }
  };

  const storedValue = localStorage.getItem(UNIT_STORAGE_KEY);
  if (storedValue) {
    setSelection(storedValue);
  }

  trigger.addEventListener("click", () => {
    const isOpen = dropdown.classList.toggle("open");
    trigger.setAttribute("aria-expanded", String(isOpen));
  });

  list.addEventListener("pointerdown", (event) => {
    const target = event.target.closest("li");
    if (!target) {
      return;
    }
    event.preventDefault();
    const value = target.getAttribute("data-value") || target.textContent;
    setSelection(value);
    closeDropdown();
    trigger.blur();
  });

  document.addEventListener("click", (event) => {
    if (!dropdown.contains(event.target)) {
      closeDropdown();
    }
  });
}

if (form) {
  form.addEventListener("submit", (event) => {
    event.preventDefault();

    const button = form.querySelector(".primary");
    if (!button) {
      return;
    }

    if (!notice) {
      return;
    }

    notice.className = "notice";
    notice.textContent = "";

    button.textContent = "Signing in...";
    button.disabled = true;

    const payload = {
      unit: form.querySelector("input[name='unit']")?.value,
      username: usernameInput?.value?.trim(),
      password: passwordInput?.value
    };

    fetch("/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    })
      .then(async (response) => {
        const data = await response.json().catch(() => ({}));
        if (response.ok) {
          notice.classList.add("is-success");
          notice.textContent = "Signing in...";
          button.textContent = "Signed in";
          if (data.unit) {
            localStorage.setItem("visorhr.unit", data.unit);
          }
          localStorage.setItem(AUTH_STORAGE_KEY, Date.now().toString());
          const unitValue = data.unit || "";
          const usernameValue = usernameInput?.value?.trim() || "";
          const params = new URLSearchParams({ unit: unitValue, user: usernameValue });
          window.location.href = `/welcome.html?${params.toString()}`;
          return;
        }

        notice.classList.add("is-error");
        notice.textContent = data.message || "Login failed.";
        button.textContent = "Sign in";
        button.disabled = false;
      })
      .catch(() => {
        notice.classList.add("is-error");
        notice.textContent = "Could not reach the server.";
        button.textContent = "Sign in";
        button.disabled = false;
      });
  });
}
