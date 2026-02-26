using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RoyalBakeryCashier.Data.Entities
{
    public class Stock
    {
        public int Id { get; set; }
        public int MenuItemId { get; set; }

        // Navigation property
        public MenuItem MenuItem { get; set; } = null!;

        public int Quantity { get; set; }
    }

}
