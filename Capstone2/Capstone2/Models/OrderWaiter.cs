using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Capstone2.Models
{
    public class OrderWaiter
    {
        [Key]
        public int OrderWaiterId { get; set; }
        public int OrderId { get; set; }
        public Order Order { get; set; }

        public int WaiterId { get; set; }
        public Waiter Waiter { get; set; }
    }
}
