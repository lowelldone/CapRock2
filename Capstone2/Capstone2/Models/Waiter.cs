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

        // Availability: 'Available' or 'Busy'
        public string Availability { get; set; } = "Available";

        // Many-to-many with Order
        public ICollection<OrderWaiter>? OrderWaiters { get; set; }
    }
}
