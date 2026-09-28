(() => {
  const strings = {
    en: {
      language: "Language", brand: "Home & Pantry", home: "Home", recipes: "Recipes", products: "My products", edit: "Edit", done: "Done", productCountLabel:"Product count", recipeCountLabel:"Recipe count", productCountOne:"product", productCountMany:"products", tabProducts:"products", minutesShort:"min", recipeReady:"{minutes} min · {servings} servings",
      addProduct: "Add product", productName: "Product name", example: "For example: tomatoes", suggestionsHint: "Online suggestions use English; you can also type a name yourself.",
      quantity: "Quantity", unit: "Unit", category: "Category", uncategorized: "Uncategorized", cancel: "Cancel", saveProduct: "Save product", atHome: "At home",
      available: "available", productsLower: "products", emptyPantry: "Your list is empty. Add the first product you have at home.", emptyCategory: "No products in this category.",
      loadingProducts: "Loading products…", recipesForYou: "Find recipes", recipesSubtitle: "Choose what you feel like eating today.", recipeCount: "recipes", searchRecipe: "Search recipes",
      search: "Search", withMyProducts: "With my products", allRecipes: "All recipes", favorites: "Favorites", all: "All", breakfast: "Breakfast", lunch: "Lunch", dinner: "Dinner", dessert: "Dessert",
      recipeIdeas: "COOKING IDEAS", filteredRecipes: "Selected recipes", accordingToProducts: "Based on your products", savedRecipes: "Saved recipes", pageOf: "Page {page} of {total} · From catalogue",
      favoriteRecipes: "Favorite recipes", recipeFooter: "Recipes are matched to the ingredients you have at home.", savedRecipesWait: "Your saved recipes will be here.",
      searchingRecipes: "Searching recipes…", noResults: "No results", searchFailed: "Search failed", pantryEmpty: "No recipes found with these products. Try “All recipes”.", favoritesEmpty: "You have no saved favorites yet.", noRecipes: "No recipes match these filters.",
      previous: "‹ Previous", next: "Next ›", noPhoto: "No photo", catalogRecipe: "Recipe from catalogue", open: "Open →", allNeeded: "You have all {count} ingredients", someNeeded: "You have {used} of {total} ingredients",
      excerpt: "FROM THE RECIPE BOOK", original: "English", bulgarian: "Bulgarian", ingredients: "Ingredients", method: "Method", missing: "Missing {count}", allAtHome: "Everything is at home",
      optional: "Optional", missingLabel: "Missing", saveFavorite: "♡ Save to favorites", removeFavorite: "♥ Remove from favorites", addMissing: "Add missing products", translationNote: "Automatic translation from English. The source link opens the original recipe.",
      sourceRecipe: "View source recipe ↗", originalRecipe: "Original recipe", loadingRecipe: "Loading recipe…", invalidRecipe: "Recipe ID is missing or invalid.",
      noIngredients: "Ingredient list is unavailable.", noSteps: "Instructions are unavailable.", translateWait: "Translating recipe into Bulgarian…", translatedFallback: "Showing the English text.", saving: "Saving…", saved: "Saved to favorites.", removed: "Removed from favorites.", addingMissing: "Adding missing products…", addedProducts: "Added {count} products to your list.", noMissing: "There are no missing products to add.",
      allStock: "In stock", noStock: "Out of stock", delete: "Delete {name}", suggestionCatalog: "My catalogue", requestFailed: "Request failed. Please try again.",
      appTitle: "Home & Pantry", listProducts: "My products", pantry: "At home", viewRecipes: "Browse recipes →", recipeFooterProducts: "Recipes use the products from this list.", accountLogin:"Log in", accountCreate:"Create account", accountNeed:"Don't have an account?", accountHave:"Already have an account?", accountLoginTitle:"Log in to your account", accountRegisterInfo:"Use the same account on your phone and computer.", accountLoginInfo:"Your products will sync across devices.",
      addSection: "Add a product", filterCategory: "Filter by category", recipeSearchArea: "Search recipes", recipeModes: "Recipe filters", recipeCategories: "Recipe categories", recipePages: "Recipe pages", recipeHeading: "Recipes", pageLabel: "Page {page}"
    },
    bg: {
      language: "Език", brand: "Вкус у дома", home: "Начало", recipes: "Рецепти", products: "Моите продукти", edit: "Редактирай", done: "Готово", productCountLabel:"Брой продукти", recipeCountLabel:"Брой рецепти", productCountOne:"продукт", productCountMany:"продукта", tabProducts:"продукта", minutesShort:"мин", recipeReady:"{minutes} мин · {servings} порции",
      addProduct: "Добави продукт", productName: "Име на продукта", example: "Например: домати", suggestionsHint: "Онлайн предложенията се търсят на английски; можеш да въведеш име и ръчно.",
      quantity: "Количество", unit: "Мерна единица", category: "Категория", uncategorized: "Без категория", cancel: "Отказ", saveProduct: "Запази продукта", atHome: "Вкъщи",
      available: "наличен продукт", productsLower: "продукта", emptyPantry: "Списъкът е празен. Добави първия продукт, който имаш вкъщи.", emptyCategory: "Няма продукти в тази категория.",
      loadingProducts: "Зареждане на продуктите…", recipesForYou: "Идеи за готвене", recipesSubtitle: "Избери какво ти се хапва днес.", recipeCount: "рецепти", searchRecipe: "Търси рецепта",
      search: "Търси", withMyProducts: "С моите продукти", allRecipes: "Всички рецепти", favorites: "Любими", all: "Всички", breakfast: "Закуска", lunch: "Обяд", dinner: "Вечеря", dessert: "Десерт",
      recipeIdeas: "ИДЕИ ЗА ГОТВЕНЕ", filteredRecipes: "Подбрани рецепти", accordingToProducts: "Според продуктите ти", savedRecipes: "Запазени рецепти", pageOf: "Страница {page} от {total} · От каталога",
      favoriteRecipes: "Любими рецепти", recipeFooter: "Рецептите се намират според продуктите, които имаш вкъщи.", savedRecipesWait: "Рецептите, които си запазил, ще те чакат тук.",
      searchingRecipes: "Търся рецепти…", noResults: "Няма резултати", searchFailed: "Търсенето не успя", pantryEmpty: "Няма намерени рецепти с тези продукти. Опитай с „Всички рецепти“.", favoritesEmpty: "Още нямаш запазени любими рецепти.", noRecipes: "Няма рецепти по тези филтри.",
      previous: "‹ Предишна", next: "Следваща ›", noPhoto: "Няма снимка", catalogRecipe: "Рецепта от каталога", open: "Отвори →", allNeeded: "Имаш всички {count} продукта", someNeeded: "Имаш {used} от {total} продукта",
      excerpt: "ОТКЪС ОТ КНИГАТА С РЕЦЕПТИ", original: "English", bulgarian: "Български", ingredients: "Необходими продукти", method: "Начин на приготвяне", missing: "Липсват {count}", allAtHome: "Всичко необходимо е вкъщи",
      optional: "По желание", missingLabel: "Липсва", saveFavorite: "♡ Запази като любима", removeFavorite: "♥ Премахни от любими", addMissing: "Добави липсващите продукти", translationNote: "Автоматичен превод от английски. Източникът отваря оригиналната рецепта.",
      sourceRecipe: "Към оригиналната рецепта ↗", originalRecipe: "Оригинална рецепта", loadingRecipe: "Зареждам рецептата…", invalidRecipe: "Липсва валиден номер на рецепта.",
      noIngredients: "Списъкът със съставки не е наличен.", noSteps: "Инструкциите не са налични.", translateWait: "Превеждам рецептата на български…", translatedFallback: "Показваме текста на английски.", saving: "Запазвам…", saved: "Запазена сред любимите.", removed: "Премахната от любимите.", addingMissing: "Добавям липсващите продукти…", addedProducts: "Добавени {count} продукта в списъка.", noMissing: "Няма липсващи продукти за добавяне.",
      allStock: "Има наличност", noStock: "Няма наличност", delete: "Изтрий {name}", suggestionCatalog: "Моят каталог", requestFailed: "Заявката не успя. Опитай отново.",
      appTitle: "Моите продукти", listProducts: "Моите продукти", pantry: "Вкъщи", viewRecipes: "Виж рецепти →", recipeFooterProducts: "Рецептите ще използват продуктите от този списък.", accountLogin:"Вход", accountCreate:"Създай профил", accountNeed:"Нямаш профил?", accountHave:"Имаш профил?", accountLoginTitle:"Влез в профила си", accountRegisterInfo:"Използвай един и същ профил на телефона и компютъра си.", accountLoginInfo:"Продуктите ти ще се синхронизират между устройствата.",
      addSection: "Добавяне на продукт", filterCategory: "Филтриране по категория", recipeSearchArea: "Търсене на рецепти", recipeModes: "Филтър за налични продукти", recipeCategories: "Категории рецепти", recipePages: "Страници с рецепти", recipeHeading: "Рецепти", pageLabel: "Страница {page}"
    }
  };
  let language = localStorage.getItem("appLanguage");
  if (!['en', 'bg'].includes(language)) language = document.documentElement.lang === 'en' ? 'en' : 'bg';
  const t = (key, values = {}) => (strings[language][key] || strings.bg[key] || key).replace(/\{(\w+)\}/g, (_, name) => values[name] ?? '');
  const apply = () => {
    document.documentElement.lang = language;
    const tr = strings[language];
    const set = (selector, key) => { const el = document.querySelector(selector); if (el) el.textContent = t(key); };
    const attr = (selector, name, key) => { const el = document.querySelector(selector); if (el) el.setAttribute(name, t(key)); };
    document.querySelectorAll(".app-language-switch").forEach(el => el.remove());
    document.querySelectorAll(".topbar").forEach(topbar => {
      const group = document.createElement("div"); group.className = "app-language-switch"; group.setAttribute("role", "group"); group.setAttribute("aria-label", t("language"));
      for (const [code, label] of [["en", "English"], ["bg", "Български"]]) { const button = document.createElement("button"); button.type = "button"; button.textContent = label; button.setAttribute("aria-pressed", String(language === code)); button.addEventListener("click", () => setLanguage(code)); group.append(button); }
      topbar.append(group);
    });
    const brand = document.querySelector(".brand"); if(brand){ const mark=brand.querySelector(".brand-mark"); brand.replaceChildren(...(mark?[mark]:[]),document.createTextNode(` ${t("brand")}`)); brand.setAttribute("aria-label",t("brand")); }
    if (document.querySelector("#edit-toggle")) set("#edit-toggle", document.querySelector("#edit-toggle").dataset.editing === "true" ? "done" : "edit");
    if (document.querySelector("#page-title")) { set("#page-title", "listProducts"); document.title = `${t("appTitle")} | ${t("brand")}`; attr(".product-count","aria-label","productCountLabel"); const sr=document.querySelector(".product-count .visually-hidden"); if(sr) sr.textContent=t("productCountMany"); }
    const addToggle=document.querySelector("#add-toggle"); if(addToggle){ const plus=addToggle.querySelector(".plus"); addToggle.replaceChildren(...(plus?[plus]:[]),document.createTextNode(` ${t("addProduct")}`)); }
    const labels = [...document.querySelectorAll("#product-form label")]; if(labels[0]) labels[0].childNodes[0].textContent = t("productName"); if(labels[1]) labels[1].childNodes[0].textContent = t("quantity"); if(labels[2]) labels[2].childNodes[0].textContent = t("category");
    attr("#product-name", "placeholder", "example"); set(".field-hint", "suggestionsHint"); set("#cancel-add", "cancel"); const submit = document.querySelector("#product-form button[type=submit]"); if(submit) submit.textContent=t("saveProduct");
    attr(".add-section", "aria-label", "addSection"); attr("#category-tabs", "aria-label", "filterCategory"); set("#inventory-title", "pantry");
    const uncategorized=document.querySelector("#category-select option[value='']"); if(uncategorized)uncategorized.textContent=t("uncategorized");
    const unitLabels=language==="en"?{"бр.":"pcs","г":"g","кг":"kg","мл":"ml","л":"l","пакет":"pack"}:{"бр.":"бр.","г":"г","кг":"кг","мл":"мл","л":"л","пакет":"пакет"};
    document.querySelectorAll("#product-form select[name='unit'] option").forEach(option=>{option.textContent=unitLabels[option.value]||option.value;});
    set(".bottom-action .recipes-button", "viewRecipes"); set(".bottom-action p", "recipeFooterProducts");
    if (document.querySelector("#recipes-title")) { document.title=`${t("recipes")} | ${t("brand")}`; set("#recipes-title", "recipes"); set(".recipes-heading .eyebrow", "recipeIdeas"); set(".recipes-heading .subtitle", "recipesSubtitle"); attr(".recipes-heading .product-count","aria-label","recipeCountLabel"); const rc=document.querySelector(".recipes-heading .product-count span"); if(rc)rc.textContent=t("recipeCount"); set(".recipe-search-button", "search"); attr("#recipe-search", "placeholder", "searchRecipe"); attr(".recipe-search-section", "aria-label", "recipeSearchArea"); attr(".recipe-modes", "aria-label", "recipeModes"); attr(".recipe-layout .category-tabs", "aria-label", "recipeCategories"); attr("#recipe-pagination", "aria-label", "recipePages");
      const modeKeys={pantry:"withMyProducts",all:"allRecipes",favorites:"favorites"}; document.querySelectorAll(".recipe-mode").forEach(b=>b.textContent=t(modeKeys[b.dataset.mode]));
      const catKeys=["all","breakfast","lunch","dinner","dessert"]; document.querySelectorAll(".recipe-layout .category-tab").forEach((b,i)=>{ if(catKeys[i]) b.firstChild.textContent=t(catKeys[i]); });
      const back=document.querySelector(".back-to-products"); if(back) back.textContent=t("products");
    }
    if (document.querySelector("#detail-title")) { document.title=`${t("recipes")} | ${t("brand")}`; set(".eyebrow", "excerpt"); set("#recipe-back", "recipes"); set("#favorite-button", "saveFavorite"); set("#add-missing-button", "addMissing"); set("#detail-language-note", "translationNote"); set("#ingredients-heading", "ingredients"); set("#steps-heading", "method"); set("#detail-source", "sourceRecipe"); }
    document.dispatchEvent(new CustomEvent("app-language-change", {detail:{language}}));
  };
  function setLanguage(code) { if(!['en','bg'].includes(code)) return; language=code; localStorage.setItem("appLanguage", code); apply(); }
  window.appLocale={ get language(){return language;}, t, setLanguage, apply };
  document.addEventListener("DOMContentLoaded", apply);
})();
