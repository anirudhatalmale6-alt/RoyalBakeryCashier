namespace RoyalBakeryAPI.Models;

// ===== GRN DTOs =====
public class CreateGRNRequest
{
    public List<GRNItemDTO> Items { get; set; } = new();
}

public class GRNItemDTO
{
    public int MenuItemId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class GRNResponse
{
    public int Id { get; set; }
    public string GRNNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<GRNItemResponse> Items { get; set; } = new();
}

public class GRNItemResponse
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public int CurrentQuantity { get; set; }
}

// ===== Adjustment DTOs =====
public class CreateAdjustmentRequest
{
    public int GRNId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<AdjustmentItemDTO> Items { get; set; } = new();
}

public class AdjustmentItemDTO
{
    public int MenuItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public decimal Price { get; set; }
}

public class ApproveAdjustmentRequest
{
    public string AdminCode { get; set; } = string.Empty;
}

public class AdjustmentResponse
{
    public int Id { get; set; }
    public int GRNId { get; set; }
    public string GRNNumber { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string AdminCode { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public DateTime RequestedAt { get; set; }
    public List<AdjustmentItemResponse> Items { get; set; } = new();
}

public class AdjustmentItemResponse
{
    public int MenuItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public decimal Price { get; set; }
}

// ===== Clearance DTOs =====
public class CreateClearanceRequest
{
    public int MenuItemId { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class ClearanceResponse
{
    public int Id { get; set; }
    public DateTime DateTime { get; set; }
    public int MenuItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Note { get; set; }
}

// ===== Stock / Menu DTOs =====
public class StockResponse
{
    public int MenuItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}

public class MenuItemResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int MenuCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool IsQuick { get; set; }
}

public class CategoryResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
