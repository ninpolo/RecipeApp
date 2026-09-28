using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Models;

namespace RecipeApp.Controllers;

[ApiController]
[Authorize]
[Route("api/inventory-items")]
public sealed class InventoryItemsController(RecipeAppContext db) : ControllerBase
{
    private string UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var items = await db.InventoryItems
            .AsNoTracking()
            .Where(item => item.UserId == UserId)
            .Include(item => item.Product)
                .ThenInclude(product => product.Category)
            .OrderBy(item => item.Product.Name)
            .Select(item => new InventoryItemResponse(
                item.InventoryItemId,
                item.Product.Name,
                item.Product.Category == null ? null : item.Product.Category.Name,
                item.Quantity,
                item.Unit))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Add(AddInventoryItemRequest request, CancellationToken cancellationToken)
    {
        var productName = request.Name.Trim();
        var unit = request.Unit.Trim();

        if (string.IsNullOrWhiteSpace(productName) || string.IsNullOrWhiteSpace(unit) || request.Quantity <= 0)
        {
            return BadRequest(new { message = "Въведи име, положително количество и мерна единица." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var product = await db.ProductCatalog
            .FirstOrDefaultAsync(item => item.Name == productName, cancellationToken);

        if (product is null)
        {
            if (request.ProductCategoryId is int categoryId &&
                !await db.ProductCategories.AnyAsync(category => category.ProductCategoryId == categoryId, cancellationToken))
            {
                return BadRequest(new { message = "Избраната категория не съществува." });
            }

            product = new ProductCatalogItem
            {
                Name = productName,
                ProductCategoryId = request.ProductCategoryId,
                CatalogProvider = request.CatalogProvider,
                CatalogItemId = request.CatalogItemId
            };
            db.ProductCatalog.Add(product);
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (product.ProductCategoryId is null && request.ProductCategoryId is not null)
        {
            product.ProductCategoryId = request.ProductCategoryId;
        }

        if (product.CatalogProvider is null &&
            !string.IsNullOrWhiteSpace(request.CatalogProvider))
        {
            product.CatalogProvider = request.CatalogProvider;
            product.CatalogItemId = request.CatalogItemId;
        }

        var inventoryItem = await db.InventoryItems.FirstOrDefaultAsync(item =>
            item.UserId == UserId && item.ProductId == product.ProductId && item.Unit == unit,
            cancellationToken);

        if (inventoryItem is null)
        {
            inventoryItem = new InventoryItem
            {
                ProductId = product.ProductId,
                UserId = UserId,
                Quantity = request.Quantity,
                Unit = unit
            };
            db.InventoryItems.Add(inventoryItem);
        }
        else
        {
            inventoryItem.Quantity += request.Quantity;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(new { message = "Продуктът е добавен." });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateInventoryItemRequest request, CancellationToken cancellationToken)
    {
        if (request.Quantity < 0 || string.IsNullOrWhiteSpace(request.Unit))
        {
            return BadRequest(new { message = "Въведи неотрицателно количество и мерна единица." });
        }

        var item = await db.InventoryItems.SingleOrDefaultAsync(item => item.InventoryItemId == id && item.UserId == UserId, cancellationToken);
        if (item is null)
        {
            return NotFound(new { message = "Продуктът не е намерен." });
        }

        item.Quantity = request.Quantity;
        item.Unit = request.Unit.Trim();
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var item = await db.InventoryItems.SingleOrDefaultAsync(item => item.InventoryItemId == id && item.UserId == UserId, cancellationToken);
        if (item is null)
        {
            return NotFound(new { message = "Продуктът не е намерен." });
        }

        db.InventoryItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

public sealed record AddInventoryItemRequest(
    string Name,
    decimal Quantity,
    string Unit,
    int? ProductCategoryId,
    string? CatalogProvider = null,
    string? CatalogItemId = null)
{
    public string Name { get; init; } = Name?.Trim() ?? string.Empty;
    public string Unit { get; init; } = Unit?.Trim() ?? string.Empty;
}

public sealed record UpdateInventoryItemRequest(decimal Quantity, string Unit);

public sealed record InventoryItemResponse(
    int Id,
    string Name,
    string? Category,
    decimal Quantity,
    string Unit);
