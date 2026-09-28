const inventoryList = document.querySelector("#inventory-list");
const productForm = document.querySelector("#product-form");
const formWrap = document.querySelector("#product-form-wrap");
const categorySelect = document.querySelector("#category-select");
const formError = document.querySelector("#form-error");
const loadError = document.querySelector("#load-error");
const emptyState = document.querySelector("#empty-state");
const listCaption = document.querySelector("#list-caption");
const productCount = document.querySelector("#product-count");
const appShell = document.querySelector(".app-shell");
const productNameInput = document.querySelector("#product-name");
const productSuggestions = document.querySelector("#product-suggestions");
const categoryTabs = document.querySelector("#category-tabs");
let inventoryItems = [];
let productCategories = [];
let activeFilter = null;
let selectedCatalogSuggestion = null;
let suggestionTimer;
const t = (key, values) => window.appLocale?.t(key, values) || key;
const categoryEnglish = { "Без категория":"Uncategorized", "Зеленчуци":"Vegetables", "Плодове":"Fruit", "Месо":"Meat", "Риба":"Fish", "Млечни":"Dairy", "Подправки":"Spices", "Зърнени":"Grains", "Напитки":"Drinks" };
function categoryLabel(name) { return window.appLocale?.language === "en" ? (categoryEnglish[name] || name) : name; }
function displayUnit(unit) { return window.appLocale?.language === "en" ? ({"бр.":"pcs","г":"g","кг":"kg","мл":"ml","л":"l","пакет":"pack"}[unit] || unit) : unit; }

async function request(url, options = {}) {
  const response = await fetch(url, {
    ...options,
    headers: { "Content-Type": "application/json", ...options.headers }
  });

  if (!response.ok) {
    const error = await response.json().catch(() => null);
    throw new Error(error?.message || "Заявката не успя. Опитай отново.");
  }

  return response.status === 204 ? null : response.json();
}

function getFilteredInventory() {
  if (!activeFilter) return inventoryItems;
  if (activeFilter === "uncategorized") return inventoryItems.filter(item => !item.category);
  const categoryName = activeFilter.slice("category:".length);
  return inventoryItems.filter(item => item.category === categoryName);
}

function renderCategoryTabs() {
  for (const option of categorySelect.options) {
    const category = productCategories.find(item => String(item.id) === option.value);
    option.textContent = option.value ? categoryLabel(category?.name || option.textContent) : t("uncategorized");
  }
  const colors = ["#b95745", "#4f8055", "#91aada", "#d9908a", "#c28a54", "#647b59", "#a9584d", "#778dab"];
  const tabs = [];

  productCategories.forEach((category, index) => {
    const count = inventoryItems.filter(item => item.category === category.name).length;
    if (count > 0) {
      tabs.push({
        name: category.name,
        filter: `category:${category.name}`,
        count,
        color: colors[index % colors.length]
      });
    }
  });

  const uncategorizedCount = inventoryItems.filter(item => !item.category).length;
  if (uncategorizedCount > 0) {
    tabs.push({ name: "Без категория", filter: "uncategorized", count: uncategorizedCount, color: "#8a8174" });
  }

  categoryTabs.replaceChildren();
  tabs.forEach((tab, index) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className = `category-tab${activeFilter === tab.filter ? " active" : ""}`;
    button.dataset.filter = tab.filter;
    button.style.setProperty("--tab-accent", tab.color || "#b95745");
    button.title = `${categoryLabel(tab.name)}: ${tab.count} ${t("tabProducts")}`;
    button.setAttribute("aria-label", `${categoryLabel(tab.name)}, ${tab.count} ${t("tabProducts")}`);
    if (activeFilter === tab.filter) button.setAttribute("aria-current", "true");

    const name = document.createElement("span");
    name.className = "category-tab-name";
    name.textContent = categoryLabel(tab.name);
    const count = document.createElement("span");
    count.className = "category-tab-count";
    count.textContent = tab.count;
    button.append(name, count);
    button.addEventListener("click", () => {
      activeFilter = activeFilter === tab.filter ? null : tab.filter;
      renderCategoryTabs();
      renderInventory(getFilteredInventory());
    });
    categoryTabs.append(button);
  });
}

function renderInventory(items) {
  inventoryList.replaceChildren();
  productCount.textContent = items.length;
  listCaption.textContent = items.length === 1 ? `1 ${t("available")}` : `${items.length} ${window.appLocale?.language === "en" ? "available products" : "налични продукта"}`;
  emptyState.hidden = items.length > 0;
  emptyState.textContent = !activeFilter
    ? t("emptyPantry")
    : t("emptyCategory");

  for (const item of items) {
    const row = document.createElement("article");
    row.className = "inventory-row";
    row.dataset.id = item.id;

    const details = document.createElement("div");
    const name = document.createElement("div");
    name.className = "product-name";
    name.textContent = item.name;
    const meta = document.createElement("div");
    meta.className = "product-meta";
    meta.textContent = categoryLabel(item.category || t("uncategorized"));
    details.append(name, meta);

    const quantity = document.createElement("span");
    quantity.className = "quantity";
    quantity.textContent = `${item.quantity} ${displayUnit(item.unit)}`;

    const status = document.createElement("span");
    const hasStock = Number(item.quantity) > 0;
    status.className = `product-status ${hasStock ? "available" : "unavailable"}`;
    status.textContent = hasStock ? "✓" : "×";
    status.setAttribute("aria-label", hasStock ? t("allStock") : t("noStock"));

    const remove = document.createElement("button");
    remove.className = "remove-button";
    remove.type = "button";
    remove.dataset.removeId = item.id;
    remove.setAttribute("aria-label", t("delete", {name:item.name}));
    remove.textContent = "×";

    row.append(details, status, quantity, remove);
    inventoryList.append(row);
  }
}

