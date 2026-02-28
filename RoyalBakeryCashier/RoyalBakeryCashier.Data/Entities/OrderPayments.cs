using System;
using System.ComponentModel.DataAnnotations;

namespace RoyalBakeryCashier.Data.Entities
{
    public class OrderPayments
    {
        [Key]
        public int Id { get; set; }

        public int OrderId { get; set; }

        // 0 = Cash, 1 = Card
        public int PaymentType { get; set; }

        public decimal TenderAmount { get; set; }

        public DateTime DateTime { get; set; }
    }
}