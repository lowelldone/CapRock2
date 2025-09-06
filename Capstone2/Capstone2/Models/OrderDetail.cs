using Microsoft.AspNetCore.Routing.Constraints;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Capstone2.Models
{
    public class OrderDetail
    {
        [Key]
        public int OrderDetailId { get; set; }

        public Menu Menu { get; set; }
        public int MenuId { get; set; }
        public Order Order { get; set; }
        public int OrderId { get; set; }

        [Required]
        public int Quantity { get; set; }

        // New properties for package orders
        public string? Type { get; set; } // "Package Item", "Package Bonus", or null for regular items

        // Package-specific properties with foreign key relationship
        public int? MenuPackageId { get; set; } // Foreign key to MenuPackages

        [ForeignKey("MenuPackageId")]
        public MenuPackages? MenuPackage { get; set; } // Navigation property to MenuPackages

        public decimal? PackagePrice { get; set; } // Price per person for the package
        public decimal? PackageTotal { get; set; } // Total package price

        // Special flag for free lechon bonus
        public bool IsFreeLechon { get; set; } = false;

        [NotMapped]
        public double subTotal => (Menu?.Price ?? 0) * Quantity;
    }
}
