using Capstone2.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Capstone2.Data;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace Capstone2.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
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

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string Role, string Username, string Password)
        {
            if (Role == "Admin")
            {
                if (Username == "admin" && Password == "admin")
                {
                    HttpContext.Session.SetString("Role", "Admin");
                    HttpContext.Session.SetString("Username", Username);
                    return RedirectToAction("AdminHomepage");
                }
                else
                {
                    ViewBag.Error = "Invalid Admin credentials.";
                    return View();
                }
            }
            else if (Role == "HeadWaiter" || Role == "Waiter")
            {
                var user = _context.Users.FirstOrDefault(u => u.Username == Username && u.Password == Password && u.Role.ToUpper() == Role.ToUpper());
                if (user != null)
                {
                    HttpContext.Session.SetString("Role", Role);
                    HttpContext.Session.SetString("Username", user.Username);
                    if (Role == "HeadWaiter")
                        return RedirectToAction("HeadWaiterHomepage");
                    else
                        return RedirectToAction("WaiterHomepage");
                }
                else
                {
                    ViewBag.Error = $"Invalid {Role} credentials.";
                    return View();
                }
            }
            else
            {
                ViewBag.Error = "Invalid role selected.";
                return View();
            }
        }

        public IActionResult AdminHomepage()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToAction("Login");
            return View();
        }

        public IActionResult HeadWaiterHomepage()
        {
            if (HttpContext.Session.GetString("Role") != "HeadWaiter")
                return RedirectToAction("Login");
            return View();
        }

        public IActionResult WaiterHomepage()
        {
            if (HttpContext.Session.GetString("Role") != "Waiter")
                return RedirectToAction("Login");
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
