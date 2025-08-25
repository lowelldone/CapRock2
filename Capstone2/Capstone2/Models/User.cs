using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Capstone2.Models
{
    [Table("Users")]
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        public string Password { get; set; }

        [Display(Name = "First Name")]
        [Required]
        public string FirstName { get; set; }

        [Display(Name = "Last Name")]
        [Required]
        public string LastName { get; set; }

        [Required]
        public string Role { get; set; }

        [Display(Name = "Number")]
        [StringLength(11, MinimumLength = 11, ErrorMessage = "Phone Number must be exactly 11 digits.")]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "Phone Number must be exactly 11 digits.")]
        public string UserNumber { get; set; }
    }
}
