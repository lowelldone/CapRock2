using System.ComponentModel.DataAnnotations;

namespace Capstone2.Models
{
    public class CustomerProfileViewModel
    {
        public int CustomerID { get; set; }

        [Required]
        public string Name { get; set; }

        [Display(Name = "Contact No.")]
        public string ContactNo { get; set; }

        public string Address { get; set; }

        public int TotalOrders { get; set; }

        public List<OrderSummary> Orders { get; set; } = new List<OrderSummary>();
    }

    public class OrderSummary
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime CateringDate { get; set; }
        public string Venue { get; set; }
        public int NoOfPax { get; set; }
        public string Status { get; set; }
        public double TotalPayment { get; set; }
        public double AmountPaid { get; set; }
        public double Balance { get; set; }
        public string Occasion { get; set; }
    }
}

