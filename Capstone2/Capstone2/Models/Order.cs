using System.ComponentModel.DataAnnotations;

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
        public List<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        public Customer Customer { get; set; }

        [Required]
        public int CustomerID { get; set; }

        [Required]
        public string Status { get; set; }

        // Add waiter assignment
        public ICollection<OrderWaiter>? OrderWaiters { get; set; }
    }
}
