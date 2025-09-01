using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Capstone2.Models
{
    public class PurchaseOrder
    {
        [Key]
        public int PurchaseOrderId { get; set; }

        [Required]
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; }

        [Required]
        public int MaterialId { get; set; }
        public Material Material { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Open"; // Open, Ordered, Delivered, Cancelled

        // Quantity actually received; populated upon receiving
        public int? ReceivedQuantity { get; set; }

        // Link each purchase order to its own ViewTransaction record
        public int? ViewTransactionId { get; set; }
        public ViewTransaction ViewTransaction { get; set; }
    }
}


