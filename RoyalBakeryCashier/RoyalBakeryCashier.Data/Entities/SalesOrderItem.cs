using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RoyalBakeryCashier.Data.Entities;

[Table("SalesOrderItems")]
public class SalesOrderItem
{
    [Key]
    public int Id { get; set; }

    public int SalesOrderId { get; set; }

    public int MenuItemId { get; set; }

    [Required]
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PricePerItem { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPrice { get; set; }

    [ForeignKey(nameof(SalesOrderId))]
    public SalesOrder? SalesOrder { get; set; }

    [ForeignKey(nameof(MenuItemId))]
    public MenuItem? MenuItem { get; set; }
}
