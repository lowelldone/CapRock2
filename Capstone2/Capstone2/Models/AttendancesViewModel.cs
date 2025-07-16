namespace Capstone2.Models
{
    public class AttendancesViewModel
    {
        public Customer Customer { get; set; }
        public int CustomerId { get; set; }    // to redirect back
        public Order Order { get; set; }
        public int OrderId { get; set; }
        public List<Waiter> Waiters { get; set; } = new();
    }
}
