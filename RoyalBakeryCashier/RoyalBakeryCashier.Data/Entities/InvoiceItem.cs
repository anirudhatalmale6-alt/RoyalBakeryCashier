namespace RoyalBakeryCashier.Models
{
    public class InvoiceItem
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        public decimal SubTotal => UnitPrice * Quantity;
    }
}