using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;
using Microsoft.Identity.Client;

namespace Capstone2.Controllers.AdminControllers
{
    public class HeadWaitersController : GenericController
    {
        private readonly ApplicationDbContext _context;

        public HeadWaitersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: HeadWaiters
        public IActionResult Index()
        {
            List<HeadWaiter> headWaiters = _context.HeadWaiters.Include(h => h.User).Where(h => h.isActive).ToList();
            return View(headWaiters);
        }

        public IActionResult UpSert(int? id)
        {
            return View(id == null ? new HeadWaiter() { User = new User() } : _context.HeadWaiters.Include(h => h.User).First(h => h.HeadWaiterId == id));
        }

        [HttpPost]
        public IActionResult UpSert(HeadWaiter headWaiter)
        {
            // Sanitize UserNumber: keep digits only and cap to 11
            if (headWaiter?.User != null)
            {
                var raw = headWaiter.User.UserNumber ?? string.Empty;
                var digitsOnly = new string(raw.Where(char.IsDigit).ToArray());
                if (digitsOnly.Length > 11)
                {
                    digitsOnly = digitsOnly.Substring(0, 11);
                }
                headWaiter.User.UserNumber = digitsOnly;
            }

            // Validate: must be exactly 11 digits
            if (string.IsNullOrWhiteSpace(headWaiter.User?.UserNumber) || headWaiter.User.UserNumber.Length != 11)
            {
                ModelState.AddModelError("User.UserNumber", "User Number must be exactly 11 digits.");
                return View(headWaiter);
            }

            if (headWaiter.HeadWaiterId == 0)
            {
                // Check if a user with the same username exists
                var existingUser = _context.Users.FirstOrDefault(u => u.Username == headWaiter.User.Username);
                if (existingUser != null)
                {
                    // Update existing user details
                    existingUser.Password = headWaiter.User.Password;
                    existingUser.FirstName = headWaiter.User.FirstName;
                    existingUser.LastName = headWaiter.User.LastName;
                    existingUser.Role = "HeadWaiter";
                    existingUser.UserNumber = headWaiter.User.UserNumber;
                    _context.Users.Update(existingUser);
                    _context.SaveChanges();
                    headWaiter.UserId = existingUser.UserId;
                    headWaiter.User = existingUser;
                }
                else
                {
                    headWaiter.User.Role = "HeadWaiter";
                    _context.Users.Add(headWaiter.User);
                    _context.SaveChanges();
                    headWaiter.UserId = headWaiter.User.UserId;
                }
                headWaiter.isActive = true;
                _context.HeadWaiters.Add(headWaiter);
            }
            else
            {
                // Update existing head waiter and its associated user
                var existingHeadWaiter = _context.HeadWaiters
                    .Include(h => h.User)
                    .FirstOrDefault(h => h.HeadWaiterId == headWaiter.HeadWaiterId);

                if (existingHeadWaiter != null)
                {
                    // Update head waiter properties
                    existingHeadWaiter.isActive = headWaiter.isActive;
                    _context.HeadWaiters.Update(existingHeadWaiter);

                    // Update user credentials using the existing UserId
                    if (existingHeadWaiter.User != null)
                    {
                        existingHeadWaiter.User.Username = headWaiter.User.Username;
                        existingHeadWaiter.User.Password = headWaiter.User.Password;
                        existingHeadWaiter.User.FirstName = headWaiter.User.FirstName;
                        existingHeadWaiter.User.LastName = headWaiter.User.LastName;
                        existingHeadWaiter.User.Role = "HeadWaiter";
                        existingHeadWaiter.User.UserNumber = headWaiter.User.UserNumber;
                        _context.Users.Update(existingHeadWaiter.User);
                    }
                }
            }
            _context.SaveChanges();

            if (headWaiter.HeadWaiterId == 0)
            {
                TempData["Success"] = "Head Waiter created successfully!";
            }
            else
            {
                TempData["Success"] = "Head Waiter credentials updated successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: HeadWaiters/ViewOrders/5
        //public async Task<IActionResult> ViewOrders(int id)
        //{
        //    var headWaiter = await _context.HeadWaiters
        //        .Include(h => h.User)
        //        .FirstOrDefaultAsync(h => h.HeadWaiterId == id);

        //    if (headWaiter == null)
        //        return NotFound();

        //    // Get all orders assigned to this head waiter
        //    var assignedOrders = await _context.Orders
        //        .Include(o => o.Customer)
        //        .Include(o => o.OrderWaiters)
        //            .ThenInclude(ow => ow.Waiter)
        //                .ThenInclude(w => w.User)
        //        .Where(o => o.HeadWaiterId == id && !o.isDeleted)
        //        .OrderByDescending(o => o.CateringDate)
        //        .ThenByDescending(o => o.OrderDate)
        //        .ToListAsync();

        //    ViewBag.HeadWaiter = headWaiter;
        //    return View(assignedOrders);
        //}

        // GET: HeadWaiters/Delete/5
        public IActionResult Delete(int id)
        {
            var headWaiter = _context.HeadWaiters.Find(id);
            if (headWaiter == null)
                return NotFound();

            headWaiter.isActive = false;
            _context.HeadWaiters.Update(headWaiter);

            // Only perform a soft delete; do not delete the User entity
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
    }
}