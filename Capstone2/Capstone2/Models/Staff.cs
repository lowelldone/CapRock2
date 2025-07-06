using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Capstone2.Models
{
    public class Staff
    {
        [Key]
        public int StaffId { get; set; }

        [Required]
        public bool isTemporary = false;

        public User User { get; set; }
        [ForeignKey("User")]
        public int UserId { get; set; }

        public HeadWaiter HeadWaiter { get; set; }
        [ForeignKey ("HeadWaiter")]
        public int HeadWaiterId { get; set; }
    }
}
