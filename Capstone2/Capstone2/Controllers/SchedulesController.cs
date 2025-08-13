using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;

namespace Capstone2.Controllers
{
    public class SchedulesController : GenericController
    {
        private readonly ApplicationDbContext _context;

        public SchedulesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Schedules
        public async Task<IActionResult> Index()
        {
            var role = (HttpContext.Session.GetString("Role") ?? string.Empty).ToUpper();

            List<Order> headWaiterOrders;
            List<Order> waiterOrders;

            if (role == "ADMIN")
            {
                // Admin: show all assigned orders across all staff
                headWaiterOrders = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.HeadWaiter)
                        .ThenInclude(hw => hw.User)
                    .Include(o => o.OrderWaiters)
                        .ThenInclude(ow => ow.Waiter)
                            .ThenInclude(w => w.User)
                    .Where(o => o.HeadWaiterId != null &&
                                o.Status != "Completed" &&
                                o.Status != "Cancelled")
                    .OrderBy(o => o.CateringDate)
                    .ThenBy(o => o.timeOfFoodServing)
                    .ToListAsync();

                waiterOrders = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.HeadWaiter)
                        .ThenInclude(hw => hw.User)
                    .Include(o => o.OrderWaiters)
                        .ThenInclude(ow => ow.Waiter)
                            .ThenInclude(w => w.User)
                    .Where(o => o.OrderWaiters.Any() &&
                                o.Status != "Completed" &&
                                o.Status != "Cancelled")
                    .OrderBy(o => o.CateringDate)
                    .ThenBy(o => o.timeOfFoodServing)
                    .ToListAsync();
            }
            else
            {
                // Non-admin: show only assignments related to the logged-in user
                headWaiterOrders = new List<Order>();
                waiterOrders = new List<Order>();

                var currentWaiter = await _context.Waiters
                    .Include(w => w.User)
                    .FirstOrDefaultAsync(w => w.UserId == userId);

                var currentHeadWaiter = await _context.HeadWaiters
                    .Include(hw => hw.User)
                    .FirstOrDefaultAsync(hw => hw.UserId == userId);

                if (currentWaiter != null)
                {
                    waiterOrders = await _context.OrderWaiters
                        .Include(ow => ow.Order)
                            .ThenInclude(o => o.Customer)
                        .Include(ow => ow.Order)
                            .ThenInclude(o => o.HeadWaiter)
                                .ThenInclude(hw => hw.User)
                        .Include(ow => ow.Order)
                            .ThenInclude(o => o.OrderWaiters)
                                .ThenInclude(ow2 => ow2.Waiter)
                                    .ThenInclude(w => w.User)
                        .Where(ow => ow.WaiterId == currentWaiter.WaiterId &&
                                    ow.Order.Status != "Completed" &&
                                    ow.Order.Status != "Cancelled")
                        .Select(ow => ow.Order)
                        .OrderBy(o => o.CateringDate)
                        .ThenBy(o => o.timeOfFoodServing)
                        .ToListAsync();
                }

                if (currentHeadWaiter != null)
                {
                    headWaiterOrders = await _context.Orders
                        .Include(o => o.Customer)
                        .Include(o => o.HeadWaiter)
                            .ThenInclude(hw => hw.User)
                        .Include(o => o.OrderWaiters)
                            .ThenInclude(ow => ow.Waiter)
                                .ThenInclude(w => w.User)
                        .Where(o => o.HeadWaiterId == currentHeadWaiter.HeadWaiterId &&
                                    o.Status != "Completed" &&
                                    o.Status != "Cancelled")
                        .OrderBy(o => o.CateringDate)
                        .ThenBy(o => o.timeOfFoodServing)
                        .ToListAsync();
                }
            }

            var viewModel = new SchedulesIndexViewModel
            {
                WaiterAssignedOrders = waiterOrders,
                HeadWaiterAssignedOrders = headWaiterOrders
            };

            return View(viewModel);
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

        // POST: Schedules/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string username, string currentPassword, string newPassword)
        {
            try
            {
                // Get current user from session
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    TempData["ProfileError"] = "User session not found. Please log in again.";
                    return RedirectToAction("Index");
                }

                // Find the current user
                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId.Value);
                if (currentUser == null)
                {
                    TempData["ProfileError"] = "User not found.";
                    return RedirectToAction("Index");
                }

                // Verify current password
                if (currentUser.Password != currentPassword)
                {
                    TempData["ProfileError"] = "Current password is incorrect.";
                    return RedirectToAction("Index");
                }

                // Check if new username already exists (if username is being changed)
                if (username != currentUser.Username)
                {
                    var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username && u.UserId != userId.Value);
                    if (existingUser != null)
                    {
                        TempData["ProfileError"] = "Username already exists. Please choose a different username.";
                        return RedirectToAction("Index");
                    }
                }

                // Update user information
                currentUser.Username = username;
                currentUser.Password = newPassword;
                _context.Users.Update(currentUser);
                await _context.SaveChangesAsync();

                TempData["ProfileSuccess"] = "Profile updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ProfileError"] = $"Error updating profile: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
    }
}