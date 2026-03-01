using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoyalBakeryAPI.Models;

namespace RoyalBakeryAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockController : ControllerBase
{
    private readonly BakeryDbContext _db;
    public StockController(BakeryDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<StockResponse>>> GetStock()
    {
        var stocks = await _db.Stocks
            .Include(s => s.MenuItem)
            .Select(s => new StockResponse
            {
                MenuItemId = s.MenuItemId,
                ItemName = s.MenuItem != null ? s.MenuItem.Name : "Unknown",
                Quantity = s.Quantity,
                Price = s.MenuItem != null ? s.MenuItem.Price : 0
            })
            .ToListAsync();
        return Ok(stocks);
    }

    [HttpGet("{menuItemId}")]
    public async Task<ActionResult<StockResponse>> GetStockByItem(int menuItemId)
    {
        var stock = await _db.Stocks
            .Include(s => s.MenuItem)
            .Where(s => s.MenuItemId == menuItemId)
            .Select(s => new StockResponse
            {
                MenuItemId = s.MenuItemId,
                ItemName = s.MenuItem != null ? s.MenuItem.Name : "Unknown",
                Quantity = s.Quantity,
                Price = s.MenuItem != null ? s.MenuItem.Price : 0
            })
            .FirstOrDefaultAsync();

        if (stock == null)
            return NotFound(new { message = $"No stock entry for MenuItem {menuItemId}" });

        return Ok(stock);
    }
}
