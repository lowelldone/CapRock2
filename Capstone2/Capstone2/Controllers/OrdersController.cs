using Microsoft.AspNetCore.Mvc;
using Capstone2.Models;
using Capstone2.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Capstone2.Controllers
{
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Form(string OrderItemsJson, Order? order)
        {
            Order? currentOrder = TempData?["Order"] != null ? JsonSerializer.Deserialize<Order>(TempData?["Order"] as string) : null;

            // Step 1: From ClientMenus
            if (currentOrder != null)
            {
                TempData["OrderItemsJson"] = JsonSerializer.Serialize(currentOrder.OrderDetails);
                ViewBag.SelectedItems = currentOrder.OrderDetails;

                currentOrder.CateringDate = DateTime.Now;
                currentOrder.Customer = new Customer();

                return View(currentOrder);
            }

            ModelState.Remove("Customer.Order");
            ModelState.Remove("Status");
            ModelState.Remove("OrderNumber");
            order.Status = "Pending";

            // Step 2: Final submission
            if (ModelState.IsValid)
            {
                List<OrderDetail> selectedItems = JsonSerializer.Deserialize<List<OrderDetail>>(OrderItemsJson);
                selectedItems.ForEach(x =>
                {
                    _context.Entry(x).Reference(x => x.Menu).Load();
                });

                order.OrderDetails = selectedItems;

                // Calculate base amount and rush order fee
                double baseAmount = selectedItems.Sum(x => x.Menu.Price * x.Quantity);
                order.BaseAmount = baseAmount;

                // Check if it's a rush order (same day)
                if (order.OrderDate.Date == order.CateringDate.Date)
                {
                    order.RushOrderFee = baseAmount * 0.10; // 10% rush order fee
                    order.TotalPayment = baseAmount + order.RushOrderFee;
                }
                else
                {
                    order.RushOrderFee = 0;
                    order.TotalPayment = baseAmount;
                }

                TempData["Order"] = JsonSerializer.Serialize(order);
                return RedirectToAction("Index", "OrderDetails");
            }

            return View(order);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null) return NotFound();
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Order model)
        {
            ModelState.Remove("Customer.Order");
            ModelState.Remove("OrderNumber");
            if (!ModelState.IsValid) return View(model);

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Menu)
                .FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null) return NotFound();

            // Update order fields
            order.Venue = model.Venue;
            order.CateringDate = model.CateringDate;
            order.timeOfFoodServing = model.timeOfFoodServing;
            order.Occasion = model.Occasion;
            order.Motif = model.Motif;
            order.NoOfPax = model.NoOfPax;

            // Update customer fields
            order.Customer.Name = model.Customer.Name;
            order.Customer.ContactNo = model.Customer.ContactNo;
            order.Customer.Address = model.Customer.Address;

            // Recalculate base total from current order details
            double baseTotal = 0;
            if (order.OrderDetails != null)
            {
                foreach (var od in order.OrderDetails)
                {
                    var unit = od.Menu?.Price ?? 0;
                    baseTotal += unit * od.Quantity;
                }
            }

            // Apply rush order fee when order date and catering date are the same calendar day
            var isRush = order.OrderDate.Date == order.CateringDate.Date;
            order.TotalPayment = isRush ? baseTotal + (baseTotal * 0.10) : baseTotal;

            _context.Orders.Update(order);
            await _context.SaveChangesAsync();
            return RedirectToAction("ViewOrder", "Customers", new { id = order.CustomerID });
        }
    }
}
