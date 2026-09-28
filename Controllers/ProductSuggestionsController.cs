using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Services;

namespace RecipeApp.Controllers;

[ApiController]
[Route("api/product-suggestions")]
public sealed class ProductSuggestionsController(
    RecipeAppContext db,
    IngredientCatalogService catalog,
    ILogger<ProductSuggestionsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? query, CancellationToken cancellationToken)
    {
        var term = query?.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2) return Ok(Array.Empty<object>());

        var local = await db.ProductCatalog.AsNoTracking()
            .Where(product => EF.Functions.Like(product.Name, $"%{term}%"))
            .OrderBy(product => product.Name)
            .Take(8)
            .Select(product => new SuggestionResponse(product.Name, null, null, "Моят каталог"))
            .ToListAsync(cancellationToken);

        var external = new List<SuggestionResponse>();
        // Spoonacular supports English/German autocomplete. Skip remote lookup for Bulgarian input.
        if (Regex.IsMatch(term, "^[\\p{IsBasicLatin}\\p{P}\\p{Zs}]+$"))
        {
            try
            {
                external = (await catalog.SearchAsync(term, cancellationToken))
                    .Select(item => new SuggestionResponse(item.Name, item.CatalogProvider, item.CatalogItemId, item.Category))
                    .ToList();
                logger.LogInformation("Spoonacular returned {SuggestionCount} ingredient suggestions.", external.Count);
            }
            catch (HttpRequestException)
            {
                // Local catalog suggestions remain available when the external service is offline.
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // A provider timeout should not prevent adding a product manually.
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var suggestions = local.Concat(external).Where(item => seen.Add(item.Name)).Take(12);
        return Ok(suggestions);
    }
}

public sealed record SuggestionResponse(string Name, string? CatalogProvider, string? CatalogItemId, string? Category);
