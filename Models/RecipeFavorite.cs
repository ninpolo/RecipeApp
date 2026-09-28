namespace RecipeApp.Models;

public sealed class RecipeFavorite
{
    public string UserId { get; set; } = string.Empty;
    public int RecipeId { get; set; }
    public DateTime CreatedAt { get; set; }
    public AppUser User { get; set; } = null!;
    public Recipe Recipe { get; set; } = null!;
}
