using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RoyalBakeryCashier.Data.Entities;

/// <summary>
/// Sales orders created by other terminals (e.g. order-taking stations).
/// Scanned or searched at the cashier POS to load items for payment.
/// </summary>
[Table("SalesOrders")]
public class SalesOrder
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string SalesOrderNumber { get; set; } = string.Empty;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// 0 = Pending (waiting for cashier), 1 = Paid, 2 = Cancelled
    /// </summary>
    public int Status { get; set; } = 0;

    public string? TerminalName { get; set; }

    public string? CustomerName { get; set; }

    public ICollection<SalesOrderItem> Items { get; set; } = new List<SalesOrderItem>();
}
