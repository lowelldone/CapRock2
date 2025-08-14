using System.ComponentModel.DataAnnotations;

namespace Capstone2.Models
{
    public class Material
    {
        [Key]
        public int MaterialId { get; set; }

        [Required]
        public string Name { get; set; }

        public string Description { get; set; }

        public int Quantity { get; set; }

        public decimal ChargePerItem { get; set; }
        public bool IsConsumable { get; set; } // true for consumables, false for non-consumables
        public decimal Price { get; set; } // Price per item, can be used for both consumables and non-consumables
    }
}
