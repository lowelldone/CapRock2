using Capstone2.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Capstone2.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Homepage()
        {
            // note: get user role first and put it on ViewBag
            if (HttpContext.Session.GetInt32("user").HasValue)
            {
                // ViewBag.Role = get role
            }

            return View();
        }

        public IActionResult Login()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
