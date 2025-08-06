using System.ComponentModel.DataAnnotations;

namespace Capstone2.Models
{
    public class Customer
    {
        [Key]
        public int CustomerID { get; set; }

        [Required]
        public string Name { get; set; }

        [Display(Name = "Contact No.")]
        public string ContactNo { get; set; }

        public string Address { get; set; }

        public bool IsPaid { get; set; } = false;
        public bool isDeleted { get; set; } = false;
        public virtual Order Order { get; set; }
    }
}
