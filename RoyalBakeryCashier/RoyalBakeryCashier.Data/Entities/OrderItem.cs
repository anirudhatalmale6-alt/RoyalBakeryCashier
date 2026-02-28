using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RoyalBakeryCashier.Data.Entities
{
    [Table("OrderItems")]
    public class OrderItem
    {
        [Key]
        public int Id { get; set; }

        public int OrderId { get; set; }
        public int MenuItemId { get; set; }

        public int Quantity { get; set; }
        public decimal PricePerItem { get; set; }
        public decimal TotalPrice { get; set; }

        // Navigation properties (nullable to avoid EF errors)
        [ForeignKey(nameof(OrderId))]
        public Order? Order { get; set; }

        [ForeignKey(nameof(MenuItemId))]
        public MenuItem? MenuItem { get; set; }
    }
}