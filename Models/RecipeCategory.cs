namespace RecipeApp.Models;

public sealed class RecipeCategory
{
    public int RecipeCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();
}
