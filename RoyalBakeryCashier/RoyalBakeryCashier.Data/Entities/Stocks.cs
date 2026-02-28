using System.ComponentModel.DataAnnotations.Schema;

namespace RoyalBakeryCashier.Data.Entities
{
    [Table("Stocks")]
    public class Stock
    {
        public int Id { get; set; }
        public int MenuItemId { get; set; }

        public MenuItem? MenuItem { get; set; } // nullable

        public int Quantity { get; set; }
    }
}