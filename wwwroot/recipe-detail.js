const recipeId = new URLSearchParams(location.search).get("id");
const recipePage = document.querySelector("#recipe-page");
const loading = document.querySelector("#detail-loading");
const errorState = document.querySelector("#detail-error");
const feedback = document.querySelector("#detail-feedback");
const ingredientsList = document.querySelector("#detail-ingredients");
const stepsList = document.querySelector("#detail-steps");
const favoriteButton = document.querySelector("#favorite-button");
const addMissingButton = document.querySelector("#add-missing-button");
const sourceLink = document.querySelector("#detail-source");
let recipe;
let recipeTranslation = null;
let displayLanguage = window.appLocale?.language || "bg";
const t = (key, values) => window.appLocale?.t(key, values) || key;
function displayUnit(unit) { return displayLanguage === "en" ? ({"бр.":"pcs","г":"g","кг":"kg","мл":"ml","л":"l","пакет":"pack"}[unit] || unit) : unit; }

const returnUrl = sessionStorage.getItem("recipeReturnUrl");
if (returnUrl?.startsWith("/recipes.html")) document.querySelector("#recipe-back").href = returnUrl;

function addTextItem(list, text, className = "") {
  const item = document.createElement("li");
  if (className) item.className = className;
  item.textContent = text;
  list.append(item);
}

function renderRecipe(data) {
  recipe = data;
  const translation = displayLanguage === "bg" ? recipeTranslation : null;
  const displayTitle = translation?.title || data.title;
  document.title = `${displayTitle} | ${t("brand")}`;
  document.querySelector("#detail-title").textContent = displayTitle;
  document.querySelector("#detail-facts").textContent = [
    data.readyInMinutes ? `${data.readyInMinutes} ${displayLanguage === "bg" ? "минути" : "minutes"}` : "",
    data.servings ? `${data.servings} ${displayLanguage === "bg" ? "порции" : "servings"}` : "",
    data.sourceName || ""
  ].filter(Boolean).join(" · ");

  if (data.imageUrl) {
    const photo = document.querySelector("#detail-photo");
    const image = document.createElement("img");
    image.src = data.imageUrl;
    image.alt = data.title;
    image.addEventListener("error", () => { photo.hidden = true; }, { once: true });
    photo.replaceChildren(image);
    photo.hidden = false;
  }

  ingredientsList.replaceChildren();
  const ingredients = data.ingredients || [];
  const missing = ingredients.filter(item => !item.isOptional && !item.isInPantry);
  document.querySelector("#ingredient-status").textContent = missing.length
    ? t("missing", {count: missing.length}) : t("allAtHome");
  for (const [index, ingredient] of ingredients.entries()) {
    const item = document.createElement("li");
    item.className = ingredient.isInPantry ? "ingredient-at-home" : ingredient.isOptional ? "ingredient-optional" : "ingredient-missing";
    const check = document.createElement("span");
    check.className = "ingredient-check";
    check.textContent = ingredient.isInPantry ? "✓" : ingredient.isOptional ? "○" : "×";
    check.setAttribute("aria-hidden", "true");
    const name = document.createElement("span");
    name.className = "ingredient-name";
    name.textContent = translation?.ingredientNames?.[index] || ingredient.catalogName || ingredient.name;
    const amount = document.createElement("span");
    amount.className = "ingredient-amount";
    const unit = displayLanguage === "bg"
      ? (translation?.ingredientUnits?.[index] || ingredient.unit || "бр.")
      : displayUnit(ingredient.unit || "piece");
    amount.textContent = `${ingredient.amount || 1} ${unit}`;
    const label = document.createElement("small");
    label.className = "ingredient-label";
    label.textContent = ingredient.isInPantry ? t("atHome") : ingredient.isOptional ? t("optional") : t("missingLabel");
    item.append(check, name, amount, label);
    ingredientsList.append(item);
  }
  if (!ingredients.length) addTextItem(ingredientsList, t("noIngredients"));
  addMissingButton.disabled = missing.length === 0;

  stepsList.replaceChildren();
  for (const [index, step] of (data.steps || []).entries()) {
    addTextItem(stepsList, translation?.stepInstructions?.[index] || step.instruction);
  }
  if (!stepsList.children.length) addTextItem(stepsList, t("noSteps"));

  favoriteButton.textContent = data.isFavorite ? t("removeFavorite") : t("saveFavorite");
  favoriteButton.classList.toggle("is-favorite", data.isFavorite);
  if (data.sourceUrl) {
    sourceLink.href = data.sourceUrl;
    sourceLink.textContent = `${t("originalRecipe")} · ${data.sourceName || (displayLanguage === "bg" ? "източник" : "source")} ↗`;
    sourceLink.hidden = false;
  }
  document.querySelector("#detail-language-note").hidden = displayLanguage !== "bg";
}

