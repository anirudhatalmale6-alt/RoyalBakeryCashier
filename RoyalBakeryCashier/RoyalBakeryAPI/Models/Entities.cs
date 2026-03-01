using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RoyalBakeryAPI.Models;

[Table("MenuCategories")]
public class MenuCategory
{
    [Key]
    public int Id { get; set; }
    [Required][MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

[Table("MenuItems")]
public class MenuItem
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }
    public int MenuCategoryId { get; set; }
    public bool IsQuick { get; set; } = false;
}

[Table("Stocks")]
public class Stock
{
    [Key]
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }
    public int Quantity { get; set; }
}

public class GRN
{
    [Key]
    public int Id { get; set; }
    public string GRNNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<GRNItem> Items { get; set; } = new();
}

[Table("GRNItems")]
public class GRNItem
{
    [Key]
    public int Id { get; set; }
    public int GRNId { get; set; }
    public int MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }
    public GRN? GRN { get; set; }
    [Required]
    public int Quantity { get; set; }
    [Required][Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }
    [Required]
    public int CurrentQuantity { get; set; }
}

public class GRNAdjustmentRequest
{
    [Key]
    public int Id { get; set; }
    public int GRNId { get; set; }
    public GRN? GRN { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string AdminCode { get; set; } = string.Empty;
    public bool IsApproved { get; set; } = false;
    public DateTime RequestedAt { get; set; } = DateTime.Now;
    public ICollection<GRNAdjustmentRequestItem> RequestedItems { get; set; } = new List<GRNAdjustmentRequestItem>();
}

public class GRNAdjustmentRequestItem
{
    [Key]
    public int Id { get; set; }
    public int GRNAdjustmentRequestId { get; set; }
    public GRNAdjustmentRequest? GRNAdjustmentRequest { get; set; }
    public int MenuItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }
}

public class Clearance
{
    [Key]
    public int Id { get; set; }
    [Required]
    public DateTime DateTime { get; set; } = DateTime.Now;
    [Required]
    public int MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }
    [Required]
    public int Quantity { get; set; }
    [Required]
    public string Reason { get; set; } = string.Empty;
    public string? Note { get; set; }
}
