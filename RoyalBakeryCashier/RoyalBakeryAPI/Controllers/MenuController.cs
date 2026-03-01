using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoyalBakeryAPI.Models;

namespace RoyalBakeryAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenuController : ControllerBase
{
    private readonly BakeryDbContext _db;
    public MenuController(BakeryDbContext db) => _db = db;

    [HttpGet("categories")]
    public async Task<ActionResult<List<CategoryResponse>>> GetCategories()
    {
        var categories = await _db.MenuCategories
            .Select(c => new CategoryResponse { Id = c.Id, Name = c.Name })
            .ToListAsync();
        return Ok(categories);
    }

    [HttpGet("items")]
    public async Task<ActionResult<List<MenuItemResponse>>> GetMenuItems()
    {
        var items = await _db.MenuItems
            .Join(_db.MenuCategories, mi => mi.MenuCategoryId, mc => mc.Id,
                (mi, mc) => new MenuItemResponse
                {
                    Id = mi.Id,
                    Name = mi.Name,
                    Price = mi.Price,
                    MenuCategoryId = mi.MenuCategoryId,
                    CategoryName = mc.Name,
                    IsQuick = mi.IsQuick
                })
            .ToListAsync();
        return Ok(items);
    }
}
