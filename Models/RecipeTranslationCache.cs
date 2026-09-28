namespace RecipeApp.Models;

public sealed class RecipeTranslationCache
{
    public int RecipeTranslationCacheId { get; set; }
    public string ProviderRecipeId { get; set; } = string.Empty;
    public string Language { get; set; } = "bg";
    public string SourceHash { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string IngredientNamesJson { get; set; } = "[]";
    public string IngredientUnitsJson { get; set; } = "[]";
    public string StepInstructionsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
}
