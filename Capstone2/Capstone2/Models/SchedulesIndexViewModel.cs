using System.Collections.Generic;
using Capstone2.Models;

namespace Capstone2.Models
{
    public class SchedulesIndexViewModel
    {
        public List<Order> HeadWaiterAssignedOrders { get; set; } = new List<Order>();
        public List<Order> WaiterAssignedOrders { get; set; } = new List<Order>();
    }
}
