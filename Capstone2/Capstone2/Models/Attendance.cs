using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Capstone2.Models
{
    public class Attendance
    {
        [Key]
        public int AttendanceId { get; set; }

        [Required]
        public DateTime? TimeIn { get; set; }

        [Required]
        public DateTime? TimeOut { get; set; }

        public Waiter Waiter { get; set; }
        [ForeignKey("Waiter")]
        public int WaiterId { get; set; }

        public Order? Order { get; set; }

        [ForeignKey("Order")]
        public int? OrderId { get; set; }
    }
}
