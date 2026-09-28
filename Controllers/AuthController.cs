using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Models;

namespace RecipeApp.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(RecipeAppContext db, IPasswordHasher<AppUser> passwordHasher) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("me")]
    public IActionResult Me() => User.Identity?.IsAuthenticated == true
        ? Ok(new { email = User.FindFirstValue(ClaimTypes.Email) })
        : Unauthorized();

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        var normalizedEmail = email.ToUpperInvariant();
        if (email.Length > 254 || !MailAddress.TryCreate(email, out var address) ||
            !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 10 || request.Password.Length > 128)
        {
            return BadRequest(new { message = "Въведи валиден имейл и парола с поне 10 знака." });
        }

        if (await db.AppUsers.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
            return Conflict(new { message = "Вече има профил с този имейл." });

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        if (await db.AppUsers.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
            return Conflict(new { message = "Вече има профил с този имейл." });

        var isFirstUser = !await db.AppUsers.AnyAsync(cancellationToken);
        var user = new AppUser { Email = email, NormalizedEmail = normalizedEmail };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.AppUsers.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        // Keep the existing local pantry and saved recipes with its owner during the account transition.
        if (isFirstUser)
        {
            await db.InventoryItems.Where(item => item.UserId == null)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.UserId, user.Id), cancellationToken);
            var oldFavorites = await db.Recipes.Where(recipe => recipe.IsSaved && recipe.SourceProvider == "Spoonacular")
                .Select(recipe => recipe.RecipeId).ToListAsync(cancellationToken);
            foreach (var recipeId in oldFavorites)
                db.RecipeFavorites.Add(new RecipeFavorite { UserId = user.Id, RecipeId = recipeId });
            await db.SaveChangesAsync(cancellationToken);
            await db.Recipes.Where(recipe => recipe.IsSaved && recipe.SourceProvider == "Spoonacular")
                .ExecuteUpdateAsync(update => update.SetProperty(recipe => recipe.IsSaved, false), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await SignIn(user);
        return Ok(new { email = user.Email });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = (request.Email ?? string.Empty).Trim().ToUpperInvariant();
        var user = await db.AppUsers.SingleOrDefaultAsync(item => item.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? string.Empty)
            == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Имейлът или паролата не са правилни." });

        await SignIn(user);
        return Ok(new { email = user.Email });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return NoContent();
    }

    private Task SignIn(AppUser user)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Email, user.Email)],
            Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
        return HttpContext.SignInAsync(
            Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    public sealed record RegisterRequest(string? Email, string? Password);
    public sealed record LoginRequest(string? Email, string? Password);
}
