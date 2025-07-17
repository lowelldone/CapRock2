using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;
using System.Text.Json;

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
        public IActionResult Index()
        {
            Order order = JsonSerializer.Deserialize<Order>(TempData["Order"] as string);
            return View(order);
        }

        public IActionResult OrderConfirmed(string orderJson)
        {
            Order order = JsonSerializer.Deserialize<Order>(orderJson);
            order.OrderDetails.ForEach(x => x.Menu = null);

            _context.Orders.Add(order);
            _context.SaveChanges();

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Menu)
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound();

            ViewBag.Menus = await _context.Menu.ToListAsync();
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int orderId, List<OrderDetail> orderDetails)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound();

            // Remove all existing details and add new ones
            _context.OrderDetails.RemoveRange(order.OrderDetails);
            await _context.SaveChangesAsync();

            double total = 0;
            foreach (var detail in orderDetails)
            {
                detail.OrderId = orderId;
                // Get the menu price from the database
                var menu = await _context.Menu.FindAsync(detail.MenuId);
                if (menu != null)
                {
                    total += menu.Price * detail.Quantity;
                }
                _context.OrderDetails.Add(new OrderDetail
                {
                    MenuId = detail.MenuId,
                    Name = detail.Name,
                    Quantity = detail.Quantity,
                    OrderId = orderId
                });
            }
            order.TotalPayment = total;
            await _context.SaveChangesAsync();

            return RedirectToAction("ViewOrder", "Customers", new { id = order.CustomerID });
        }
    }
}
