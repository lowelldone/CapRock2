using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Capstone2.Models
{
    public class Attendance
    {
        [Key]
        public int AttendanceId { get; set; }

        [Required]
        public DateTime TimeIn { get; set; } = DateTime.Now;

        [Required]
        public DateTime TimeOut { get; set; } = DateTime.Now;

        public Staff Staff { get; set; }
        [ForeignKey("Staff")]
        public int StaffId { get; set; }

        public Order Order { get; set; }

        [ForeignKey("Order")]
        public int OrderId { get; set; }
    }
}
