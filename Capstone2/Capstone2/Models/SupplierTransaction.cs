using System.ComponentModel.DataAnnotations;

namespace Capstone2.Models
{
    public class SupplierTransaction
    {
        [Key]
        public int SupplierTransactionId { get; set; }

        [Required]
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; }

        [Required]
        public int MaterialId { get; set; }
        public Material Material { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        // Quantity actually received; may be less than ordered when partially delivered
        public int? ReceivedQuantity { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.Now;
        public DateTime? ExpectedDeliveryDate { get; set; }
        public DateTime? DeliveredDate { get; set; }

        [Required]
        public string Status { get; set; } = "Pending"; // Pending, Ordered, Delivered, Cancelled

        // Link to the ViewTransaction that groups related purchase orders/transactions
        public int? ViewTransactionId { get; set; }
        public ViewTransaction ViewTransaction { get; set; }
    }
}


