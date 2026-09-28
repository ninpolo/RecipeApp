namespace RecipeApp.Models;

public sealed class Recipe
{
    public int RecipeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? RecipeCategoryId { get; set; }
    public int? PreparationMinutes { get; set; }
    public string Source { get; set; } = "Manual";
    public string? SourceProvider { get; set; }
    public string? ProviderRecipeId { get; set; }
    public string? SourceName { get; set; }
    public string? SourceUrl { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsSaved { get; set; }
    public DateTime CreatedAt { get; set; }
    public RecipeCategory? Category { get; set; }
    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<RecipeStep> Steps { get; set; } = new List<RecipeStep>();
}
