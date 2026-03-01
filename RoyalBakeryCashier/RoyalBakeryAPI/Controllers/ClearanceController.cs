using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoyalBakeryAPI.Models;

namespace RoyalBakeryAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClearanceController : ControllerBase
{
    private readonly BakeryDbContext _db;
    public ClearanceController(BakeryDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<ClearanceResponse>>> GetToday()
    {
        var today = DateTime.Today;
        var clearances = await _db.Clearances
            .Include(c => c.MenuItem)
            .Where(c => c.DateTime.Date == today)
            .OrderByDescending(c => c.DateTime)
            .Select(c => new ClearanceResponse
            {
                Id = c.Id,
                DateTime = c.DateTime,
                MenuItemId = c.MenuItemId,
                ItemName = c.MenuItem != null ? c.MenuItem.Name : "Unknown",
                Quantity = c.Quantity,
                Reason = c.Reason,
                Note = c.Note
            })
            .ToListAsync();
        return Ok(clearances);
    }

    [HttpPost]
    public async Task<ActionResult<ClearanceResponse>> Create([FromBody] CreateClearanceRequest request)
    {
        var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.MenuItemId == request.MenuItemId);
        if (stock == null)
            return NotFound(new { message = "No stock found for this item" });

        if (request.Quantity > stock.Quantity)
            return BadRequest(new { message = $"Insufficient stock. Available: {stock.Quantity}" });

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var clearance = new Clearance
            {
                DateTime = DateTime.Now,
                MenuItemId = request.MenuItemId,
                Quantity = request.Quantity,
                Reason = request.Reason,
                Note = request.Note
            };
            _db.Clearances.Add(clearance);

            // Reduce stock
            stock.Quantity -= request.Quantity;

            // FIFO: reduce CurrentQuantity from oldest GRN items
            int remaining = request.Quantity;
            var grnItems = await _db.GRNItems
                .Where(gi => gi.MenuItemId == request.MenuItemId && gi.CurrentQuantity > 0)
                .OrderBy(gi => gi.Id)
                .ToListAsync();

            foreach (var gi in grnItems)
            {
                if (remaining <= 0) break;
                int deduct = Math.Min(remaining, gi.CurrentQuantity);
                gi.CurrentQuantity -= deduct;
                remaining -= deduct;
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            var menuItem = await _db.MenuItems.FindAsync(request.MenuItemId);
            return CreatedAtAction(nameof(GetToday), null, new ClearanceResponse
            {
                Id = clearance.Id,
                DateTime = clearance.DateTime,
                MenuItemId = clearance.MenuItemId,
                ItemName = menuItem?.Name ?? "Unknown",
                Quantity = clearance.Quantity,
                Reason = clearance.Reason,
                Note = clearance.Note
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = $"Error: {ex.Message}" });
        }
    }
}