async function loadInventory() {
  loadError.hidden = true;
  listCaption.textContent = t("loadingProducts");
  try {
    inventoryItems = await request("/api/inventory-items");
    if (activeFilter?.startsWith("category:") && getFilteredInventory().length === 0) activeFilter = null;
    if (activeFilter === "uncategorized" && !inventoryItems.some(item => !item.category)) activeFilter = null;
    renderCategoryTabs();
    renderInventory(getFilteredInventory());
  } catch (error) {
    listCaption.textContent = "";
    loadError.textContent = error.message;
    loadError.hidden = false;
  }
}

async function loadCategories() {
  try {
    productCategories = await request("/api/product-categories");
    renderCategoryTabs();
    for (const category of productCategories) {
      const option = document.createElement("option");
      option.value = category.id;
      option.textContent = categoryLabel(category.name);
      categorySelect.append(option);
    }
  } catch (error) {
    formError.textContent = error.message;
    formError.hidden = false;
  }
}

document.querySelector("#add-toggle").addEventListener("click", (event) => {
  const open = formWrap.hidden;
  formWrap.hidden = !open;
  event.currentTarget.setAttribute("aria-expanded", String(open));
  if (open) productForm.elements.name.focus();
});

document.querySelector("#cancel-add").addEventListener("click", () => {
  productForm.reset();
  formError.hidden = true;
  formWrap.hidden = true;
  productSuggestions.hidden = true;
  selectedCatalogSuggestion = null;
  document.querySelector("#add-toggle").setAttribute("aria-expanded", "false");
});

productNameInput.addEventListener("input", () => {
  selectedCatalogSuggestion = null;
  clearTimeout(suggestionTimer);
  const query = productNameInput.value.trim();
  if (query.length < 2) {
    productSuggestions.hidden = true;
    return;
  }
  suggestionTimer = setTimeout(async () => {
    try {
      const suggestions = await request(`/api/product-suggestions?query=${encodeURIComponent(query)}`);
      if (productNameInput.value.trim() !== query) return;
      productSuggestions.replaceChildren();
      for (const suggestion of suggestions) {
        const option = document.createElement("li");
        option.setAttribute("role", "option");
        const button = document.createElement("button");
        button.type = "button";
        button.className = "suggestion-option";
        const name = document.createElement("span");
        name.textContent = suggestion.name;
        const source = document.createElement("span");
        source.className = "suggestion-source";
        source.textContent = suggestion.catalogProvider || suggestion.category || t("suggestionCatalog");
        button.append(name, source);
        button.addEventListener("click", () => {
          productNameInput.value = suggestion.name;
          selectedCatalogSuggestion = suggestion.catalogProvider ? suggestion : null;
          productSuggestions.hidden = true;
          productForm.elements.quantity.focus();
        });
        option.append(button);
        productSuggestions.append(option);
      }
      productSuggestions.hidden = suggestions.length === 0;
    } catch {
      productSuggestions.hidden = true;
    }
  }, 300);
});

document.addEventListener("click", (event) => {
  if (!event.target.closest(".suggestion-field")) productSuggestions.hidden = true;
});

productForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  formError.hidden = true;
  const data = new FormData(productForm);
  const payload = {
    name: data.get("name"),
    quantity: Number(data.get("quantity")),
    unit: data.get("unit"),
    productCategoryId: data.get("productCategoryId") ? Number(data.get("productCategoryId")) : null,
    catalogProvider: selectedCatalogSuggestion?.catalogProvider || null,
    catalogItemId: selectedCatalogSuggestion?.catalogItemId || null
  };

  try {
    await request("/api/inventory-items", { method: "POST", body: JSON.stringify(payload) });
    productForm.reset();
    selectedCatalogSuggestion = null;
    activeFilter = null;
    renderCategoryTabs();
    formWrap.hidden = true;
    document.querySelector("#add-toggle").setAttribute("aria-expanded", "false");
    await loadInventory();
  } catch (error) {
    formError.textContent = error.message;
    formError.hidden = false;
  }
});

document.querySelector("#edit-toggle").addEventListener("click", (event) => {
  const editing = appShell.classList.toggle("edit-mode");
  event.currentTarget.dataset.editing = String(editing);
  event.currentTarget.textContent = t(editing ? "done" : "edit");
});

inventoryList.addEventListener("click", async (event) => {
  const button = event.target.closest("[data-remove-id]");
  if (!button) return;
  try {
    await request(`/api/inventory-items/${button.dataset.removeId}`, { method: "DELETE" });
    await loadInventory();
  } catch (error) {
    loadError.textContent = error.message;
    loadError.hidden = false;
  }
});

document.addEventListener("app-language-change", () => { renderCategoryTabs(); renderInventory(getFilteredInventory()); });
window.appSession.then(() => { loadCategories(); loadInventory(); }).catch(() => {});
