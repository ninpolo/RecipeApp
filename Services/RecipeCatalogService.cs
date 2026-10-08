using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace RecipeApp.Services;

/// <summary>
/// Recipe catalog backed by TheMealDB (free, no API key, no daily quota).
/// TheMealDB has no "list everything" or multi-ingredient endpoint on the free tier, so this
/// service builds those features on top of its category / ingredient / name endpoints and
/// caches everything in memory.
/// </summary>
public sealed class RecipeCatalogService
{
    public const string Provider = "TheMealDB";
    private const string BaseUrl = "https://www.themealdb.com/api/json/v1/1/";
    private const int PageSize = 12;

    // Be polite to the free API: never more than 6 requests in flight at once.
    private static readonly SemaphoreSlim RequestGate = new(6);

    private static readonly string[] AllCategories =
    [
        "Beef", "Breakfast", "Chicken", "Dessert", "Goat", "Lamb", "Miscellaneous",
        "Pasta", "Pork", "Seafood", "Side", "Starter", "Vegan", "Vegetarian"
    ];

    // The app's tabs (Bulgarian) -> TheMealDB categories.
    private static readonly Dictionary<string, string[]> CategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Закуска"] = ["Breakfast"],
        ["Обяд"] = ["Starter", "Side", "Pasta", "Vegetarian", "Vegan", "Miscellaneous"],
        ["Вечеря"] = ["Beef", "Chicken", "Lamb", "Pork", "Goat", "Seafood", "Pasta"],
        ["Десерт"] = ["Dessert"]
    };

    private static readonly Regex WordRegex = new(@"[\p{L}\p{Nd}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex QuantityRegex = new(
        @"^\s*(?:(?<whole>\d+)\s+(?<n1>\d+)/(?<d1>\d+)|(?<n2>\d+)/(?<d2>\d+)|(?<dec>\d+(?:[.,]\d+)?))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StepPrefixRegex = new(
        @"^\s*(?:step\s*\d+\s*[:.\-)]?|\d+\s*[.)])\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly HttpClient httpClient;
    private readonly IngredientCatalogService ingredients;
    private readonly IMemoryCache cache;
    private readonly ILogger<RecipeCatalogService> logger;

    public RecipeCatalogService(
        HttpClient httpClient,
        IngredientCatalogService ingredients,
        ILogger<RecipeCatalogService> logger,
        IMemoryCache cache)
    {
        this.httpClient = httpClient;
        this.ingredients = ingredients;
        this.logger = logger;
        this.cache = cache;
        httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    // TheMealDB needs no key, so the catalog is always available.
    public bool IsConfigured => true;

    public async Task<RecipeSearchPage> SearchAsync(
        IReadOnlyList<string> pantryIngredients,
        bool usePantry,
        string? query,
        string? category,
        int page,
        CancellationToken cancellationToken)
    {
        var pantry = pantryIngredients
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
        var text = query?.Trim();
        var selectedPage = Math.Max(1, page);

        IReadOnlyList<Meal> candidates;
        if (!string.IsNullOrWhiteSpace(text))
        {
            candidates = await SearchByTextAsync(text, cancellationToken);
        }
        else if (usePantry && pantry.Length > 0)
        {
            candidates = await SearchByPantryAsync(pantry, cancellationToken);
        }
        else
        {
            candidates = (await GetAllMealsAsync(cancellationToken))
                .OrderBy(meal => meal.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var categoryIds = await GetCategoryIdsAsync(category, cancellationToken);
        if (categoryIds is not null)
        {
            candidates = candidates.Where(meal => categoryIds.Contains(meal.Id)).ToList();
        }

        var total = candidates.Count;
        var pageItems = candidates.Skip((selectedPage - 1) * PageSize).Take(PageSize).ToArray();
        var pantryForCounts = usePantry && pantry.Length > 0 ? pantry : null;
        var results = await Task.WhenAll(
            pageItems.Select(meal => ToResultAsync(meal, pantryForCounts, cancellationToken)));
        return new RecipeSearchPage(results, total, selectedPage, PageSize);
    }

    public async Task<RecipeDetails> GetDetailsAsync(int recipeId, CancellationToken cancellationToken)
    {
        if (recipeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recipeId));
        }

        var cacheKey = $"mealdb:details:{recipeId.ToString(CultureInfo.InvariantCulture)}";
        if (cache.TryGetValue(cacheKey, out RecipeDetails? cached) && cached is not null)
        {
            return cached;
        }

        using var document = await GetJsonAsync($"lookup.php?i={recipeId.ToString(CultureInfo.InvariantCulture)}", cancellationToken);
        var meal = ReadMeals(document.RootElement).FirstOrDefault();
        if (meal.ValueKind != JsonValueKind.Object)
        {
            throw new RecipeCatalogException("Рецептата не е намерена.");
        }

        // Map ingredient names to TheMealDB ingredient ids (best effort).
        var idByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var item in await ingredients.GetAllAsync(cancellationToken))
            {
                idByName.TryAdd(item.Name, item.Id);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                                          exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning("Could not load the ingredient list; continuing without ingredient ids.");
        }

        var ingredientInfos = new List<RecipeIngredientInfo>();
        for (var index = 1; index <= 20; index++)
        {
            var name = ReadString(meal, $"strIngredient{index}");
            if (name is null) continue;

            var measure = ReadString(meal, $"strMeasure{index}") ?? string.Empty;
            var (amount, unit) = ParseMeasure(measure);
            int? catalogId = idByName.TryGetValue(name, out var id) ? id : null;
            ingredientInfos.Add(new RecipeIngredientInfo(
                name,
                measure.Length == 0 ? name : $"{measure} {name}",
                amount,
                unit,
                $"https://www.themealdb.com/images/ingredients/{Uri.EscapeDataString(name)}-Small.png",
                catalogId,
                false,
                false,
                name,
                null));
        }

        var details = new RecipeDetails(
            recipeId,
            Truncate(ReadString(meal, "strMeal") ?? "Рецепта", 140),
            ReadString(meal, "strMealThumb"),
            null,
            null,
            Provider,
            ReadString(meal, "strSource"),
            ingredientInfos,
            ParseSteps(ReadString(meal, "strInstructions")));
        cache.Set(cacheKey, details, TimeSpan.FromHours(24));
        return details;
    }

    // ------------------------------------------------------------------ searching

    private async Task<IReadOnlyList<Meal>> SearchByTextAsync(string text, CancellationToken cancellationToken)
    {
        var words = Words(text);
        if (words.Length == 0) return [];

        // 1) Recipes whose TITLE is about what was typed ("eggs" -> egg dishes, not every
        //    cake that happens to contain an egg). "eggplant" does not count as "egg".
        var searchTerms = new List<string> { text };
        var normalizedText = string.Join(' ', words);
        if (!string.Equals(normalizedText, text.ToLowerInvariant(), StringComparison.Ordinal))
        {
            searchTerms.Add(normalizedText);
        }

        var byName = new List<Meal>();
        foreach (var term in searchTerms)
        {
            byName.AddRange(await SearchByNameAsync(term, cancellationToken));
        }

        var titleMatches = byName
            .GroupBy(meal => meal.Id)
            .Select(group => group.First())
            .Where(meal =>
            {
                var titleWords = Words(meal.Title);
                return words.All(word => titleWords.Contains(word));
            })
            .ToList();
        if (titleMatches.Count > 0)
        {
            return titleMatches;
        }

        // 2) No title contains it, so fall back to recipes that use it as an ingredient.
        var matchingIngredients = (await ingredients.GetAllAsync(cancellationToken))
            .Where(item =>
            {
                var itemWords = Words(item.Name);
                return words.All(word => itemWords.Contains(word));
            })
            .OrderBy(item => Words(item.Name).Length)
            .Take(3)
            .ToList();

        var meals = new List<Meal>();
        foreach (var item in matchingIngredients)
        {
            meals.AddRange(await FilterByIngredientAsync(item.Name, cancellationToken));
        }

        return meals
            .GroupBy(meal => meal.Id)
            .Select(group => group.First())
            .OrderBy(meal => meal.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<Meal>> SearchByPantryAsync(string[] pantry, CancellationToken cancellationToken)
    {
        var allIngredients = await ingredients.GetAllAsync(cancellationToken);

        // For every pantry product, find recipes that use it. A recipe's score is how many
        // different pantry products it uses.
        var perProduct = await Task.WhenAll(pantry.Select(async product =>
        {
            var words = Words(product);
            if (words.Length == 0) return Array.Empty<Meal>();

            var names = allIngredients
                .Where(item =>
                {
                    var itemWords = Words(item.Name);
                    return words.All(word => itemWords.Contains(word));
                })
                .OrderBy(item => Words(item.Name).Length)
                .Take(2)
                .Select(item => item.Name)
                .ToArray();

            var meals = new List<Meal>();
            foreach (var name in names)
            {
                meals.AddRange(await FilterByIngredientAsync(name, cancellationToken));
            }

            return meals.GroupBy(meal => meal.Id).Select(group => group.First()).ToArray();
        }));

        var scores = new Dictionary<int, (Meal Item, int Score)>();
        foreach (var meals in perProduct)
        {
            foreach (var meal in meals)
            {
                scores[meal.Id] = scores.TryGetValue(meal.Id, out var existing)
                    ? (meal, existing.Score + 1)
                    : (meal, 1);
            }
        }

        return scores.Values
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Item.Title, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Item)
            .ToList();
    }

    private async Task<RecipeSearchResult> ToResultAsync(Meal meal, string[]? pantry, CancellationToken cancellationToken)
    {
        int? used = null;
        int? missed = null;
        if (pantry is not null)
        {
            try
            {
                var details = await GetDetailsAsync(meal.Id, cancellationToken);
                var total = details.Ingredients.Count;
                var matched = details.Ingredients.Count(ingredient => pantry.Any(product => NamesMatch(product, ingredient.Name)));
                used = matched;
                missed = total - matched;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                                              exception is RecipeCatalogException or HttpRequestException or TaskCanceledException)
            {
                logger.LogWarning("Could not count pantry ingredients for recipe {RecipeId}.", meal.Id);
            }
        }

        return new RecipeSearchResult(meal.Id, meal.Title, meal.ImageUrl, null, used, missed, Provider, null);
    }

    // ------------------------------------------------------------------ data access

    private async Task<IReadOnlyList<Meal>> GetAllMealsAsync(CancellationToken cancellationToken)
    {
        var all = await cache.GetOrCreateAsync<IReadOnlyList<Meal>>("mealdb:all", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
            var lists = await Task.WhenAll(AllCategories.Select(name => FilterByCategoryAsync(name, cancellationToken)));
            return lists.SelectMany(list => list).GroupBy(meal => meal.Id).Select(group => group.First()).ToList();
        });
        return all ?? [];
    }

    private async Task<HashSet<int>?> GetCategoryIdsAsync(string? category, CancellationToken cancellationToken)
    {
        var selected = category?.Trim();
        if (string.IsNullOrWhiteSpace(selected)) return null;

        var names = CategoryMap.TryGetValue(selected, out var mapped)
            ? mapped
            : AllCategories.Where(name => string.Equals(name, selected, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (names.Length == 0) return null;

        var lists = await Task.WhenAll(names.Select(name => FilterByCategoryAsync(name, cancellationToken)));
        return lists.SelectMany(list => list).Select(meal => meal.Id).ToHashSet();
    }

    private Task<IReadOnlyList<Meal>> FilterByCategoryAsync(string category, CancellationToken cancellationToken) =>
        CachedMealsAsync(
            $"mealdb:cat:{category.ToLowerInvariant()}",
            TimeSpan.FromHours(12),
            $"filter.php?c={Uri.EscapeDataString(category)}",
            cancellationToken);

    private Task<IReadOnlyList<Meal>> FilterByIngredientAsync(string ingredient, CancellationToken cancellationToken) =>
        CachedMealsAsync(
            $"mealdb:ing:{ingredient.ToLowerInvariant()}",
            TimeSpan.FromHours(12),
            $"filter.php?i={Uri.EscapeDataString(ingredient.Replace(' ', '_'))}",
            cancellationToken);

    private Task<IReadOnlyList<Meal>> SearchByNameAsync(string term, CancellationToken cancellationToken) =>
        CachedMealsAsync(
            $"mealdb:name:{term.ToLowerInvariant()}",
            TimeSpan.FromHours(6),
            $"search.php?s={Uri.EscapeDataString(term)}",
            cancellationToken);

    private async Task<IReadOnlyList<Meal>> CachedMealsAsync(
        string cacheKey,
        TimeSpan lifetime,
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        var meals = await cache.GetOrCreateAsync<IReadOnlyList<Meal>>(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = lifetime;
            using var document = await GetJsonAsync(relativeUrl, cancellationToken);
            return ReadMeals(document.RootElement).Select(ToMeal).OfType<Meal>().ToList();
        });
        return meals ?? [];
    }

    private async Task<JsonDocument> GetJsonAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        await RequestGate.WaitAsync(cancellationToken);
        try
        {
            using var response = await httpClient.GetAsync(BaseUrl + relativeUrl, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new RecipeCatalogException("Каталогът с рецепти е претоварен. Опитай отново след малко.");
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("TheMealDB returned HTTP {StatusCode} for {Url}.", (int)response.StatusCode, relativeUrl);
                throw new RecipeCatalogException("Каталогът с рецепти не отговори. Опитай отново.");
            }

            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                // TheMealDB sometimes answers with an empty body instead of {"meals":null}.
                return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{\"meals\":null}" : text);
            }
            catch (JsonException)
            {
                return JsonDocument.Parse("{\"meals\":null}");
            }
        }
        finally
        {
            RequestGate.Release();
        }
    }

    // ------------------------------------------------------------------ helpers

    private sealed record Meal(int Id, string Title, string? ImageUrl);

    private static List<JsonElement> ReadMeals(JsonElement root)
    {
        if (root.TryGetProperty("meals", out var meals) && meals.ValueKind == JsonValueKind.Array)
        {
            return meals.EnumerateArray().ToList();
        }

        return [];
    }

    private static Meal? ToMeal(JsonElement element)
    {
        if (!int.TryParse(ReadString(element, "idMeal"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0)
        {
            return null;
        }

        var title = ReadString(element, "strMeal");
        return string.IsNullOrWhiteSpace(title) ? null : new Meal(id, Truncate(title, 140), ReadString(element, "strMealThumb"));
    }

    private static string? ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    // Lower-cased words with a naive singular form: "Eggs" -> "egg", "Tomatoes" -> "tomato".
    private static string[] Words(string value) =>
        WordRegex.Matches(value.ToLowerInvariant())
            .Select(match => Singular(match.Value))
            .ToArray();

    private static string Singular(string word)
    {
        if (word.Length > 4 && word.EndsWith("ies", StringComparison.Ordinal)) return word[..^3] + "y";
        if (word.Length > 4 && word.EndsWith("oes", StringComparison.Ordinal)) return word[..^2];
        if (word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal)) return word[..^1];
        return word;
    }

    // "chicken" matches "Chicken Breast"; "egg" matches "Eggs".
    private static bool NamesMatch(string pantryProduct, string recipeIngredient)
    {
        var product = Words(pantryProduct);
        var ingredient = Words(recipeIngredient);
        if (product.Length == 0 || ingredient.Length == 0) return false;
        return product.All(word => ingredient.Contains(word)) || ingredient.All(word => product.Contains(word));
    }

    private static (decimal Amount, string Unit) ParseMeasure(string measure)
    {
        var trimmed = measure.Trim();
        var match = QuantityRegex.Match(trimmed);
        if (!match.Success)
        {
            return (1m, Truncate(trimmed, 20));
        }

        decimal amount;
        if (match.Groups["whole"].Success)
        {
            var denominator = ToNumber(match.Groups["d1"].Value);
            amount = ToNumber(match.Groups["whole"].Value) +
                     (denominator > 0 ? ToNumber(match.Groups["n1"].Value) / denominator : 0m);
        }
        else if (match.Groups["n2"].Success)
        {
            var denominator = ToNumber(match.Groups["d2"].Value);
            amount = denominator > 0 ? ToNumber(match.Groups["n2"].Value) / denominator : 0m;
        }
        else
        {
            amount = ToNumber(match.Groups["dec"].Value);
        }

        var unit = trimmed[match.Length..].Trim();
        return (amount > 0 ? Math.Round(amount, 2) : 1m, Truncate(unit, 20));
    }

    private static decimal ToNumber(string value) =>
        decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? number
            : 0m;

    private static List<RecipeStepInfo> ParseSteps(string? instructions)
    {
        if (string.IsNullOrWhiteSpace(instructions)) return [];

        var lines = instructions
            .Replace("\r", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => StepPrefixRegex.Replace(line, string.Empty).Trim())
            .Where(line => line.Length > 0)
            .ToList();

        // Some recipes are one giant paragraph: group sentences into readable steps.
        if (lines.Count == 1 && lines[0].Length > 350)
        {
            lines = GroupSentences(lines[0]);
        }

        var steps = new List<RecipeStepInfo>();
        foreach (var line in lines)
        {
            foreach (var chunk in SplitLongText(line, 900))
            {
                steps.Add(new RecipeStepInfo(steps.Count + 1, chunk));
            }
        }

        return steps;
    }

    private static List<string> GroupSentences(string text)
    {
        var groups = new List<string>();
        var current = string.Empty;
        foreach (var sentence in Regex.Split(text, @"(?<=[.!?])\s+"))
        {
            if (string.IsNullOrWhiteSpace(sentence)) continue;
            current = current.Length == 0 ? sentence : $"{current} {sentence}";
            if (current.Length >= 250)
            {
                groups.Add(current);
                current = string.Empty;
            }
        }

        if (current.Length > 0) groups.Add(current);
        return groups;
    }

    // The database stores a step in at most 1000 characters.
    private static IEnumerable<string> SplitLongText(string text, int maxLength)
    {
        while (text.Length > maxLength)
        {
            var cut = text.LastIndexOf(' ', maxLength);
            if (cut <= 0) cut = maxLength;
            yield return text[..cut].Trim();
            text = text[cut..].Trim();
        }

        if (text.Length > 0) yield return text;
    }
}

public sealed class RecipeCatalogException(string message) : Exception(message);

public sealed record RecipeSearchResult(
    int Id,
    string Title,
    string? ImageUrl,
    int? ReadyInMinutes,
    int? UsedIngredientCount,
    int? MissedIngredientCount,
    string? SourceName,
    string? SourceUrl);

public sealed record RecipeSearchPage(
    IReadOnlyList<RecipeSearchResult> Results,
    int TotalResults,
    int Page,
    int PageSize);

public sealed record RecipeDetails(
    int Id,
    string Title,
    string? ImageUrl,
    int? ReadyInMinutes,
    int? Servings,
    string SourceName,
    string? SourceUrl,
    IReadOnlyList<RecipeIngredientInfo> Ingredients,
    IReadOnlyList<RecipeStepInfo> Steps,
    bool IsFavorite = false);

public sealed record RecipeIngredientInfo(
    string Name,
    string Original,
    decimal Amount,
    string Unit,
    string? ImageUrl,
    int? CatalogItemId,
    bool IsOptional,
    bool IsInPantry = false,
    string? CatalogName = null,
    string? Aisle = null);

public sealed record RecipeStepInfo(int Number, string Instruction);
