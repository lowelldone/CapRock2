using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;

namespace Capstone2.Controllers
{
    public class SchedulesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SchedulesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Schedules
        public async Task<IActionResult> Index()
        {
            // Get the logged-in waiter's UserId from session
            var waiterUserId = HttpContext.Session.GetInt32("UserId");
            
            if (!waiterUserId.HasValue)
            {
                return RedirectToAction("Login", "Home");
            }

            // Get the waiter record for the logged-in user
            var waiter = await _context.Waiters
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.UserId == waiterUserId.Value);

            if (waiter == null)
            {
                return RedirectToAction("Login", "Home");
            }

            // Get orders assigned to this specific waiter
            var schedules = await _context.OrderWaiters
                .Include(ow => ow.Order)
                    .ThenInclude(o => o.Customer)
                .Include(ow => ow.Order)
                    .ThenInclude(o => o.HeadWaiter)
                        .ThenInclude(hw => hw.User)
                .Include(ow => ow.Order)
                    .ThenInclude(o => o.OrderWaiters)
                        .ThenInclude(ow2 => ow2.Waiter)
                            .ThenInclude(w => w.User)
                .Where(ow => ow.WaiterId == waiter.WaiterId && 
                            ow.Order.Status != "Completed" && 
                            ow.Order.Status != "Cancelled")
                .Select(ow => ow.Order)
                .OrderBy(o => o.CateringDate)
                .ThenBy(o => o.timeOfFoodServing)
                .ToListAsync();

            return View(schedules);
        }

        // GET: Schedules/Details/5
        public async Task<IActionResult> Details(int id)
        {
            // Get the logged-in waiter's UserId from session
            var waiterUserId = HttpContext.Session.GetInt32("UserId");
            
            if (!waiterUserId.HasValue)
            {
                return RedirectToAction("Login", "Home");
            }

            // Get the waiter record for the logged-in user
            var waiter = await _context.Waiters
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.UserId == waiterUserId.Value);

            if (waiter == null)
            {
                return RedirectToAction("Login", "Home");
            }

            // Get the order and verify the waiter is assigned to it
            var orderWaiter = await _context.OrderWaiters
                .Include(ow => ow.Order)
                    .ThenInclude(o => o.Customer)
                .Include(ow => ow.Order)
                    .ThenInclude(o => o.HeadWaiter)
                        .ThenInclude(hw => hw.User)
                .Include(ow => ow.Order)
                    .ThenInclude(o => o.OrderWaiters)
                        .ThenInclude(ow2 => ow2.Waiter)
                            .ThenInclude(w => w.User)
                .Include(ow => ow.Order)
                    .ThenInclude(o => o.OrderDetails)
                        .ThenInclude(od => od.Menu)
                .FirstOrDefaultAsync(ow => ow.OrderId == id && ow.WaiterId == waiter.WaiterId);

            if (orderWaiter?.Order == null)
            {
                return NotFound();
            }

            return View(orderWaiter.Order);
        }
    }
} 