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
    }
}
