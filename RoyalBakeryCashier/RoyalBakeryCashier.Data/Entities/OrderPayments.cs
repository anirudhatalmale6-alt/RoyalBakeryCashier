using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoyalBakeryCashier.Data.Entities
{
    public class OrderPayments
    {
        [Key]
        public int Id { get; set; }

        public int OrderId { get; set; }

        // paymentType: 0 = Cash, 1 = Card
        public int PaymentType { get; set; }

        public decimal TenderAmount { get; set; }

        public DateTime DateTime { get; set; }
    }
}
