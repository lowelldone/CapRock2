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

        public string Name { get; set; }

        public Order Order { get; set; }
        public int OrderId { get; set; }

        [Required]
        public int Quantity { get; set; }

        [NotMapped]
        public double subTotal => (Menu?.Price ?? 0) * Quantity;
    }
}
