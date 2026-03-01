using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoyalBakeryAPI.Models;

namespace RoyalBakeryAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdjustmentController : ControllerBase
{
    private readonly BakeryDbContext _db;
    public AdjustmentController(BakeryDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<AdjustmentResponse>>> GetPending()
    {
        var requests = await _db.GRNAdjustmentRequests
            .Include(r => r.GRN)
            .Include(r => r.RequestedItems)
            .Where(r => !r.IsApproved)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new AdjustmentResponse
            {
                Id = r.Id,
                GRNId = r.GRNId,
                GRNNumber = r.GRN != null ? r.GRN.GRNNumber : "",
                Reason = r.Reason,
                AdminCode = r.AdminCode,
                IsApproved = r.IsApproved,
                RequestedAt = r.RequestedAt,
                Items = r.RequestedItems.Select(i => new AdjustmentItemResponse
                {
                    MenuItemId = i.MenuItemId,
                    ItemName = i.ItemName,
                    RequestedQuantity = i.RequestedQuantity,
                    Price = i.Price
                }).ToList()
            })
            .ToListAsync();
        return Ok(requests);
    }

    [HttpPost]
    public async Task<ActionResult<AdjustmentResponse>> Create([FromBody] CreateAdjustmentRequest request)
    {
        var grn = await _db.GRNs.FindAsync(request.GRNId);
        if (grn == null)
            return NotFound(new { message = $"GRN {request.GRNId} not found" });

        var adminCode = $"ADM-{Guid.NewGuid().ToString()[..6].ToUpper()}";

        var adj = new GRNAdjustmentRequest
        {
            GRNId = request.GRNId,
            Reason = request.Reason,
            AdminCode = adminCode,
            IsApproved = false,
            RequestedAt = DateTime.Now,
            RequestedItems = request.Items.Select(i => new GRNAdjustmentRequestItem
            {
                MenuItemId = i.MenuItemId,
                ItemName = i.ItemName,
                RequestedQuantity = i.RequestedQuantity,
                Price = i.Price
            }).ToList()
        };

        _db.GRNAdjustmentRequests.Add(adj);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPending), null, new AdjustmentResponse
        {
            Id = adj.Id,
            GRNId = adj.GRNId,
            GRNNumber = grn.GRNNumber,
            Reason = adj.Reason,
            AdminCode = adj.AdminCode,
            IsApproved = adj.IsApproved,
            RequestedAt = adj.RequestedAt,
            Items = adj.RequestedItems.Select(i => new AdjustmentItemResponse
            {
                MenuItemId = i.MenuItemId,
                ItemName = i.ItemName,
                RequestedQuantity = i.RequestedQuantity,
                Price = i.Price
            }).ToList()
        });
    }

    [HttpPost("{id}/approve")]
    public async Task<ActionResult> Approve(int id, [FromBody] ApproveAdjustmentRequest request)
    {
        var adj = await _db.GRNAdjustmentRequests
            .Include(r => r.RequestedItems)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (adj == null)
            return NotFound(new { message = "Adjustment request not found" });

        if (adj.IsApproved)
            return BadRequest(new { message = "Already approved" });

        if (adj.AdminCode != request.AdminCode)
            return BadRequest(new { message = "Invalid admin code" });

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var reqItem in adj.RequestedItems)
            {
                var grnItem = await _db.GRNItems
                    .FirstOrDefaultAsync(gi => gi.GRNId == adj.GRNId && gi.MenuItemId == reqItem.MenuItemId);

                if (grnItem == null) continue;

                int soldQty = grnItem.Quantity - grnItem.CurrentQuantity;
                if (reqItem.RequestedQuantity < soldQty)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { message = $"Cannot reduce {reqItem.ItemName} below {soldQty} (already sold)" });
                }

                int qtyDiff = reqItem.RequestedQuantity - grnItem.Quantity;
                grnItem.Quantity = reqItem.RequestedQuantity;
                grnItem.CurrentQuantity += qtyDiff;

                var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.MenuItemId == reqItem.MenuItemId);
                if (stock != null)
                    stock.Quantity += qtyDiff;
            }

            adj.IsApproved = true;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Adjustment approved and applied" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = $"Error: {ex.Message}" });
        }
    }
}
