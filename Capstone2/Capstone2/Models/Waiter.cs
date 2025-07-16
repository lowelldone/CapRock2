using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Capstone2.Models
{
    public class Waiter
    {
        [Key]
        public int WaiterId { get; set; }
        [Required]
        public bool isTemporary { get; set; }
        public bool isDeleted { get; set; }

        public User User { get; set; }
        [ForeignKey("User")]
        public int UserId { get; set; }

        public HeadWaiter HeadWaiter { get; set; }
        [ForeignKey("HeadWaiter")]
        public int HeadWaiterId { get; set; }
        public ICollection<Attendance>? Attendance { get; set; }
    }
}
