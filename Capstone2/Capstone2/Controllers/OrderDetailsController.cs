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
    }
}
