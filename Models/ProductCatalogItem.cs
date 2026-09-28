namespace RecipeApp.Models;

public sealed class ProductCatalogItem
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CatalogProvider { get; set; }
    public string? CatalogItemId { get; set; }
    public int? ProductCategoryId { get; set; }
    public ProductCategory? Category { get; set; }
    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}
