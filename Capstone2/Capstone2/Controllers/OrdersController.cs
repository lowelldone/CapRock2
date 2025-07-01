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

        [HttpPost]
        public IActionResult Form(string OrderItemsJson, Order? order)
        {
            List<OrderDetail> selectedItems = JsonSerializer.Deserialize<List<OrderDetail>>(OrderItemsJson);
            

            // Step 1: From ClientMenus
            if (!string.IsNullOrEmpty(OrderItemsJson) && order?.Customer == null)
            {
                TempData["OrderItemsJson"] = OrderItemsJson;
                ViewBag.SelectedItems = selectedItems;

                order.OrderDate = DateTime.Now.Date;
                order.Customer = new Customer();

                return View(order);
            }

            // Step 2: Final submission
            if (ModelState.IsValid && order != null)
            {
                selectedItems.ForEach(x =>
                {
                    _context.Entry(x).Reference(x => x.Menu).Load();
                });

                order.OrderDetails = selectedItems;
                TempData["Order"] = JsonSerializer.Serialize(order);
               

                return RedirectToAction("Index", "OrderDetails");
            }

            return View(order ?? new Order());
        }
    }
}
