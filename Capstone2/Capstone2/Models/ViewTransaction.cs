using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Capstone2.Models
{
    public class ViewTransaction
    {
        [Key]
        public int ViewTransactionId { get; set; }

        [Required]
        [ForeignKey("Supplier")]
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; }

        [Required]
        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Required]
        public string Status { get; set; } = "Ordered"; // Ordered, Delivered, Cancelled

        // Human-readable order number for this transaction (per order), e.g., 20250131-001
        [MaxLength(32)]
        public string? TransactionOrderNumber { get; set; }
    }
}


