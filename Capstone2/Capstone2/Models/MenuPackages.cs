using System.ComponentModel.DataAnnotations;

namespace Capstone2.Models
{
    public class MenuPackages
    {
        [Key]
        public int MenuPackageId { get; set; }

        [Required]
        public string MenuPackageName { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public double Price { get; set; }

        [Required]
        public int NoOfMainDish { get; set; }

        public int? NoOfSideDish { get; set; }

        public int? NoOfDessert { get; set; }

        public int? NoOfRice { get; set; }

        public int? NoOfSoftDrinks { get; set; }

        public int NumberOfFreeLechons { get; set; } = 0;

        [Required]
        public int MinimumPax { get; set; }

    }
}
