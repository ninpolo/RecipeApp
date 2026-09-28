using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace RecipeApp.Services;

public sealed class RecipeCatalogService
{
    private const string Provider = "Spoonacular";
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly ILogger<RecipeCatalogService> logger;
    private readonly IMemoryCache cache;

    public RecipeCatalogService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<RecipeCatalogService> logger,
        IMemoryCache cache)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.logger = logger;
        this.cache = cache;
        httpClient.Timeout = TimeSpan.FromSeconds(12);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["Spoonacular:ApiKey"]);

    public async Task<RecipeSearchPage> SearchAsync(
        IReadOnlyList<string> pantryIngredients,
        bool usePantry,
        string? query,
        string? category,
        int page,
        CancellationToken cancellationToken)
    {
        var apiKey = configuration["Spoonacular:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new RecipeCatalogException(
                "Липсва Spoonacular API key. Добави го в User Secrets, за да търсиш рецепти.");
        }

        var ingredients = pantryIngredients
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
        var normalizedQuery = query?.Trim();
        var normalizedCategory = category?.Trim();
        var selectedPage = usePantry ? 1 : Math.Clamp(page, 1, 76);
        var isSimplePantrySearch = usePantry &&
            ingredients.Length > 0 &&
            string.IsNullOrWhiteSpace(normalizedQuery) &&
            string.IsNullOrWhiteSpace(normalizedCategory);
        var cacheKey = $"recipes:{string.Join('|', ingredients)}:{usePantry}:{normalizedQuery}:{normalizedCategory}:{selectedPage}";
        if (cache.TryGetValue(cacheKey, out RecipeSearchPage? cached) && cached is not null)
        {
            return cached;
        }

        var results = isSimplePantrySearch
            ? ToSinglePage(await SearchByIngredientsAsync(ingredients, apiKey, cancellationToken))
            : await SearchComplexAsync(ingredients, usePantry, normalizedQuery, normalizedCategory, selectedPage, apiKey, cancellationToken);

        cache.Set(cacheKey, results, TimeSpan.FromMinutes(10));
        return results;
    }

    public async Task<RecipeDetails> GetDetailsAsync(int recipeId, CancellationToken cancellationToken)
    {
        if (recipeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recipeId));
        }

        var apiKey = configuration["Spoonacular:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new RecipeCatalogException(
                "Липсва Spoonacular API key. Добави го в User Secrets, за да отвориш рецептата.");
        }

        var cacheKey = $"recipe-details:{recipeId}";
        if (cache.TryGetValue(cacheKey, out RecipeDetails? cached) && cached is not null)
        {
            return cached;
        }

        var json = await SendAsync(
            $"/recipes/{recipeId}/information?includeNutrition=false",
            apiKey,
            cancellationToken);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var ingredients = ReadArray(root, "extendedIngredients")
            .Select(item => new RecipeIngredientInfo(
                ReadString(item, "name") ?? ReadString(item, "originalName") ?? "Продукт",
                ReadString(item, "original") ?? ReadString(item, "originalName") ?? ReadString(item, "name") ?? "Продукт",
                ReadDecimal(item, "amount") ?? 1m,
                ReadString(item, "unit") ?? string.Empty,
                ReadString(item, "image"),
                ReadInt(item, "id"),
                ReadArray(item, "meta").Any(meta => meta.ValueKind == JsonValueKind.String &&
                    meta.GetString()?.Contains("optional", StringComparison.OrdinalIgnoreCase) == true),
                false,
                CleanIngredientName(ReadString(item, "nameClean") ?? ReadString(item, "name")),
                ReadString(item, "aisle")))
            .ToArray();
        var steps = ReadSteps(root);
        var sourceUrl = ReadString(root, "sourceUrl") ?? ReadString(root, "spoonacularSourceUrl");
        var details = new RecipeDetails(
            ReadInt(root, "id") ?? recipeId,
            ReadString(root, "title") ?? "Рецепта",
            ReadString(root, "image"),
            ReadInt(root, "readyInMinutes"),
            ReadInt(root, "servings"),
            ReadString(root, "sourceName") ?? Provider,
            sourceUrl,
            ingredients,
            steps);

        cache.Set(cacheKey, details, TimeSpan.FromHours(1));
        return details;
    }

    private async Task<IReadOnlyList<RecipeSearchResult>> SearchByIngredientsAsync(
        IReadOnlyList<string> ingredients,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var query = $"ingredients={Uri.EscapeDataString(string.Join(',', ingredients))}&number=12&ranking=2&ignorePantry=true";
        var json = await SendAsync($"/recipes/findByIngredients?{query}", apiKey, cancellationToken);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Select(item => new RecipeSearchResult(
                ReadInt(item, "id") ?? 0,
                ReadString(item, "title") ?? "Рецепта",
                ReadString(item, "image"),
                null,
                ReadInt(item, "usedIngredientCount"),
                ReadInt(item, "missedIngredientCount"),
                null,
                null))
            .Where(recipe => recipe.Id > 0)
            .ToArray();
    }

    private static RecipeSearchPage ToSinglePage(IReadOnlyList<RecipeSearchResult> results) =>
        new(results, results.Count, 1, 12);

    private async Task<RecipeSearchPage> SearchComplexAsync(
        IReadOnlyList<string> pantryIngredients,
        bool usePantry,
        string? query,
        string? category,
        int page,
        string apiKey,
        CancellationToken cancellationToken)
    {
        const int pageSize = 12;
        var parameters = new List<string>
        {
            $"number={pageSize}",
            "addRecipeInformation=true",
            "fillIngredients=true",
            "instructionsRequired=true",
            "sort=popularity",
            "sortDirection=desc"
        };

        if (!string.IsNullOrWhiteSpace(query))
        {
            parameters.Add($"query={Uri.EscapeDataString(query)}");
        }

        var recipeType = GetRecipeType(category);
        if (recipeType is not null)
        {
            parameters.Add($"type={Uri.EscapeDataString(recipeType)}");
        }

        if (usePantry && pantryIngredients.Count > 0)
        {
            parameters.Add($"includeIngredients={Uri.EscapeDataString(string.Join(',', pantryIngredients))}");
        }
        else
        {
            parameters.Add($"offset={(page - 1) * pageSize}");
        }

        var json = await SendAsync($"/recipes/complexSearch?{string.Join('&', parameters)}", apiKey, cancellationToken);
        using var document = JsonDocument.Parse(json);
        if (!TryGetProperty(document.RootElement, "results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return new RecipeSearchPage([], 0, page, pageSize);
        }

        var recipes = results.EnumerateArray()
            .Select(item =>
            {
                var extendedIngredients = ReadArray(item, "extendedIngredients");
                int? usedCount = null;
                int? missedCount = null;
                if (usePantry && pantryIngredients.Count > 0)
                {
                    usedCount = extendedIngredients
                        .Count(recipeIngredient => pantryIngredients.Any(pantry => IngredientMatches(
                            pantry,
                            ReadString(recipeIngredient, "name") ?? ReadString(recipeIngredient, "original") ?? string.Empty)));
                    missedCount = Math.Max(0, extendedIngredients.Length - usedCount.Value);
                }

                return new RecipeSearchResult(
                    ReadInt(item, "id") ?? 0,
                    ReadString(item, "title") ?? "Рецепта",
                    ReadString(item, "image"),
                    ReadInt(item, "readyInMinutes"),
                    usedCount,
                    missedCount,
                    ReadString(item, "sourceName") ?? Provider,
                    ReadString(item, "sourceUrl") ?? ReadString(item, "spoonacularSourceUrl"));
            })
            .Where(recipe => recipe.Id > 0)
            .ToArray();
        var totalResults = ReadInt(document.RootElement, "totalResults") ?? recipes.Length;
        return new RecipeSearchPage(recipes, totalResults, page, pageSize);
    }

    private async Task<string> SendAsync(string pathAndQuery, string apiKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.spoonacular.com{pathAndQuery}");
        request.Headers.Add("x-api-key", apiKey);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "{Provider} recipe request returned HTTP {StatusCode}.",
                Provider,
                (int)response.StatusCode);
            throw new RecipeCatalogException(GetProviderErrorMessage(response.StatusCode));
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static string GetProviderErrorMessage(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            "Spoonacular не прие ключа. Провери API key в User Secrets.",
        HttpStatusCode.PaymentRequired or HttpStatusCode.TooManyRequests =>
            "Достигнат е лимитът на Spoonacular. Опитай отново по-късно.",
        _ => "Каталогът с рецепти временно не отговаря. Опитай отново след малко."
    };

    private static string? GetRecipeType(string? category) => category switch
    {
        "Закуска" => "breakfast",
        "Обяд" or "Вечеря" => "main course",
        "Десерт" => "dessert",
        "Салата" => "salad",
        _ => null
    };

    private static bool IngredientMatches(string pantryName, string recipeName)
    {
        var pantryWords = NormalizeIngredient(pantryName);
        var recipeWords = NormalizeIngredient(recipeName);
        return pantryWords.Length > 1 &&
            (pantryWords == recipeWords ||
             pantryWords.Contains(recipeWords, StringComparison.Ordinal) ||
             recipeWords.Contains(pantryWords, StringComparison.Ordinal));
    }

    private static string NormalizeIngredient(string value)
    {
        var words = value.ToLowerInvariant()
            .Split([' ', '-', '_', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => new string(word.Where(char.IsLetterOrDigit).ToArray()))
            .Where(word => word.Length > 0)
            .Select(word => word.Length > 4 && word.EndsWith("ies", StringComparison.Ordinal)
                ? word[..^3] + "y"
                : word.Length > 3 && word.EndsWith('s')
                    ? word[..^1]
                    : word);
        return string.Join(' ', words);
    }

    private static RecipeStepInfo[] ReadSteps(JsonElement root)
    {
        var steps = new List<RecipeStepInfo>();
        foreach (var instructionBlock in ReadArray(root, "analyzedInstructions"))
        {
            foreach (var step in ReadArray(instructionBlock, "steps"))
            {
                var text = ReadString(step, "step");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    steps.Add(new RecipeStepInfo(steps.Count + 1, text.Trim()));
                }
            }
        }

        if (steps.Count == 0)
        {
            var instructions = ReadString(root, "instructions");
            if (!string.IsNullOrWhiteSpace(instructions))
            {
                var plainText = System.Net.WebUtility.HtmlDecode(
                    System.Text.RegularExpressions.Regex.Replace(instructions, "<[^>]+>", " "));
                var paragraphs = plainText.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                steps.AddRange(paragraphs.Select((text, index) => new RecipeStepInfo(index + 1, text)));
            }
        }

        return steps.ToArray();
    }

    private static JsonElement[] ReadArray(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().ToArray()
            : [];

    private static string? ReadString(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.TryGetInt32(out var result)
            ? result
            : null;

    private static decimal? ReadDecimal(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.TryGetDecimal(out var result)
            ? result
            : null;

    private static string? CleanIngredientName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var name = System.Net.WebUtility.HtmlDecode(value).Trim();
        name = System.Text.RegularExpressions.Regex.Replace(
            name,
            @"\s+(?:from|in)\s+the\s+(?:(?:refrigerated|frozen|grocery|produce)\s+)?section\b.*$",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        name = System.Text.RegularExpressions.Regex.Replace(
            name,
            @"\s*[-–—]\s*(?:beat|beaten|chopped|minced|sliced|grated|diced|crushed|mashed|shredded|peeled)\b.*$",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        name = System.Text.RegularExpressions.Regex.Replace(
            name,
            @"^(?:block|can|package|bag|box|jar|bottle|clove|head|bunch|stalk|sprig)\s+(?:of\s+)?",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ").Trim(' ', ',', ';', '.');
        return name.Length == 0 ? value : name;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
        {
            return true;
        }
        value = default;
        return false;
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

