using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Models;
using RecipeApp.Services;

namespace RecipeApp.Controllers;

[ApiController]
[Authorize]
[Route("api/recipes")]
public sealed class RecipesController(
    RecipeAppContext db,
    RecipeCatalogService recipeCatalog,
    RecipeTranslationService recipeTranslation) : ControllerBase
{
    private string UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string mode = "pantry",
        [FromQuery] string? query = null,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (mode is not ("pantry" or "all" or "favorites"))
        {
            return BadRequest(new { message = "Невалиден режим за търсене на рецепти." });
        }

        if (mode == "favorites")
        {
            var favorites = await db.RecipeFavorites
                .AsNoTracking()
                .Where(favorite => favorite.UserId == UserId && favorite.Recipe.SourceProvider == "TheMealDB" && favorite.Recipe.ProviderRecipeId != null)
                .Where(favorite => string.IsNullOrWhiteSpace(query) || favorite.Recipe.Name.Contains(query.Trim()))
                .OrderByDescending(favorite => favorite.CreatedAt)
                .Select(recipe => new
                {
                    recipe.Recipe.ProviderRecipeId,
                    recipe.Recipe.Name,
                    recipe.Recipe.ImageUrl,
                    recipe.Recipe.PreparationMinutes,
                    recipe.Recipe.SourceName,
                    recipe.Recipe.SourceUrl
                })
                .ToListAsync(cancellationToken);
            var results = favorites
                .Select(recipe => int.TryParse(recipe.ProviderRecipeId, out var id)
                    ? new RecipeSearchResult(id, recipe.Name, recipe.ImageUrl, recipe.PreparationMinutes, null, null,
                        recipe.SourceName, recipe.SourceUrl)
                    : null)
                .Where(recipe => recipe is not null)
                .Cast<RecipeSearchResult>()
                .ToArray();
            return Ok(new RecipeSearchPage(results, results.Length, 1, 12));
        }

        if (!recipeCatalog.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Каталогът с рецепти не е наличен."
            });
        }

        List<string> pantryIngredients = mode == "pantry"
            ? await db.InventoryItems
                .AsNoTracking()
                .Where(item => item.UserId == UserId && item.Quantity > 0)
                .Select(item => item.Product.Name)
                .Distinct()
                .ToListAsync(cancellationToken)
            : [];

        if (mode == "pantry" && pantryIngredients.Count == 0)
        {
            return Ok(new RecipeSearchPage([], 0, 1, 12));
        }

        try
        {
            var recipes = await recipeCatalog.SearchAsync(
                pantryIngredients,
                mode == "pantry",
                query,
                category,
                page,
                cancellationToken);
            return Ok(recipes);
        }
        catch (RecipeCatalogException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Не успяхме да се свържем с каталога с рецепти."
            });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                message = "Търсенето на рецепти отне твърде дълго. Опитай отново."
            });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetails(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "Невалиден номер на рецепта." });
        }

        if (!recipeCatalog.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Каталогът с рецепти не е наличен."
            });
        }

        try
        {
            var details = await recipeCatalog.GetDetailsAsync(id, cancellationToken);
            var pantryItems = await db.InventoryItems
                .AsNoTracking()
                .Where(item => item.UserId == UserId && item.Quantity > 0)
                .Select(item => new PantryProduct(
                    item.Product.Name,
                    item.Product.CatalogProvider,
                    item.Product.CatalogItemId))
                .ToListAsync(cancellationToken);
            var isFavorite = await db.RecipeFavorites.AnyAsync(favorite =>
                favorite.UserId == UserId && favorite.Recipe.SourceProvider == "TheMealDB" && favorite.Recipe.ProviderRecipeId == id.ToString(),
                cancellationToken);
            var ingredients = details.Ingredients
                .Select(ingredient => ingredient with
                {
                    IsInPantry = pantryItems.Any(item => PantryMatches(item, ingredient))
                })
                .ToArray();
            return Ok(details with { Ingredients = ingredients, IsFavorite = isFavorite });
        }
        catch (RecipeCatalogException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Не успяхме да заредим подробностите за рецептата."
            });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                message = "Зареждането на рецептата отне твърде дълго. Опитай отново."
            });
        }
    }

    [HttpGet("{id:int}/translation")]
    public async Task<IActionResult> GetBulgarianTranslation(int id, [FromQuery] string language = "bg",
        CancellationToken cancellationToken = default)
    {
        if (id <= 0 || language != "bg")
        {
            return BadRequest(new { message = "Невалидна заявка за превод на рецептата." });
        }

        try
        {
            var details = await recipeCatalog.GetDetailsAsync(id, cancellationToken);
            var providerId = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var sourceHash = recipeTranslation.GetSourceHash(details);
            var cached = await db.RecipeTranslationCache.AsNoTracking().FirstOrDefaultAsync(item =>
                item.ProviderRecipeId == providerId && item.Language == language && item.SourceHash == sourceHash,
                cancellationToken);
            if (cached is not null)
            {
                return Ok(new RecipeTranslationResult(
                    id,
                    cached.Title,
                    System.Text.Json.JsonSerializer.Deserialize<string[]>(cached.IngredientNamesJson) ?? [],
                    System.Text.Json.JsonSerializer.Deserialize<string[]>(cached.IngredientUnitsJson) ?? [],
                    System.Text.Json.JsonSerializer.Deserialize<string[]>(cached.StepInstructionsJson) ?? []));
            }

            var translation = await recipeTranslation.TranslateToBulgarianAsync(details, cancellationToken);
            db.RecipeTranslationCache.Add(new RecipeTranslationCache
            {
                ProviderRecipeId = providerId,
                Language = language,
                SourceHash = sourceHash,
                Title = translation.Title,
                IngredientNamesJson = System.Text.Json.JsonSerializer.Serialize(translation.IngredientNames),
                IngredientUnitsJson = System.Text.Json.JsonSerializer.Serialize(translation.IngredientUnits),
                StepInstructionsJson = System.Text.Json.JsonSerializer.Serialize(translation.StepInstructions)
            });
            await db.SaveChangesAsync(cancellationToken);
            return Ok(translation);
        }
        catch (RecipeCatalogException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
        catch (RecipeTranslationException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Не успяхме да се свържем с услугата за превод. Оригиналната рецепта остава достъпна."
            });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                message = "Преводът отне твърде дълго. Опитай отново, оригиналната рецепта остава достъпна."
            });
        }
    }

    [HttpPost("translate-titles")]
    public async Task<IActionResult> TranslateTitles([FromBody] RecipeTitlesRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Language != "bg" || request.Titles is null || request.Titles.Count > 20 ||
            request.Titles.Any(item => item.Id <= 0 || string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > 300))
        {
            return BadRequest(new { message = "Невалидна заявка за превод на заглавия." });
        }

        try
        {
            var translations = new string?[request.Titles.Count];
            var sourceHashes = request.Titles.Select(item => Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                    "recipe-title-v1\n" + item.Title)))).ToArray();
            var needsTranslation = new List<int>();
            for (var index = 0; index < request.Titles.Count; index++)
            {
                var providerId = request.Titles[index].Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var cached = await db.RecipeTranslationCache.AsNoTracking().FirstOrDefaultAsync(item =>
                    item.ProviderRecipeId == providerId && item.Language == "bg" && item.SourceHash == sourceHashes[index],
                    cancellationToken);
                if (cached is null) needsTranslation.Add(index);
                else translations[index] = cached.Title;
            }

            var newTranslations = await Task.WhenAll(needsTranslation.Select(index =>
                recipeTranslation.TranslateTextToBulgarianAsync(request.Titles[index].Title, cancellationToken)));
            for (var offset = 0; offset < needsTranslation.Count; offset++)
            {
                var index = needsTranslation[offset];
                var item = request.Titles[index];
                translations[index] = newTranslations[offset];
                db.RecipeTranslationCache.Add(new RecipeTranslationCache
                {
                    ProviderRecipeId = item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Language = "bg",
                    SourceHash = sourceHashes[index],
                    Title = newTranslations[offset]
                });
            }
            if (needsTranslation.Count > 0) await db.SaveChangesAsync(cancellationToken);
            return Ok(new { titles = translations });
        }
        catch (RecipeTranslationException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Не успяхме да се свържем с услугата за превод."
            });
        }
    }

    public sealed record RecipeTitlesRequest(string Language, List<RecipeTitleRequestItem> Titles);
    public sealed record RecipeTitleRequestItem(int Id, string Title);

    [HttpPost("{id:int}/favorite")]
    public async Task<IActionResult> SaveFavorite(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "Невалиден номер на рецепта." });
        }

        try
        {
            var details = await recipeCatalog.GetDetailsAsync(id, cancellationToken);
            var providerId = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var favorite = await db.Recipes.SingleOrDefaultAsync(recipe =>
                recipe.SourceProvider == "TheMealDB" && recipe.ProviderRecipeId == providerId,
                cancellationToken);
            if (favorite is null)
            {
                favorite = new Recipe
                {
                    Name = details.Title[..Math.Min(details.Title.Length, 140)],
                    Source = "External",
                    SourceProvider = "TheMealDB",
                    ProviderRecipeId = providerId,
                    SourceName = details.SourceName,
                    SourceUrl = Limit(details.SourceUrl, 2048),
                    ImageUrl = Limit(details.ImageUrl, 2048),
                    PreparationMinutes = details.ReadyInMinutes,
                    IsSaved = false
                };
                db.Recipes.Add(favorite);
            }
            else
            {
                favorite.Name = details.Title[..Math.Min(details.Title.Length, 140)];
                favorite.SourceName = details.SourceName;
                favorite.SourceUrl = Limit(details.SourceUrl, 2048);
                favorite.ImageUrl = Limit(details.ImageUrl, 2048);
                favorite.PreparationMinutes = details.ReadyInMinutes;
            }

            await db.SaveChangesAsync(cancellationToken);
            if (!await db.RecipeFavorites.AnyAsync(item => item.UserId == UserId && item.RecipeId == favorite.RecipeId, cancellationToken))
                db.RecipeFavorites.Add(new RecipeFavorite { UserId = UserId, RecipeId = favorite.RecipeId });
            await db.SaveChangesAsync(cancellationToken);
            return Ok(new { isFavorite = true });
        }
        catch (RecipeCatalogException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Не успяхме да запазим рецептата." });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = "Запазването на рецептата отне твърде дълго." });
        }
    }

    [HttpDelete("{id:int}/favorite")]
    public async Task<IActionResult> RemoveFavorite(int id, CancellationToken cancellationToken)
    {
        var providerId = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var favorite = await db.RecipeFavorites.SingleOrDefaultAsync(item =>
            item.UserId == UserId && item.Recipe.SourceProvider == "TheMealDB" && item.Recipe.ProviderRecipeId == providerId,
            cancellationToken);
        if (favorite is not null)
        {
            db.RecipeFavorites.Remove(favorite);
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpPost("{id:int}/add-missing")]
    public async Task<IActionResult> AddMissingIngredients(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "Невалиден номер на рецепта." });
        }

        try
        {
            var details = await recipeCatalog.GetDetailsAsync(id, cancellationToken);
            var pantryItems = await db.InventoryItems
                .AsNoTracking()
                .Where(item => item.UserId == UserId && item.Quantity > 0)
                .Select(item => new PantryProduct(
                    item.Product.Name,
                    item.Product.CatalogProvider,
                    item.Product.CatalogItemId))
                .ToListAsync(cancellationToken);
            var missing = details.Ingredients
                .Where(ingredient => !ingredient.IsOptional &&
                    !pantryItems.Any(item => PantryMatches(item, ingredient)))
                .GroupBy(ingredient => ingredient.CatalogItemId?.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    ?? NormalizeFoodName(ingredient.CatalogName ?? ingredient.Name), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
            if (missing.Length == 0)
            {
                return Ok(new { addedCount = 0, addedProducts = Array.Empty<string>(), message = "Всички нужни продукти вече са в списъка." });
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var addedProducts = new List<string>();
            var categories = await db.ProductCategories
                .AsNoTracking()
                .ToDictionaryAsync(category => category.Name, category => category.ProductCategoryId,
                    StringComparer.OrdinalIgnoreCase, cancellationToken);
            foreach (var ingredient in missing)
            {
                var name = CleanCatalogName(ingredient.CatalogName ?? ingredient.Name);
                if (name.Length == 0) continue;
                name = name[..Math.Min(name.Length, 100)];
                ProductCatalogItem? product = null;
                if (ingredient.CatalogItemId is int externalId)
                {
                    product = await db.ProductCatalog.FirstOrDefaultAsync(item =>
                        item.CatalogProvider == "TheMealDB" && item.CatalogItemId == externalId.ToString(), cancellationToken);
                }
                product ??= await db.ProductCatalog.FirstOrDefaultAsync(item => item.Name == name, cancellationToken);
                var mappedCategoryId = ResolveCategoryId(ingredient.Aisle, name, categories);
                if (product is null)
                {
                    product = new ProductCatalogItem
                    {
                        Name = name,
                        CatalogProvider = ingredient.CatalogItemId is null ? null : "TheMealDB",
                        CatalogItemId = ingredient.CatalogItemId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ProductCategoryId = mappedCategoryId
                    };
                    db.ProductCatalog.Add(product);
                    await db.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    product.ProductCategoryId ??= mappedCategoryId;
                    if (product.CatalogProvider is null && ingredient.CatalogItemId is not null)
                    {
                        product.CatalogProvider = "TheMealDB";
                        product.CatalogItemId = ingredient.CatalogItemId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }
                }

                var unit = string.IsNullOrWhiteSpace(ingredient.Unit) ? "бр." : ingredient.Unit.Trim();
                unit = unit[..Math.Min(unit.Length, 20)];
                var quantity = ingredient.Amount <= 0 ? 1m : Math.Min(decimal.Round(ingredient.Amount, 2), 99_999_999.99m);
                var inventoryItem = await db.InventoryItems.FirstOrDefaultAsync(item =>
                    item.UserId == UserId && item.ProductId == product.ProductId && item.Unit == unit,
                    cancellationToken);
                if (inventoryItem is null)
                {
                    db.InventoryItems.Add(new InventoryItem
                    {
                        ProductId = product.ProductId,
                        UserId = UserId,
                        Quantity = quantity,
                        Unit = unit
                    });
                }
                else
                {
                    inventoryItem.Quantity = Math.Min(inventoryItem.Quantity + quantity, 99_999_999.99m);
                }
                addedProducts.Add(product.Name);
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(new { addedCount = addedProducts.Count, addedProducts });
        }
        catch (RecipeCatalogException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Не успяхме да добавим липсващите продукти." });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = "Добавянето на продуктите отне твърде дълго." });
        }
    }

    private static bool FoodNameMatches(string pantryName, string recipeName)
    {
        var pantry = NormalizeFoodName(pantryName);
        var ingredient = NormalizeFoodName(recipeName);
        return pantry.Length > 1 && ingredient.Length > 1 &&
            (pantry == ingredient || $" {pantry} ".Contains($" {ingredient} ", StringComparison.Ordinal) ||
             $" {ingredient} ".Contains($" {pantry} ", StringComparison.Ordinal));
    }

    private static bool PantryMatches(PantryProduct product, RecipeIngredientInfo ingredient)
    {
        if (ingredient.CatalogItemId is int ingredientId &&
            product.CatalogProvider == "TheMealDB" &&
            int.TryParse(product.CatalogItemId, out var productId) && productId == ingredientId)
        {
            return true;
        }

        var catalogName = ingredient.CatalogName;
        return FoodNameMatches(product.Name, ingredient.Name) ||
            (!string.IsNullOrWhiteSpace(catalogName) && FoodNameMatches(product.Name, catalogName));
    }

    private static string NormalizeFoodName(string value)
    {
        var words = value.ToLowerInvariant()
            .Split([' ', '-', '_', ',', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => new string(word.Where(char.IsLetterOrDigit).ToArray()))
            .Where(word => word.Length > 0)
            .Select(word => word.Length > 4 && word.EndsWith("ies", StringComparison.Ordinal)
                ? word[..^3] + "y"
                : word.Length > 4 && word.EndsWith("oes", StringComparison.Ordinal)
                    ? word[..^2]
                    : word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal)
                        ? word[..^1]
                        : word);
        return string.Join(' ', words);
    }

    private static string CleanCatalogName(string value)
    {
        var cleaned = System.Net.WebUtility.HtmlDecode(value).Trim();
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ");
        return cleaned.Length > 0 ? char.ToLowerInvariant(cleaned[0]) + cleaned[1..] : string.Empty;
    }

    private static int? ResolveCategoryId(
        string? aisle,
        string ingredientName,
        IReadOnlyDictionary<string, int> categories)
    {
        var aisleText = (aisle ?? string.Empty).ToLowerInvariant();
        var name = NormalizeFoodName(ingredientName);
        string? category = null;

        if (aisleText.Contains("dairy") || aisleText.Contains("milk") || aisleText.Contains("cheese") ||
            aisleText.Contains("yogurt") || aisleText.Contains("egg"))
        {
            category = "Млечни";
        }
        else if (aisleText.Contains("meat") || aisleText.Contains("seafood") || aisleText.Contains("fish") ||
                 aisleText.Contains("poultry"))
        {
            category = "Месо и риба";
        }
        else if (aisleText.Contains("spice") || aisleText.Contains("seasoning") || aisleText.Contains("herb"))
        {
            category = "Подправки";
        }
        else if (aisleText.Contains("fruit"))
        {
            category = "Плодове";
        }
        else if (aisleText.Contains("vegetable"))
        {
            category = "Зеленчуци";
        }
        else if (aisleText.Contains("produce"))
        {
            if (ContainsAny(name, "apple", "banana", "orange", "lemon", "lime", "berry", "berries", "grape", "melon", "mango", "pineapple", "pear", "peach", "plum", "avocado", "cherry"))
                category = "Плодове";
            else if (ContainsAny(name, "tomato", "carrot", "potato", "cucumber", "onion", "garlic", "pepper", "lettuce", "spinach", "broccoli", "cabbage", "celery", "zucchini", "eggplant", "squash", "corn", "mushroom", "pea", "asparagus"))
                category = "Зеленчуци";
        }
        else if (ContainsAny(aisleText, "baking", "bakery", "cereal", "pasta", "grain", "rice", "canned", "oil", "vinegar", "nut", "legume", "bean", "condiment", "sauce") ||
                 (aisleText.Contains("refrigerated") && ContainsAny(name, "dough", "roll", "crescent", "pastry", "biscuit")))
        {
            category = "Основни продукти";
        }

        if (category is null && !string.IsNullOrWhiteSpace(aisle)) category = "Други";
        return category is not null && categories.TryGetValue(category, out var categoryId) ? categoryId : null;
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.Ordinal));

    private static string? Limit(string? value, int length) =>
        string.IsNullOrEmpty(value) ? value : value[..Math.Min(value.Length, length)];

    private sealed record PantryProduct(string Name, string? CatalogProvider, string? CatalogItemId);
}

