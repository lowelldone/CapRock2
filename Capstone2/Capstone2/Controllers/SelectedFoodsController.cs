using Microsoft.AspNetCore.Mvc;
using Capstone2.Models;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using System.ComponentModel.Design;

namespace Capstone2.Controllers
{
    public class SelectedFoodsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public SelectedFoodsController(ApplicationDbContext context)
        {
            _context = context;
        }
        // POST: /SelectedFoods
        [HttpPost]
        public IActionResult Index(string OrderItemsJson, Order? order, bool isConfirmed = false)
        {
            // Create a DTO class to match the JSON structure
            var jsonItems = JsonSerializer.Deserialize<List<OrderItemDto>>(OrderItemsJson);
            List<OrderDetail> selectedItems = new List<OrderDetail>();

            foreach (var jsonItem in jsonItems)
            {
                // Create OrderDetail and load Menu data
                var orderDetail = new OrderDetail
                {
                    MenuId = jsonItem.MenuId,
                    Quantity = jsonItem.Quantity
                };

                // Load the Menu navigation property
                _context.Entry(orderDetail).Reference(x => x.Menu).Load();

                selectedItems.Add(orderDetail);
            }

            // Step 1: From ClientMenus
            if (!isConfirmed)
            {
                TempData["OrderItemsJson"] = OrderItemsJson;
                ViewBag.SelectedItems = selectedItems;
                return View(order);
            }
            order.OrderDetails = selectedItems;
            order.OrderDetails.ForEach(x => order.TotalPayment += x.subTotal);
            TempData["Order"] = JsonSerializer.Serialize(order);
            return Json(new { success = true });
        }

        // DTO class to match the JSON structure from ClientMenus
        public class OrderItemDto
        {
            public int MenuId { get; set; }
            public string Name { get; set; }
            public double Price { get; set; }
            public int Quantity { get; set; }
        }
    }
}
