using Capstone2.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Models;

namespace Capstone2.Controllers
{
    public class OrderDetailsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrderDetailsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult ConfirmOrder(int orderId)
        {
            var order = _context.Orders
                .Include(o => o.Customer)
                .FirstOrDefault(o => o.OrderId == orderId);

            var menus = _context.Menu.ToList();

            ViewBag.Menus = menus;
            return View(order);
        }

        [HttpPost]
        public IActionResult SubmitOrderDetails(int OrderId, Dictionary<int, int> Quantities)
        {
            foreach (var item in Quantities)
            {
                if (item.Value > 0)
                {
                    var detail = new OrderDetail
                    {
                        OrderId = OrderId,
                        MenuId = item.Key,
                        Quantity = item.Value
                    };
                    _context.OrderDetails.Add(detail);
                }
            }

            _context.SaveChanges();

            // Optional: Show final order summary
            return RedirectToAction("Summary", new { orderId = OrderId });
        }


    }
}
