using Microsoft.Identity.Client;
using System.ComponentModel.DataAnnotations;

namespace Capstone2.Models
{
    public class Menu
    {
        [Key]
        public int MenuId { get; set; }

        [Required]
        public string Name { get; set; }

        public string Category { get; set; }

        [Required]
        public double Price { get; set; }

        public string? ImagePath { get; set; }

        public string? DishType { get; set; }
    }
}
