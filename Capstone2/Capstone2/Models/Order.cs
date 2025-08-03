using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Capstone2.Models
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Now;
        [Required]
        public DateTime CateringDate { get; set; }
        [Required]
        public string Venue { get; set; }
        [Required]
        public int NoOfPax { get; set; }
        [Required]
        public DateTime timeOfFoodServing { get; set; }
        [Required]
        public string Occasion { get; set; }
        [Required]
        public string Motif { get; set; }
        [Required]
        public double TotalPayment { get; set; }
        public double AmountPaid { get; set; } = 0; // New: Amount paid by customer
        [NotMapped]
        public double Balance => TotalPayment - AmountPaid; // New: Remaining balance
        [NotMapped]
        public bool DownPaymentMet => AmountPaid >= 0.5 * TotalPayment; // New: 50% rule
        [NotMapped]
        public double BaseAmount { get; set; } // Base amount before rush order fee
        [NotMapped]
        public double RushOrderFee { get; set; } // Rush order fee (10% of base amount)
        [NotMapped]
        public bool IsRushOrder => OrderDate.Date == CateringDate.Date; // Check if it's a rush order
        public List<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        public Customer Customer { get; set; }

        [Required]
        public int CustomerID { get; set; }

        [Required]
        public string Status { get; set; }

        // Add Waiter Assignment
        public ICollection<OrderWaiter>? OrderWaiters { get; set; }
        //Add HeadWaiter Assignment
        [ForeignKey("HeadWaiter")]
        public int? HeadWaiterId { get; set; }
        public HeadWaiter? HeadWaiter { get; set; }
    }
}

