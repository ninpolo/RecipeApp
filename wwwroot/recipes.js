const recipeList = document.querySelector("#recipe-list");
const recipePagination = document.querySelector("#recipe-pagination");
const recipeSection = document.querySelector(".recipe-section");
const recipeCount = document.querySelector("#recipe-count");
const recipeStatus = document.querySelector("#recipe-status");
const emptyState = document.querySelector("#recipe-empty");
const errorState = document.querySelector("#recipe-error");
const searchForm = document.querySelector("#recipe-search-form");
const searchInput = document.querySelector("#recipe-search");
const modeButtons = [...document.querySelectorAll(".recipe-mode")];
const categoryButtons = [...document.querySelectorAll(".recipe-layout .category-tab")];
const categoryTabs = document.querySelector(".recipe-layout .category-tabs");
const responseCache = new Map();
const initialParams = new URLSearchParams(window.location.search);
const t = (key, values) => window.appLocale?.t(key, values) || key;
const categoryKeys = {"Закуска":"breakfast","Обяд":"lunch","Вечеря":"dinner","Десерт":"dessert"};
function displayCategory(value) { return categoryKeys[value] ? t(categoryKeys[value]) : value; }

let selectedMode = ["all", "favorites"].includes(initialParams.get("mode")) ? initialParams.get("mode") : "pantry";
let selectedCategory = categoryButtons.some(button => button.dataset.category === initialParams.get("category"))
  ? initialParams.get("category")
  : "";
let selectedPage = selectedMode === "all"
  ? Math.max(1, Math.min(76, Number.parseInt(initialParams.get("page") || "1", 10) || 1))
  : 1;
let requestVersion = 0;
let pageTurnDirection = "forward";

if (initialParams.has("query")) searchInput.value = initialParams.get("query");
modeButtons.forEach(button => {
  const active = button.dataset.mode === selectedMode;
  button.classList.toggle("active", active);
  button.setAttribute("aria-pressed", String(active));
});
categoryTabs.hidden = selectedMode === "favorites";
categoryButtons.forEach(button => {
  const active = button.dataset.category === selectedCategory;
  button.classList.toggle("active", active);
  button.setAttribute("aria-pressed", String(active));
});

function syncRecipeUrl() {
  const params = new URLSearchParams();
  if (selectedMode !== "pantry") {
    params.set("mode", selectedMode);
    if (selectedMode === "all") params.set("page", selectedPage);
  }
  const query = searchInput.value.trim();
  if (query) params.set("query", query);
  if (selectedCategory) params.set("category", selectedCategory);
  const queryString = params.toString();
  history.replaceState(null, "", queryString ? `${location.pathname}?${queryString}` : location.pathname);
}

