using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RoyalBakeryCashier.Data.Entities
{
    public class Clearance
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime DateTime { get; set; } = DateTime.Now;

        // Link to GRNItem (FK)
        [Required]
        public int MenuItemId { get; set; }
        public MenuItem MenuItem { get; set; }  // navigation property

        [Required]
        public int Quantity { get; set; }

        [Required]
        public string Reason { get; set; } = string.Empty;

        public string? Note { get; set; }
    }
}
