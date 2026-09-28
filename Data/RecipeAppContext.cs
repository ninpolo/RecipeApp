using Microsoft.EntityFrameworkCore;
using RecipeApp.Models;

namespace RecipeApp.Data;

public sealed class RecipeAppContext(DbContextOptions<RecipeAppContext> options) : DbContext(options)
{
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<RecipeCategory> RecipeCategories => Set<RecipeCategory>();
    public DbSet<ProductCatalogItem> ProductCatalog => Set<ProductCatalogItem>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<RecipeTranslationCache> RecipeTranslationCache => Set<RecipeTranslationCache>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<RecipeFavorite> RecipeFavorites => Set<RecipeFavorite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("AppUsers");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).HasMaxLength(32);
            entity.Property(user => user.Email).HasMaxLength(254).IsRequired();
            entity.Property(user => user.NormalizedEmail).HasMaxLength(254).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(user => user.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(user => user.NormalizedEmail).IsUnique();
        });

        modelBuilder.Entity<RecipeFavorite>(entity =>
        {
            entity.ToTable("RecipeFavorites");
            entity.HasKey(favorite => new { favorite.UserId, favorite.RecipeId });
            entity.Property(favorite => favorite.UserId).HasMaxLength(32);
            entity.Property(favorite => favorite.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(favorite => favorite.User).WithMany().HasForeignKey(favorite => favorite.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(favorite => favorite.Recipe).WithMany().HasForeignKey(favorite => favorite.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.ToTable("ProductCategories");
            entity.HasKey(category => category.ProductCategoryId);
            entity.Property(category => category.Name).HasMaxLength(60).IsRequired();
            entity.HasIndex(category => category.Name).IsUnique();
        });

        modelBuilder.Entity<RecipeCategory>(entity =>
        {
            entity.ToTable("RecipeCategories");
            entity.HasKey(category => category.RecipeCategoryId);
            entity.Property(category => category.Name).HasMaxLength(60).IsRequired();
            entity.HasIndex(category => category.Name).IsUnique();
        });

        modelBuilder.Entity<ProductCatalogItem>(entity =>
        {
            entity.ToTable("ProductCatalog");
            entity.HasKey(product => product.ProductId);
            entity.Property(product => product.Name).HasMaxLength(100).IsRequired();
            entity.Property(product => product.CatalogProvider).HasMaxLength(50);
            entity.Property(product => product.CatalogItemId).HasMaxLength(100);
            entity.HasIndex(product => product.Name).IsUnique();
            entity.HasIndex(product => new { product.CatalogProvider, product.CatalogItemId }).IsUnique()
                .HasFilter("[CatalogProvider] IS NOT NULL AND [CatalogItemId] IS NOT NULL");
            entity.HasOne(product => product.Category)
                .WithMany(category => category.Products)
                .HasForeignKey(product => product.ProductCategoryId);
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.ToTable("InventoryItems");
            entity.HasKey(item => item.InventoryItemId);
            entity.Property(item => item.Quantity).HasPrecision(10, 2);
            entity.Property(item => item.Unit).HasMaxLength(20).IsRequired();
            entity.Property(item => item.AddedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(item => item.UserId).HasMaxLength(32);
            entity.HasIndex(item => new { item.UserId, item.ProductId, item.Unit }).IsUnique();
            entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Product)
                .WithMany(product => product.InventoryItems)
                .HasForeignKey(item => item.ProductId);
        });

        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.ToTable("Recipes");
            entity.HasKey(recipe => recipe.RecipeId);
            entity.Property(recipe => recipe.Name).HasMaxLength(140).IsRequired();
            entity.Property(recipe => recipe.Description).HasMaxLength(500);
            entity.Property(recipe => recipe.Source).HasMaxLength(20).HasDefaultValue("Manual");
            entity.Property(recipe => recipe.SourceProvider).HasMaxLength(50);
            entity.Property(recipe => recipe.ProviderRecipeId).HasMaxLength(150);
            entity.Property(recipe => recipe.SourceName).HasMaxLength(150);
            entity.Property(recipe => recipe.SourceUrl).HasMaxLength(2048);
            entity.Property(recipe => recipe.ImageUrl).HasMaxLength(2048);
            entity.Property(recipe => recipe.IsSaved).HasDefaultValue(false);
            entity.Property(recipe => recipe.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(recipe => new { recipe.SourceProvider, recipe.ProviderRecipeId })
                .IsUnique()
                .HasFilter("[SourceProvider] IS NOT NULL AND [ProviderRecipeId] IS NOT NULL");
            entity.HasOne(recipe => recipe.Category)
                .WithMany(category => category.Recipes)
                .HasForeignKey(recipe => recipe.RecipeCategoryId);
        });

        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.ToTable("RecipeIngredients");
            entity.HasKey(ingredient => ingredient.RecipeIngredientId);
            entity.Property(ingredient => ingredient.Quantity).HasPrecision(10, 2);
            entity.Property(ingredient => ingredient.Unit).HasMaxLength(20).IsRequired();
            entity.Property(ingredient => ingredient.IsOptional).HasDefaultValue(false);
            entity.HasOne(ingredient => ingredient.Recipe)
                .WithMany(recipe => recipe.Ingredients)
                .HasForeignKey(ingredient => ingredient.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(ingredient => ingredient.Product)
                .WithMany(product => product.RecipeIngredients)
                .HasForeignKey(ingredient => ingredient.ProductId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<RecipeStep>(entity =>
        {
            entity.ToTable("RecipeSteps");
            entity.HasKey(step => step.RecipeStepId);
            entity.Property(step => step.Instruction).HasMaxLength(1000).IsRequired();
            entity.HasIndex(step => new { step.RecipeId, step.StepNumber }).IsUnique();
            entity.HasOne(step => step.Recipe)
                .WithMany(recipe => recipe.Steps)
                .HasForeignKey(step => step.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecipeTranslationCache>(entity =>
        {
            entity.ToTable("RecipeTranslationCache");
            entity.HasKey(translation => translation.RecipeTranslationCacheId);
            entity.Property(translation => translation.ProviderRecipeId).HasMaxLength(150).IsRequired();
            entity.Property(translation => translation.Language).HasMaxLength(10).IsRequired();
            entity.Property(translation => translation.SourceHash).HasMaxLength(64).IsRequired();
            entity.Property(translation => translation.Title).HasMaxLength(500).IsRequired();
            entity.Property(translation => translation.IngredientNamesJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(translation => translation.IngredientUnitsJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(translation => translation.StepInstructionsJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(translation => translation.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(translation => new
                { translation.ProviderRecipeId, translation.Language, translation.SourceHash })
                .IsUnique();
        });
    }
}
