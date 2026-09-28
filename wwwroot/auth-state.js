window.appSession = (async () => {
  const response = await fetch("/api/auth/me");
  if (!response.ok) {
    const returnUrl = `${location.pathname}${location.search}`;
    location.replace(`/login.html?returnUrl=${encodeURIComponent(returnUrl)}`);
    throw new Error("Влез в профила си, за да продължиш.");
  }

  const user = await response.json();
  const header = document.querySelector(".topbar");
  if (header) {
    const area = document.createElement("div");
    area.className = "account-menu";
    const email = document.createElement("span");
    email.textContent = user.email;
    const logout = document.createElement("button");
    logout.type = "button";
    logout.className = "text-button";
    const updateLogout = () => { logout.textContent = window.appLocale?.language === "en" ? "Log out" : "Изход"; };
    updateLogout();
    document.addEventListener("app-language-change", updateLogout);
    logout.addEventListener("click", async () => {
      await fetch("/api/auth/logout", { method: "POST" });
      location.replace("/login.html");
    });
    area.append(email, logout);
    header.append(area);
  }
  return user;
})();
