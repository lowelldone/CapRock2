using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Capstone2.Models
{
    public class HeadWaiter
    {
        [Key]
        public int HeadWaiterId { get; set; }

        public User User { get; set; }
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        public bool isActive { get; set; }
    }
}
