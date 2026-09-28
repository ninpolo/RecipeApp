using System.Text.Json;
using System.Text.Json.Serialization;

namespace RecipeApp.Services;

public sealed class IngredientCatalogService
{
    private const string Provider = "Spoonacular";
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly ILogger<IngredientCatalogService> logger;

    public IngredientCatalogService(HttpClient httpClient, IConfiguration configuration, ILogger<IngredientCatalogService> logger)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.logger = logger;
        httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    public async Task<IReadOnlyList<IngredientSuggestion>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var apiKey = configuration["Spoonacular:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Spoonacular API key is not configured. Showing only local product suggestions.");
            return [];
        }

        var url = "https://api.spoonacular.com/food/ingredients/autocomplete" +
                  $"?query={Uri.EscapeDataString(query)}&number=8&language=en";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", apiKey);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Spoonacular ingredient autocomplete returned HTTP {StatusCode}.", (int)response.StatusCode);
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var results = await JsonSerializer.DeserializeAsync<List<SpoonacularIngredient>>(stream, cancellationToken: cancellationToken);
        var suggestions = results?
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => new IngredientSuggestion(
                item.Name!,
                item.Id > 0 ? item.Id.ToString() : null,
                Provider,
                item.Aisle))
            .ToList() ?? [];
        logger.LogInformation(
            "Spoonacular response contained {RawCount} items; {SuggestionCount} had a usable name and {CatalogIdCount} had an ID.",
            results?.Count ?? 0,
            suggestions.Count,
            suggestions.Count(item => item.CatalogItemId is not null));
        return suggestions;
    }

    private sealed class SpoonacularIngredient
    {
        [JsonPropertyName("id")] public int Id { get; init; }
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("aisle")] public string? Aisle { get; init; }
    }
}

public sealed record IngredientSuggestion(string Name, string? CatalogItemId, string? CatalogProvider, string? Category);
