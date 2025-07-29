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
            List<OrderDetail> selectedItems = JsonSerializer.Deserialize<List<OrderDetail>>(OrderItemsJson);

            selectedItems.ForEach(x =>
            {
                _context.Entry(x).Reference(x => x.Menu).Load();
            });
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
    }
}
