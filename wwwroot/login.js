let registerMode = new URLSearchParams(location.search).get("register") === "1";
const form = document.querySelector("#account-form");
const error = document.querySelector("#account-error");
const password = form.elements.password;
const submit = document.querySelector("#submit-account");
function renderMode() {
  const en = window.appLocale?.language === "en";
  const words = en ? {
    login: ["Log in to your account", "Your products will sync across devices.", "Log in", "Create an account", "Don't have an account?"],
    register: ["Create your account", "Use the same account on your phone and computer.", "Create account", "Log in", "Already have an account?"]
  } : {
    login: ["Влез в профила си", "Продуктите ти ще се синхронизират между устройствата.", "Вход", "Създай профил", "Нямаш профил?"],
    register: ["Създай профил", "Използвай един и същ профил на телефона и компютъра си.", "Създай профил", "Вход", "Имаш профил?"]
  };
  const [title, intro, submitText, toggle, copy] = words[registerMode ? "register" : "login"];
  document.querySelector("#login-title").textContent = title;
  document.querySelector("#login-intro").textContent = intro;
  submit.textContent = submitText;
  document.querySelector("#toggle-copy").textContent = copy;
  document.querySelector("#toggle-mode").textContent = toggle;
  const labels = en ? ["Email", "Password"] : ["Имейл", "Парола"];
  const fieldLabels = form.querySelectorAll("label");
  fieldLabels.forEach((label, index) => { if (label.firstChild) label.firstChild.textContent = `${labels[index]} `; });
  document.querySelector("#password-hint").textContent = en ? "Use at least 10 characters." : "Използвай поне 10 знака.";
  document.querySelector("#account-form").querySelector("input[name=email]").placeholder = en ? "you@example.com" : "ime@example.com";
  password.autocomplete = registerMode ? "new-password" : "current-password";
  password.minLength = registerMode ? 10 : 1;
  document.querySelector("#password-hint").hidden = !registerMode;
}
document.querySelector("#toggle-mode").addEventListener("click", () => { registerMode = !registerMode; error.hidden = true; renderMode(); });
form.addEventListener("submit", async event => {
  event.preventDefault();
  error.hidden = true;
  submit.disabled = true;
  try {
    const response = await fetch(`/api/auth/${registerMode ? "register" : "login"}`, {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email: form.elements.email.value, password: password.value })
    });
    const body = await response.json().catch(() => null);
    if (!response.ok) throw new Error(body?.message || "Не успяхме да влезем в профила.");
    const requestedReturn = new URLSearchParams(location.search).get("returnUrl");
    const returnUrl = requestedReturn?.startsWith("/") && !requestedReturn.startsWith("//") ? requestedReturn : "/";
    location.replace(returnUrl);
  } catch (exception) {
    error.textContent = exception.message;
    error.hidden = false;
  } finally {
    submit.disabled = false;
  }
});
document.addEventListener("app-language-change", renderMode);
renderMode();
