namespace RecipeApp.Models;

public sealed class RecipeIngredient
{
    public int RecipeIngredientId { get; set; }
    public int RecipeId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsOptional { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public ProductCatalogItem Product { get; set; } = null!;
}