async function loadRecipes() {
  const currentRequest = ++requestVersion;
  const query = searchInput.value.trim();
  const params = new URLSearchParams({ mode: selectedMode });
  if (query) params.set("query", query);
  if (selectedCategory) params.set("category", selectedCategory);
  if (selectedMode === "all") params.set("page", selectedPage);
  const cacheKey = params.toString();

  errorState.hidden = true;
  emptyState.hidden = true;
  recipeList.hidden = false;
  recipeList.replaceChildren();
  recipeStatus.textContent = t("searchingRecipes");

  try {
    let resultPage = responseCache.get(cacheKey);
    if (!resultPage) {
      const response = await fetch(`/api/recipes?${cacheKey}`);
      const body = await response.json().catch(() => null);
      if (!response.ok) {
        throw new Error(body?.message || "Не успяхме да заредим рецептите.");
      }
      resultPage = body;
      responseCache.set(cacheKey, resultPage);
    }

    if (currentRequest !== requestVersion) return;
    const recipes = resultPage.results || [];
    const totalPages = Math.max(1, Math.min(76, Math.ceil(resultPage.totalResults / resultPage.pageSize)));
    recipeCount.textContent = resultPage.totalResults;
    recipeStatus.textContent = selectedMode === "pantry"
      ? t("accordingToProducts")
      : selectedMode === "favorites" ? t("savedRecipes") : t("pageOf", {page:selectedPage,total:totalPages});
    document.querySelector("#recipe-list-title").textContent = selectedMode === "favorites" ? t("favoriteRecipes") : t("filteredRecipes");
    document.querySelector(".recipe-footer p").textContent = selectedMode === "favorites"
      ? t("savedRecipesWait")
      : t("recipeFooter");
    recipeSection.classList.toggle("has-pages", selectedMode === "all" && totalPages > 1);
    recipeSection.classList.toggle("current-book-page", selectedMode === "all" && selectedPage > 1);
    recipeList.classList.remove("page-turn-forward", "page-turn-back");
    void recipeList.offsetWidth;
    recipeList.classList.add(pageTurnDirection === "back" ? "page-turn-back" : "page-turn-forward");
    const translatedTitles = await translateTitles(recipes, currentRequest);
    if (currentRequest !== requestVersion) return;
    renderRecipes(recipes, translatedTitles);
    renderPagination(resultPage.totalResults, resultPage.pageSize, totalPages);
    if (recipes.length === 0) {
      recipeList.hidden = true;
      emptyState.textContent = selectedMode === "pantry"
      ? t("pantryEmpty")
        : selectedMode === "favorites" ? t("favoritesEmpty") : t("noRecipes");
      emptyState.hidden = false;
      recipeStatus.textContent = t("noResults");
    }
  } catch (error) {
    if (currentRequest !== requestVersion) return;
    recipeCount.textContent = "–";
    recipeStatus.textContent = t("searchFailed");
    errorState.textContent = error.message;
    errorState.hidden = false;
    recipePagination.hidden = true;
  }
}

function renderPagination(totalResults, pageSize, totalPages) {
  recipePagination.replaceChildren();
  recipePagination.hidden = selectedMode !== "all" || totalPages <= 1;
  if (recipePagination.hidden) return;

  recipePagination.classList.toggle("book-page-active", selectedPage > 1);
  const previous = document.createElement("button");
  previous.type = "button";
  previous.className = "page-turn-button";
  previous.textContent = t("previous");
  previous.disabled = selectedPage <= 1;
  previous.setAttribute("aria-label", t("previous").replace("‹ ", ""));
  previous.addEventListener("click", () => changePage(selectedPage - 1, "back"));
  recipePagination.append(previous);

  const visiblePages = new Set([1, totalPages, selectedPage - 1, selectedPage, selectedPage + 1]);
  let previousPage = 0;
  for (const page of [...visiblePages].filter(page => page >= 1 && page <= totalPages).sort((a, b) => a - b)) {
    if (page - previousPage > 1) {
      const ellipsis = document.createElement("span");
      ellipsis.className = "page-ellipsis";
      ellipsis.textContent = "…";
      recipePagination.append(ellipsis);
    }
    const pageButton = document.createElement("button");
    pageButton.type = "button";
    pageButton.className = "recipe-page-number";
    pageButton.textContent = page;
    pageButton.setAttribute("aria-label", t("pageLabel", {page}));
    if (page === selectedPage) {
      pageButton.classList.add("active");
      pageButton.setAttribute("aria-current", "page");
    }
    pageButton.addEventListener("click", () => changePage(page, page < selectedPage ? "back" : "forward"));
    recipePagination.append(pageButton);
    previousPage = page;
  }

  const next = document.createElement("button");
  next.type = "button";
  next.className = "page-turn-button";
  next.textContent = t("next");
  next.disabled = selectedPage >= totalPages;
  next.setAttribute("aria-label", t("pageLabel", {page:selectedPage+1}));
  next.addEventListener("click", () => changePage(selectedPage + 1, "forward"));
  recipePagination.append(next);
}

function changePage(page, direction) {
  selectedPage = page;
  pageTurnDirection = direction;
  syncRecipeUrl();
  loadRecipes();
}

async function translateTitles(recipes, currentRequest) {
  if (window.appLocale?.language !== "bg" || recipes.length === 0) return [];
  try {
    const response = await fetch("/api/recipes/translate-titles", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ language: "bg", titles: recipes.map(recipe => ({ id: recipe.id, title: recipe.title })) })
    });
    const body = await response.json().catch(() => null);
    if (!response.ok || currentRequest !== requestVersion || !Array.isArray(body?.titles)) return [];
    return body.titles;
  } catch {
    return [];
  }
}

