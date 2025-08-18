using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Capstone2.Models
{
    public class SupplierMaterialPrice
    {
        [Key]
        public int SupplierMaterialPriceId { get; set; }

        [Required]
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; }

        [Required]
        public int MaterialId { get; set; }
        public Material Material { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}


