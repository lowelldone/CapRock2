using Capstone2.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Capstone2.Data;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

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
            Models.User user = _context.Users.FirstOrDefault(u => u.Username == Username && u.Password == Password);

            if (user == null)
            {
                ViewBag.Error = "Invalid Admin credentials.";
                return View();
            }

            user.Role = user.Role.ToUpper();
            HttpContext.Session.SetString("Role", user.Role);
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("FirstName", user.FirstName);
            HttpContext.Session.SetInt32("UserId", user.UserId);

            string designatedPage = user.Role switch
            {
                "ADMIN" => "DashboardDateSummary", // change to dashboard
                "HEADWAITER" => "PaidOrders",
                _ => "Schedules"
            };

            return RedirectToAction("Index", designatedPage);

            //if (Role == "Admin")
            //{
            //    if (Username == "admin" && Password == "admin")
            //    {
            //        HttpContext.Session.SetString("Role", "Admin");
            //        HttpContext.Session.SetString("Username", Username);
            //        // Store UserId for admin if needed
            //        return RedirectToAction("AdminHomepage");
            //    }
            //    else
            //    {
            //        ViewBag.Error = "Invalid Admin credentials.";
            //        return View();
            //    }
            //}
            //else if (Role == "HeadWaiter" || Role == "Waiter")
            //{
            //    var user = _context.Users.FirstOrDefault(u => u.Username == Username && u.Password == Password && u.Role.ToUpper() == Role.ToUpper());
            //    if (user != null)
            //    {
            //        HttpContext.Session.SetString("Role", Role);
            //        HttpContext.Session.SetString("Username", user.Username);
            //        HttpContext.Session.SetInt32("UserId", user.UserId);
            //        if (Role == "HeadWaiter")
            //            return RedirectToAction("HeadWaiterHomepage");
            //        else
            //            return RedirectToAction("WaiterHomepage");
            //    }
            //    else
            //    {
            //        ViewBag.Error = $"Invalid {Role} credentials.";
            //        return View();
            //    }
            //}
            //else
            //{
            //    ViewBag.Error = "Invalid role selected.";
            //    return View();
            //}
        }

        //public IActionResult AdminHomepage()
        //{
        //    if (HttpContext.Session.GetString("Role") != "ADMIN")
        //        return RedirectToAction("Login");
        //    return View();
        //}

        //public IActionResult HeadWaiterHomepage()
        //{
        //    if (HttpContext.Session.GetString("Role") != "HEADWAITER")
        //        return RedirectToAction("Login");
        //    // Render the HeadWaiterHomepage view directly (do not redirect)
        //    return View();
        //}

        //public IActionResult WaiterHomepage()
        //{
        //    if (HttpContext.Session.GetString("Role") != "WAITER")
        //        return RedirectToAction("Login");
        //    return View();
        //}

        public IActionResult Logout()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var username = HttpContext.Session.GetString("Username");
                var role = HttpContext.Session.GetString("Role");
                var context = _context;
                context.AuditLogs.Add(new Capstone2.Models.AuditLog
                {
                    UserId = userId,
                    Username = username,
                    Role = role,
                    Action = nameof(Logout),
                    HttpMethod = "GET",
                    Route = HttpContext.Request.Path + HttpContext.Request.QueryString,
                    UserAgent = Request.Headers["User-Agent"].ToString(),
                    Succeeded = true,
                    Details = "User logged out"
                });
                context.SaveChanges();
            }
            catch { }
            HttpContext.Session.Clear();
            return RedirectToAction("Homepage", "Home");
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
