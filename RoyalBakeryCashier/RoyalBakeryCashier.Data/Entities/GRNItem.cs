using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RoyalBakeryCashier.Data.Entities
{
    [Table("GRNItems")]
    public class GRNItem
    {
        [Key]
        public int Id { get; set; }

        public int GRNId { get; set; }

        public int MenuItemId { get; set; }

        public MenuItem? MenuItem { get; set; }  // nullable
        public GRN? GRN { get; set; }            // nullable

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Required]
        public int CurrentQuantity { get; set; }
    }
}