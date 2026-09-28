using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;

namespace RecipeApp.Controllers;

[ApiController]
[Route("api/product-categories")]
public sealed class ProductCategoriesController(RecipeAppContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var categories = await db.ProductCategories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new
            {
                id = category.ProductCategoryId,
                name = category.Name
            })
            .ToListAsync(cancellationToken);

        return Ok(categories);
    }
}
