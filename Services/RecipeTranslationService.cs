using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace RecipeApp.Services;

public sealed class RecipeTranslationService
{
    private const int MaxQueryBytes = 450;
    private static readonly SemaphoreSlim RequestLimit = new(3, 3);
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly IMemoryCache cache;
    private readonly ILogger<RecipeTranslationService> logger;

    public RecipeTranslationService(
        HttpClient httpClient,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<RecipeTranslationService> logger)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.cache = cache;
        this.logger = logger;
        httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    public string GetSourceHash(RecipeDetails details)
    {
        var text = string.Join('\n', new[] { "translation-format-v2" }.Concat(GetTexts(details)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    public async Task<RecipeTranslationResult> TranslateToBulgarianAsync(
        RecipeDetails details,
        CancellationToken cancellationToken)
    {
        var titleTask = TranslateCachedAsync(details.Title, cancellationToken);
        var namesTask = Task.WhenAll(details.Ingredients.Select(ingredient =>
            TranslateCachedAsync(ingredient.CatalogName ?? ingredient.Name, cancellationToken)));
        var unitsTask = Task.WhenAll(details.Ingredients.Select(ingredient =>
            TranslateUnitAsync(ingredient.Unit, cancellationToken)));
        var stepsTask = Task.WhenAll(details.Steps.Select(step =>
            TranslateCachedAsync(step.Instruction, cancellationToken)));
        await Task.WhenAll(titleTask, namesTask, unitsTask, stepsTask);

        return new RecipeTranslationResult(details.Id, titleTask.Result, namesTask.Result, unitsTask.Result, stepsTask.Result);
    }

    public Task<string> TranslateTextToBulgarianAsync(string text, CancellationToken cancellationToken) =>
        TranslateCachedAsync(text, cancellationToken);

    private Task<string> TranslateUnitAsync(string unit, CancellationToken cancellationToken)
    {
        var normalized = unit.Trim().ToLowerInvariant();
        var knownUnit = normalized switch
        {
            "tbsp" or "tbs" or "tablespoon" or "tablespoons" => "с.л.",
            "tsp" or "teaspoon" or "teaspoons" => "ч.л.",
            "cup" or "cups" => "ч.ч.",
            "can" or "cans" or "tin" or "tins" => "консерва",
            "g" or "gram" or "grams" => "г",
            "kg" or "kilogram" or "kilograms" => "кг",
            "ml" or "milliliter" or "milliliters" => "мл",
            "l" or "liter" or "liters" => "л",
            "oz" or "ounce" or "ounces" => "унц.",
            "lb" or "pound" or "pounds" => "фунта",
            "pinch" or "pinches" => "щипка",
            "piece" or "pieces" => "бр.",
            "slice" or "slices" => "филия",
            _ => null
        };
        return knownUnit is not null ? Task.FromResult(knownUnit) : TranslateCachedAsync(unit, cancellationToken);
    }

    private async Task<string> TranslateCachedAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var chunks = SplitByUtf8Limit(text, MaxQueryBytes);
        var results = await Task.WhenAll(chunks.Select(TranslateChunkCachedAsync));
        return string.Join(' ', results).Trim();

        Task<string> TranslateChunkCachedAsync(string chunk)
        {
            var cacheKey = "recipe-translation:en:bg:" + Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(chunk)));
            return cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7);
                await RequestLimit.WaitAsync(cancellationToken);
                try
                {
                    return await TranslateChunkAsync(chunk, cancellationToken);
                }
                finally
                {
                    RequestLimit.Release();
                }
            })!;
        }
    }

    private async Task<string> TranslateChunkAsync(string text, CancellationToken cancellationToken)
    {
        var baseUrl = configuration["RecipeTranslation:BaseUrl"] ?? "https://api.mymemory.translated.net/";
        var requestUri = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"),
            "get?q=" + Uri.EscapeDataString(text) + "&langpair=en%7Cbg");
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Recipe translation provider returned HTTP {StatusCode}.", (int)response.StatusCode);
            throw new RecipeTranslationException(GetErrorMessage(response.StatusCode));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var status = root.TryGetProperty("responseStatus", out var statusElement) &&
                     statusElement.TryGetInt32(out var statusCode) ? statusCode : 200;
        var translation = root.TryGetProperty("responseData", out var data) &&
                          data.TryGetProperty("translatedText", out var translatedText)
            ? translatedText.GetString()
            : null;
        if (status != 200 || string.IsNullOrWhiteSpace(translation))
        {
            throw new RecipeTranslationException("Преводът временно не е достъпен. Опитай отново по-късно.");
        }

        return WebUtility.HtmlDecode(translation).Trim();
    }

    private static string[] GetTexts(RecipeDetails details) =>
    [
        details.Title,
        .. details.Ingredients.Select(ingredient => ingredient.CatalogName ?? ingredient.Name),
        .. details.Ingredients.Select(ingredient => ingredient.Unit),
        .. details.Steps.Select(step => step.Instruction)
    ];

    private static IReadOnlyList<string> SplitByUtf8Limit(string text, int maxBytes)
    {
        var chunks = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var end = start;
            var byteCount = 0;
            while (end < text.Length)
            {
                var charBytes = Encoding.UTF8.GetByteCount(text.AsSpan(end, 1));
                if (byteCount + charBytes > maxBytes) break;
                byteCount += charBytes;
                end++;
            }

            if (end < text.Length)
            {
                var splitAt = text.LastIndexOf(' ', end - 1, end - start);
                if (splitAt > start) end = splitAt;
            }

            chunks.Add(text[start..end].Trim());
            start = end;
            while (start < text.Length && char.IsWhiteSpace(text[start])) start++;
        }

        return chunks.Count == 0 ? [text] : chunks;
    }

    private static string GetErrorMessage(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.TooManyRequests => "Достигнат е дневният лимит за превод. Опитай отново утре.",
        _ => "Услугата за превод временно не отговаря. Опитай отново по-късно."
    };
}

public sealed class RecipeTranslationException(string message) : Exception(message);

public sealed record RecipeTranslationResult(
    int Id,
    string Title,
    IReadOnlyList<string> IngredientNames,
    IReadOnlyList<string> IngredientUnits,
    IReadOnlyList<string> StepInstructions);
