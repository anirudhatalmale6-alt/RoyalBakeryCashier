using System;
using System.Collections.Generic;

namespace RoyalBakeryCashier.Models
{
    public class Invoice
    {
        public string OrderId { get; set; }
        public string CustomerName { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal CashAmount { get; set; }
        public decimal CardAmount { get; set; }
        public List<InvoiceItem> Items { get; set; } = new();
    }
}