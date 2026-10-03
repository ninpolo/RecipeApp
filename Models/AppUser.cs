namespace RecipeApp.Models;

public class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N"); // 32 hexadecimal characters
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