function setLanguageInUrl(language) {
  const params = new URLSearchParams(location.search);
  if (language === "bg") params.set("lang", "bg");
  else params.delete("lang");
  const query = params.toString();
  history.replaceState(null, "", query ? `${location.pathname}?${query}` : location.pathname);
}

async function changeLanguage(language, updateUrl = true) {
  if (!recipe || !["en", "bg"].includes(language)) return;
  if (language === "en") {
    displayLanguage = "en";
    feedback.textContent = "";
    renderRecipe(recipe);
    if (updateUrl) setLanguageInUrl("en");
    return;
  }

  if (recipeTranslation) {
    displayLanguage = "bg";
    renderRecipe(recipe);
    if (updateUrl) setLanguageInUrl("bg");
    return;
  }

  feedback.textContent = t("translateWait");
  try {
    const response = await fetch(`/api/recipes/${recipeId}/translation?language=bg`);
    const body = await response.json().catch(() => null);
    if (!response.ok) throw new Error(body?.message || "Не успяхме да преведем рецептата.");
    recipeTranslation = body;
    displayLanguage = "bg";
    feedback.textContent = "";
    renderRecipe(recipe);
    if (updateUrl) setLanguageInUrl("bg");
  } catch (error) {
    feedback.textContent = `${error.message} ${t("translatedFallback")}`;
  } finally {
  }
}

async function send(url, method) {
  const response = await fetch(url, { method });
  const body = await response.json().catch(() => null);
  if (!response.ok) throw new Error(body?.message || t("requestFailed"));
  return body;
}

favoriteButton.addEventListener("click", async () => {
  favoriteButton.disabled = true;
  feedback.textContent = t("saving");
  try {
    if (recipe.isFavorite) {
      await fetch(`/api/recipes/${recipeId}/favorite`, { method: "DELETE" });
      recipe.isFavorite = false;
      feedback.textContent = t("removed");
    } else {
      await send(`/api/recipes/${recipeId}/favorite`, "POST");
      recipe.isFavorite = true;
      feedback.textContent = t("saved");
    }
    favoriteButton.textContent = recipe.isFavorite ? t("removeFavorite") : t("saveFavorite");
    favoriteButton.classList.toggle("is-favorite", recipe.isFavorite);
  } catch (error) {
    feedback.textContent = error.message;
  } finally {
    favoriteButton.disabled = false;
  }
});

addMissingButton.addEventListener("click", async () => {
  addMissingButton.disabled = true;
  feedback.textContent = t("addingMissing");
  try {
    const result = await send(`/api/recipes/${recipeId}/add-missing`, "POST");
    feedback.textContent = result.addedCount
      ? t("addedProducts", {count: result.addedCount})
      : result.message || t("noMissing");
    const response = await fetch(`/api/recipes/${recipeId}`);
    const updated = await response.json();
    if (response.ok) renderRecipe(updated);
  } catch (error) {
    feedback.textContent = error.message;
    addMissingButton.disabled = false;
  }
});

async function load() {
  if (!/^\d+$/.test(recipeId || "")) {
    loading.hidden = true;
    errorState.textContent = t("invalidRecipe");
    errorState.hidden = false;
    return;
  }
  try {
    const response = await fetch(`/api/recipes/${encodeURIComponent(recipeId)}`);
    const data = await response.json().catch(() => null);
    if (!response.ok) throw new Error(data?.message || "Не успяхме да заредим рецептата.");
    renderRecipe(data);
    recipePage.hidden = false;
    loading.hidden = true;
    if (displayLanguage === "bg") await changeLanguage("bg", false);
  } catch (error) {
    loading.hidden = true;
    errorState.textContent = error.message;
    errorState.hidden = false;
  }
}

document.addEventListener("app-language-change", event => {
  displayLanguage = event.detail.language;
  if (recipe) changeLanguage(displayLanguage, true);
});

window.appSession.then(() => load()).catch(() => {});
