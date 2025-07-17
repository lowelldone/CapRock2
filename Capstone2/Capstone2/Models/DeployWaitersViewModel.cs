using Microsoft.AspNetCore.Mvc.Rendering;

namespace Capstone2.Models
{
    public class DeployWaitersViewModel
    {
        public Customer Customer { get; set; }
        public int CustomerId { get; set; }
        public Order Order { get; set; }
        public int OrderId { get; set; }

        // all waiters not yet assigned to this order
        public List<SelectListItem> AvailableWaiters { get; set; } = new();

        // the ones you tick in the form
        public int[] SelectedWaiterIds { get; set; } = Array.Empty<int>();
    }
}
