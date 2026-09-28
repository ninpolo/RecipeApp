using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using RecipeApp.Data;
using RecipeApp.Models;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:5000");

// Add services to the container.
builder.Services.AddControllers();

// 1. ДОБАВЯНЕ НА CORS ПОЛИТИКА ЗА ТЕЛЕФОНА
builder.Services.AddCors(options => {
    options.AddPolicy("AllowPhone", policy => {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "RecipeApp.Auth";
        options.Cookie.HttpOnly = true;

        // КОРЕКЦИЯ ЗА ТЕСТВАНЕ ПРЕЗ TAILSCALE (HTTP):
        options.Cookie.SameSite = SameSiteMode.Lax; // Променено от Strict на Lax
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Променено от Always на SameAsRequest

        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddHttpClient<RecipeApp.Services.IngredientCatalogService>();
builder.Services.AddHttpClient<RecipeApp.Services.RecipeCatalogService>();
builder.Services.AddHttpClient<RecipeApp.Services.RecipeTranslationService>();
builder.Services.AddMemoryCache();
builder.Services.AddDbContext<RecipeAppContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("RecipeApp")));

builder.Services.AddOpenApi();

var app = builder.Build();

// 2. АКТИВИРАНЕ НА CORS (Слага се точно тук, веднага след builder.Build())
app.UseCors("AllowPhone");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
