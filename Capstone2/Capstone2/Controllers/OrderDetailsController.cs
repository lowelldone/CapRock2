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

        public async Task<IActionResult> OrderConfirmed(string orderJson)
        {
            Order order = JsonSerializer.Deserialize<Order>(orderJson);
            order.OrderDetails.ForEach(x => x.Menu = null);

            // Check pax limits for the catering date
            var existingOrdersForDate = await _context.Orders
                .Where(o => o.CateringDate.Date == order.CateringDate.Date && o.Status != "Cancelled")
                .ToListAsync();

            int totalPaxForDate = existingOrdersForDate.Sum(o => o.NoOfPax);
            int newOrderPax = order.NoOfPax;

            //// Check if this is a large order (701-1500 pax)
            //if (newOrderPax >= 701 && newOrderPax <= 1500)
            //{
            //    // Large orders can only be the only order for that day
            //    if (existingOrdersForDate.Any())
            //    {
            //        return Json(new
            //        {
            //            success = false,
            //            message = "Large orders (701-1500 pax) cannot be scheduled on the same day as other orders. Please choose a different date."
            //        });
            //    }
            //}

            // Generate unique order number
            order.OrderNumber = await GenerateOrderNumber();

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            return Json(new { success = true, orderNumber = order.OrderNumber });
        }

        private async Task<string> GenerateOrderNumber()
        {
            var today = DateTime.Today;
            var dateString = today.ToString("yyyyMMdd");

            // Get the count of orders for today
            var todayOrderCount = await _context.Orders
                .Where(o => o.OrderDate.Date == today)
                .CountAsync();

            // Generate sequential number (starting from 1)
            var sequentialNumber = todayOrderCount + 1;

            return $"ORD-{dateString}-{sequentialNumber:D3}";
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
