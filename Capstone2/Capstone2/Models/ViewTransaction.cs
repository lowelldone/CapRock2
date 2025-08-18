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

        public DateTime? ExpectedDate { get; set; }

        [Required]
        public string Status { get; set; } = "Ordered"; // Ordered, Delivered, Cancelled
    }
}


