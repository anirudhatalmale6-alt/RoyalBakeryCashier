using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RoyalBakeryCashier.Data.Entities
{
    [Table("Sales")]
    public class Sale
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime DateTime { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CashAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CardAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ChangeGiven { get; set; }

        public string? CashierName { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;

        public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
    }
}
