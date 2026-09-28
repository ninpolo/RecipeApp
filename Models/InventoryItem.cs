namespace RecipeApp.Models;

public sealed class InventoryItem
{
    public int InventoryItemId { get; set; }
    public int ProductId { get; set; }
    public string? UserId { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
    public ProductCatalogItem Product { get; set; } = null!;
    public AppUser? User { get; set; }
}