function renderRecipes(recipes, translatedTitles = []) {
  recipeList.replaceChildren();
  for (const [index, recipe] of recipes.entries()) {
    const row = document.createElement("article");
    row.className = "recipe-row";

    const photo = document.createElement("figure");
    photo.className = "recipe-photo";
    if (recipe.imageUrl) {
      const image = document.createElement("img");
      image.src = recipe.imageUrl;
      image.alt = recipe.title;
      image.loading = "lazy";
      image.decoding = "async";
      image.addEventListener("error", () => {
        image.remove();
        photo.classList.add("image-missing");
        photo.textContent = t("noPhoto");
      }, { once: true });
      photo.append(image);
    } else {
      photo.classList.add("image-missing");
      photo.textContent = t("noPhoto");
    }

    const info = document.createElement("div");
    info.className = "recipe-info";
    const title = document.createElement("a");
    title.className = "recipe-open-link";
    title.href = `/recipe.html?id=${encodeURIComponent(recipe.id)}`;
    const displayTitle = translatedTitles[index] || recipe.title;
    title.textContent = displayTitle;
    title.addEventListener("click", () => {
      sessionStorage.setItem("recipeReturnUrl", `${location.pathname}${location.search}`);
    });
    const description = document.createElement("p");
    const facts = [];
    if (selectedCategory) facts.push(displayCategory(selectedCategory));
    if (recipe.readyInMinutes) facts.push(`${recipe.readyInMinutes} ${t("minutesShort")}`);
    if (selectedMode === "pantry" && recipe.usedIngredientCount != null) {
      const needed = recipe.usedIngredientCount + (recipe.missedIngredientCount || 0);
      facts.push(recipe.missedIngredientCount === 0
        ? t("allNeeded", {count:needed})
        : t("someNeeded", {used:recipe.usedIngredientCount,total:needed}));
    } else if (recipe.sourceName) {
      facts.push(recipe.sourceName);
    }
    description.textContent = facts.join(" · ") || t("catalogRecipe");
    info.append(title, description);

    const open = document.createElement("a");
    open.className = "recipe-card-open";
    open.href = title.href;
    open.textContent = t("open");
    open.setAttribute("aria-label", `${t("open").replace(" →", "")}: ${displayTitle}`);
    open.addEventListener("click", () => sessionStorage.setItem("recipeReturnUrl", `${location.pathname}${location.search}`));

    row.append(photo, info, open);
    recipeList.append(row);
  }
}

searchForm.addEventListener("submit", event => {
  event.preventDefault();
  selectedPage = 1;
  syncRecipeUrl();
  loadRecipes();
});

modeButtons.forEach(button => {
  button.addEventListener("click", () => {
    selectedMode = button.dataset.mode;
    categoryTabs.hidden = selectedMode === "favorites";
    if (selectedMode === "favorites" && selectedCategory) {
      selectedCategory = "";
      categoryButtons.forEach(categoryButton => {
        const active = categoryButton.dataset.category === "";
        categoryButton.classList.toggle("active", active);
        categoryButton.setAttribute("aria-pressed", String(active));
      });
    }
    selectedPage = 1;
    syncRecipeUrl();
    modeButtons.forEach(modeButton => {
      const active = modeButton === button;
      modeButton.classList.toggle("active", active);
      modeButton.setAttribute("aria-pressed", String(active));
    });
    loadRecipes();
  });
});

categoryButtons.forEach(button => {
  button.addEventListener("click", () => {
    selectedCategory = button.dataset.category;
    selectedPage = 1;
    syncRecipeUrl();
    categoryButtons.forEach(categoryButton => {
      const active = categoryButton === button;
      categoryButton.classList.toggle("active", active);
      categoryButton.setAttribute("aria-pressed", String(active));
    });
    loadRecipes();
  });
});

document.addEventListener("app-language-change", () => loadRecipes());
window.appSession.then(() => loadRecipes()).catch(() => {});

