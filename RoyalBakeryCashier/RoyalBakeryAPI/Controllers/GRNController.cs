using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoyalBakeryAPI.Models;

namespace RoyalBakeryAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GRNController : ControllerBase
{
    private readonly BakeryDbContext _db;
    public GRNController(BakeryDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<GRNResponse>>> GetAll()
    {
        var grns = await _db.GRNs
            .Include(g => g.Items).ThenInclude(i => i.MenuItem)
            .OrderByDescending(g => g.CreatedAt)
            .Take(50)
            .Select(g => new GRNResponse
            {
                Id = g.Id,
                GRNNumber = g.GRNNumber,
                CreatedAt = g.CreatedAt,
                Items = g.Items.Select(i => new GRNItemResponse
                {
                    Id = i.Id,
                    MenuItemId = i.MenuItemId,
                    ItemName = i.MenuItem != null ? i.MenuItem.Name : "Unknown",
                    Quantity = i.Quantity,
                    Price = i.Price,
                    CurrentQuantity = i.CurrentQuantity
                }).ToList()
            })
            .ToListAsync();
        return Ok(grns);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GRNResponse>> GetById(int id)
    {
        var grn = await _db.GRNs
            .Include(g => g.Items).ThenInclude(i => i.MenuItem)
            .Where(g => g.Id == id)
            .Select(g => new GRNResponse
            {
                Id = g.Id,
                GRNNumber = g.GRNNumber,
                CreatedAt = g.CreatedAt,
                Items = g.Items.Select(i => new GRNItemResponse
                {
                    Id = i.Id,
                    MenuItemId = i.MenuItemId,
                    ItemName = i.MenuItem != null ? i.MenuItem.Name : "Unknown",
                    Quantity = i.Quantity,
                    Price = i.Price,
                    CurrentQuantity = i.CurrentQuantity
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (grn == null)
            return NotFound(new { message = $"GRN {id} not found" });

        return Ok(grn);
    }

    [HttpPost]
    public async Task<ActionResult<GRNResponse>> Create([FromBody] CreateGRNRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return BadRequest(new { message = "GRN must have at least one item" });

        var grnNumber = $"GRN-{DateTime.Now:yyyyMMddHHmmss}";

        var grn = new GRN
        {
            GRNNumber = grnNumber,
            CreatedAt = DateTime.Now,
            Items = request.Items.Select(i => new GRNItem
            {
                MenuItemId = i.MenuItemId,
                Quantity = i.Quantity,
                Price = i.Price,
                CurrentQuantity = i.Quantity
            }).ToList()
        };

        _db.GRNs.Add(grn);

        // Update stock for each item
        foreach (var item in request.Items)
        {
            var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.MenuItemId == item.MenuItemId);
            if (stock != null)
            {
                stock.Quantity += item.Quantity;
            }
            else
            {
                _db.Stocks.Add(new Stock
                {
                    MenuItemId = item.MenuItemId,
                    Quantity = item.Quantity
                });
            }
        }

        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = grn.Id }, new GRNResponse
        {
            Id = grn.Id,
            GRNNumber = grn.GRNNumber,
            CreatedAt = grn.CreatedAt,
            Items = grn.Items.Select(i => new GRNItemResponse
            {
                Id = i.Id,
                MenuItemId = i.MenuItemId,
                Quantity = i.Quantity,
                Price = i.Price,
                CurrentQuantity = i.CurrentQuantity
            }).ToList()
        });
    }
}
