namespace RecipeApp.Models;

public sealed class ProductCategory
{
    public int ProductCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<ProductCatalogItem> Products { get; set; } = new List<ProductCatalogItem>();
}
