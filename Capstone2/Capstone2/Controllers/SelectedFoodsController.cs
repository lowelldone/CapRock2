// Controllers/SelectedFoodsController.cs
using Microsoft.AspNetCore.Mvc;
using Capstone2.Models;
using System.Collections.Generic;
using System.Text.Json;

namespace Capstone2.Controllers
{
    public class SelectedFoodsController : Controller
    {
        // GET: /SelectedFoods
        public IActionResult Index()
        {
            // GET: just show the empty page or fetch from TempData if you like
            ViewBag.SelectedItems = new List<OrderDetail>();
            return View();
        }

        // POST: /SelectedFoods
        [HttpPost]
        public IActionResult Index(string OrderItemsJson, double TotalPayment)
        {
            // 1) Deserialize the incoming JSON
            var items = JsonSerializer.Deserialize<List<SelectedItemDto>>(OrderItemsJson);

            // 2) (Optional) You could map these into full OrderDetail entities,
            //    save them to TempData, Session, or the database here.

            // 3) Pass them into your view:
            ViewBag.SelectedItems = items;
            ViewBag.TotalPayment = TotalPayment;

            return View();
        }
    }

    // A simple DTO to hold exactly what your JS posted
    public class SelectedItemDto
    {
        public int MenuId { get; set; }
        public string Name { get; set; }
        public double Price { get; set; }
        public int Quantity { get; set; }
    }
}
