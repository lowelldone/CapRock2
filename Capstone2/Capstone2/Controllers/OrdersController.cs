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
            // If this is the initial post from the client menu (OrderItemsJson provided)
            if (!string.IsNullOrEmpty(OrderItemsJson))
            {
                var orderModel = new Order
                {
                    OrderDate = DateTime.Now.Date,
                    timeOfFoodServing = DateTime.Now
                };

                var selectedItems = JsonSerializer.Deserialize<List<OrderDetail>>(OrderItemsJson);
                ViewBag.SelectedItems = selectedItems;

                return View(orderModel);
            }

            // Else this is the final form submission with full Order data
            if (ModelState.IsValid && order != null)
            {
                var customer = new Customer
                {
                    Name = order.Customer.Name
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
