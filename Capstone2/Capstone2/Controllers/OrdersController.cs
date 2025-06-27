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

                return View(new Order
                {
                    OrderDate = DateTime.Now.Date,
                    Customer = new Customer()
                });
            }

            // Step 2: Final submission
            if (ModelState.IsValid && order != null)
            {
                order.OrderDetails = selectedItems;
                var customer = new Customer
                {
                    Name = order.Customer.Name,
                    ContactNo = order.Customer.ContactNo,
                    Address = order.Customer.Address
                };
                _context.Customers.Add(customer);
                _context.SaveChanges();

                order.CustomerID = customer.CustomerID;
                order.Customer = null;

                _context.Orders.Add(order);
                _context.SaveChanges();

                

                return RedirectToAction("ConfirmOrder", "OrderDetails", new { orderId = order.OrderId });
            }

            return View(order ?? new Order());
        }
    }
}
