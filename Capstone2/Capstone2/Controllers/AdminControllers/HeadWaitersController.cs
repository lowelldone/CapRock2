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
                ModelState.AddModelError("User.UserNumber", "Phone Number must be exactly 11 digits.");
                return View(headWaiter);
            }

            bool isCreateAction = headWaiter.HeadWaiterId == 0;

            if (isCreateAction)
            {
                // Check if a user with the same username exists
                var existingUser = _context.Users.FirstOrDefault(u => u.Username == headWaiter.User.Username);
                if (existingUser != null)
                {
                    // Prevent creating new account with existing username
                    ModelState.AddModelError("User.Username", "Username already exists. Please choose a different username.");
                    return View(headWaiter);
                }

                // Create new user for new head waiter
                headWaiter.User.Role = "HeadWaiter";
                _context.Users.Add(headWaiter.User);
                _context.SaveChanges();
                headWaiter.UserId = headWaiter.User.UserId;
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
                    // Check if the new username conflicts with other users (excluding current user)
                    var conflictingUser = _context.Users.FirstOrDefault(u =>
                        u.Username == headWaiter.User.Username &&
                        u.UserId != existingHeadWaiter.UserId);

                    if (conflictingUser != null)
                    {
                        ModelState.AddModelError("User.Username", "Username already exists. Please choose a different username.");
                        return View(headWaiter);
                    }

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

            // Audit: head waiter upsert (create/update)
            try
            {
                var role = HttpContext.Session.GetString("Role");
                var username = HttpContext.Session.GetString("Username");

                string details;
                if (isCreateAction)
                {
                    details = $"Created head waiter {headWaiter.HeadWaiterId} with username '{headWaiter.User?.Username}'";
                }
                else
                {
                    // For updates, provide more detailed information about what changed
                    var existingHeadWaiter = _context.HeadWaiters
                        .Include(h => h.User)
                        .FirstOrDefault(h => h.HeadWaiterId == headWaiter.HeadWaiterId);

                    if (existingHeadWaiter?.User != null)
                    {
                        var changes = new List<string>();

                        if (existingHeadWaiter.User.Username != headWaiter.User.Username)
                            changes.Add($"username: '{existingHeadWaiter.User.Username}' to '{headWaiter.User.Username}'");
                        if (existingHeadWaiter.User.Password != headWaiter.User.Password)
                            changes.Add("password changed");
                        if (existingHeadWaiter.User.FirstName != headWaiter.User.FirstName)
                            changes.Add($"first name: '{existingHeadWaiter.User.FirstName}' to '{headWaiter.User.FirstName}'");
                        if (existingHeadWaiter.User.LastName != headWaiter.User.LastName)
                            changes.Add($"last name: '{existingHeadWaiter.User.LastName}' to '{headWaiter.User.LastName}'");
                        if (existingHeadWaiter.User.UserNumber != headWaiter.User.UserNumber)
                            changes.Add($"user number: '{existingHeadWaiter.User.UserNumber}' to '{headWaiter.User.UserNumber}'");
                        if (existingHeadWaiter.isActive != headWaiter.isActive)
                            changes.Add($"active status: {existingHeadWaiter.isActive} to {headWaiter.isActive}");

                        details = changes.Any() ?
                            $"Updated head waiter {headWaiter.HeadWaiterId} ({string.Join(", ", changes)})" :
                            $"Updated head waiter {headWaiter.HeadWaiterId} (Credentials Changed.)";
                    }
                    else
                    {
                        details = $"Updated head waiter {headWaiter.HeadWaiterId} credentials for username '{headWaiter.User?.Username}'";
                    }
                }

                _context.AuditLogs.Add(new AuditLog
                {
                    Username = username,
                    Role = role,
                    Action = nameof(UpSert),
                    WaiterId = null, // Head waiters don't have WaiterId
                    Details = details
                });
                _context.SaveChanges();
            }
            catch { }

            if (isCreateAction)
            {
                TempData["Success"] = "Head Waiter created successfully!";
            }
            else
            {
                TempData["Success"] = "Head Waiter credentials updated successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: HeadWaiters/Delete/5
        public IActionResult Delete(int id)
        {
            var headWaiter = _context.HeadWaiters
                .Include(h => h.User)
                .FirstOrDefault(h => h.HeadWaiterId == id);

            if (headWaiter == null)
                return NotFound();

            return View(headWaiter);
        }

        // POST: HeadWaiters/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var headWaiter = _context.HeadWaiters
                .Include(h => h.User)
                .FirstOrDefault(h => h.HeadWaiterId == id);

            if (headWaiter == null)
                return NotFound();

            headWaiter.isActive = false;
            _context.HeadWaiters.Update(headWaiter);

            // Only perform a soft delete; do not delete the User entity
            _context.SaveChanges();

            // Audit: head waiter deactivation
            try
            {
                var role = HttpContext.Session.GetString("Role");
                var userId = HttpContext.Session.GetInt32("UserId");
                var username = HttpContext.Session.GetString("Username");
                _context.AuditLogs.Add(new AuditLog
                {
                    Username = username,
                    Role = role,
                    Action = nameof(Delete),
                    WaiterId = null, // Head waiters don't have WaiterId
                    Details = $"Deactivated head waiter {id} with username '{headWaiter.User?.Username}'"
                });
                _context.SaveChanges();
            }
            catch { }

            TempData["Success"] = "Head Waiter deactivated successfully!";
            return RedirectToAction(nameof(Index));
        }
    }
}