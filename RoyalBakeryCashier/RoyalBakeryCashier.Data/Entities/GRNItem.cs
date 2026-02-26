using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RoyalBakeryCashier.Data.Entities   // ⭐ ADD THIS
{
    public class GRNItem
    {
        [Key]
        public int Id { get; set; }

        // Foreign Key to GRN
        public int GRNId { get; set; }
        public GRN GRN { get; set; } = null!;

        // Foreign Key to MenuItem
        [Required]
        public int MenuItemId { get; set; }

        public MenuItem MenuItem { get; set; } = null!;

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Required]
        public int CurrentQuantity { get; set; }
    }
}
