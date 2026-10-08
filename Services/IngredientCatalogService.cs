using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace RecipeApp.Services;

/// <summary>
/// Ingredient autocomplete backed by TheMealDB's free ingredient list (no API key, no daily limit).
/// The full list (~900 names) is downloaded once and cached, then searched locally.
/// </summary>
public sealed class IngredientCatalogService
{
    public const string Provider = "TheMealDB";
    private const string ListUrl = "https://www.themealdb.com/api/json/v1/1/list.php?i=list";
    private readonly HttpClient httpClient;
    private readonly IMemoryCache cache;
    private readonly ILogger<IngredientCatalogService> logger;

    public IngredientCatalogService(HttpClient httpClient, IMemoryCache cache, ILogger<IngredientCatalogService> logger)
    {
        this.httpClient = httpClient;
        this.cache = cache;
        this.logger = logger;
        httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<IReadOnlyList<MealDbIngredient>> GetAllAsync(CancellationToken cancellationToken)
    {
        var all = await cache.GetOrCreateAsync<IReadOnlyList<MealDbIngredient>>("mealdb:ingredients", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            using var response = await httpClient.GetAsync(ListUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);

            var list = new List<MealDbIngredient>();
            if (document.RootElement.TryGetProperty("meals", out var meals) && meals.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in meals.EnumerateArray())
                {
                    var name = ReadText(item, "strIngredient");
                    if (name is null || !int.TryParse(ReadText(item, "idIngredient"), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out var id))
                    {
                        continue;
                    }

                    list.Add(new MealDbIngredient(id, name));
                }
            }

            logger.LogInformation("Loaded {Count} ingredients from TheMealDB.", list.Count);
            return list;
        });

        return all ?? [];
    }

    public async Task<IReadOnlyList<IngredientSuggestion>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var term = query.Trim();
        if (term.Length < 2) return [];

        var all = await GetAllAsync(cancellationToken);
        return all
            .Where(item => item.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            .ThenBy(item => item.Name.Length)
            .Take(8)
            .Select(item => new IngredientSuggestion(
                item.Name,
                item.Id.ToString(CultureInfo.InvariantCulture),
                Provider,
                null))
            .ToList();
    }

    private static string? ReadText(JsonElement element, string property)
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
}

public sealed record IngredientSuggestion(string Name, string? CatalogItemId, string? CatalogProvider, string? Category);

public sealed record MealDbIngredient(int Id, string Name);
