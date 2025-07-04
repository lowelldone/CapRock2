using Microsoft.AspNetCore.Mvc;
using Capstone2.Models;
using Capstone2.Data;
using System.Text.Json;

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

                currentOrder.OrderDate = DateTime.Now.Date;
                currentOrder.Customer = new Customer();

                return View(currentOrder);
            }

            // Step 2: Final submission
            if (ModelState.IsValid)
            {
                List<OrderDetail> selectedItems = JsonSerializer.Deserialize<List<OrderDetail>>(OrderItemsJson);
                selectedItems.ForEach(x =>
                {
                    _context.Entry(x).Reference(x => x.Menu).Load();
                });

                order.OrderDetails = selectedItems;
                TempData["Order"] = JsonSerializer.Serialize(order);
                return RedirectToAction("Index", "OrderDetails");
            }

            return View(order);
        }
    }
}
