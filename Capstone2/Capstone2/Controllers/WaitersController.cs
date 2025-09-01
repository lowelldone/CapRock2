using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;

namespace Capstone2.Controllers
{
    public class WaitersController : GenericController
    {
        private readonly ApplicationDbContext _context;

        public WaitersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Waiters
        public IActionResult Index()
        {
            var waiters = _context.Waiters
                .Include(w => w.User)
                .Where(w => !w.isDeleted).ToList();

            return View(waiters);
        }

        // GET: Waiters/UpSert/5 or create
        public IActionResult UpSert(int? id)
        {
            if (id == null)
            {
                return View(new Waiter
                {
                    User = new User() // for form binding
                });
            }

            var waiter = _context.Waiters
                .Include(w => w.User)
                .FirstOrDefault(w => w.WaiterId == id);

            return View(waiter);
        }

        // POST: Waiters/UpSert
        [HttpPost]
        public IActionResult UpSert(Waiter waiter)
        {
            // Sanitize UserNumber: keep digits only and cap to 11
            if (waiter?.User != null)
            {
                var raw = waiter.User.UserNumber ?? string.Empty;
                var digitsOnly = new string(raw.Where(char.IsDigit).ToArray());
                if (digitsOnly.Length > 11)
                {
                    digitsOnly = digitsOnly.Substring(0, 11);
                }
                waiter.User.UserNumber = digitsOnly;
            }

            // Validate: must be exactly 11 digits
            if (string.IsNullOrWhiteSpace(waiter.User?.UserNumber) || waiter.User.UserNumber.Length != 11)
            {
                ModelState.AddModelError("User.UserNumber", "Phone Number must be exactly 11 digits.");
                return View(waiter);
            }

            if (waiter.WaiterId == 0)
            {
                // Check if a user with the same username exists
                var existingUser = _context.Users.FirstOrDefault(u => u.Username == waiter.User.Username);
                if (existingUser != null)
                {
                    // Prevent creating new account with existing username
                    ModelState.AddModelError("User.Username", "Username already exists. Please choose a different username.");
                    return View(waiter);
                }

                // ✅ Create User first
                waiter.User.Role = "Waiter";
                _context.Users.Add(waiter.User);
                _context.SaveChanges();

                // ✅ Link new User to Waiter
                waiter.UserId = waiter.User.UserId;

                _context.Waiters.Add(waiter);
            }
            else
            {
                // Update existing waiter and its associated user
                var existingWaiter = _context.Waiters
                    .Include(w => w.User)
                    .FirstOrDefault(w => w.WaiterId == waiter.WaiterId);

                if (existingWaiter != null)
                {
                    // Check if the new username conflicts with other users (excluding current user)
                    var conflictingUser = _context.Users.FirstOrDefault(u =>
                        u.Username == waiter.User.Username &&
                        u.UserId != existingWaiter.UserId);

                    if (conflictingUser != null)
                    {
                        ModelState.AddModelError("User.Username", "Username already exists. Please choose a different username.");
                        return View(waiter);
                    }

                    // Update waiter properties
                    existingWaiter.isTemporary = waiter.isTemporary;
                    existingWaiter.Availability = waiter.Availability;
                    _context.Waiters.Update(existingWaiter);

                    // Update user credentials
                    if (existingWaiter.User != null)
                    {
                        existingWaiter.User.Username = waiter.User.Username;
                        existingWaiter.User.Password = waiter.User.Password;
                        existingWaiter.User.FirstName = waiter.User.FirstName;
                        existingWaiter.User.LastName = waiter.User.LastName;
                        existingWaiter.User.Role = "Waiter";
                        existingWaiter.User.UserNumber = waiter.User.UserNumber;
                        _context.Users.Update(existingWaiter.User);
                    }
                }
            }

            bool isCreateAction = waiter.WaiterId == 0;
            _context.SaveChanges();

            // Audit: waiter upsert (create/update)
            try
            {
                var role = HttpContext.Session.GetString("Role");
                var userId = HttpContext.Session.GetInt32("UserId");
                var username = HttpContext.Session.GetString("Username");

                string details;
                if (isCreateAction)
                {
                    details = $"Created waiter {waiter.WaiterId} with username '{waiter.User?.Username}'";
                }
                else
                {
                    // For updates, provide more detailed information about what changed
                    var existingWaiter = _context.Waiters
                        .Include(w => w.User)
                        .FirstOrDefault(w => w.WaiterId == waiter.WaiterId);

                    if (existingWaiter?.User != null)
                    {
                        var changes = new List<string>();

                        if (existingWaiter.User.Username != waiter.User.Username)
                            changes.Add($"username: '{existingWaiter.User.Username}' to '{waiter.User.Username}'");
                        if (existingWaiter.User.Password != waiter.User.Password)
                            changes.Add("password changed");
                        if (existingWaiter.User.FirstName != waiter.User.FirstName)
                            changes.Add($"first name: '{existingWaiter.User.FirstName}' to '{waiter.User.FirstName}'");
                        if (existingWaiter.User.LastName != waiter.User.LastName)
                            changes.Add($"last name: '{existingWaiter.User.LastName}' to '{waiter.User.LastName}'");
                        if (existingWaiter.User.UserNumber != waiter.User.UserNumber)
                            changes.Add($"user number: '{existingWaiter.User.UserNumber}' to '{waiter.User.UserNumber}'");
                        if (existingWaiter.isTemporary != waiter.isTemporary)
                            changes.Add($"temporary status: {existingWaiter.isTemporary} to {waiter.isTemporary}");
                        if (existingWaiter.Availability != waiter.Availability)
                            changes.Add($"availability: '{existingWaiter.Availability}' to '{waiter.Availability}'");

                        details = changes.Any() ?
                            $"Updated waiter {waiter.WaiterId} ({string.Join(", ", changes)})" :
                            $"Updated waiter {waiter.WaiterId} (Credentials Changed.)";
                    }
                    else
                    {
                        details = $"Updated waiter {waiter.WaiterId} credentials for username '{waiter.User?.Username}'";
                    }
                }

                _context.AuditLogs.Add(new AuditLog
                {
                    Username = username,
                    Role = role,
                    Action = nameof(UpSert),
                    WaiterId = waiter.WaiterId,
                    Details = details
                });
                _context.SaveChanges();
            }
            catch { }

            if (waiter.WaiterId == 0)
            {
                TempData["Success"] = "Waiter created successfully!";
            }
            else
            {
                TempData["Success"] = "Waiter credentials updated successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Waiters/Delete/5
        public IActionResult Delete(int id)
        {
            var waiter = _context.Waiters
                .Include(w => w.User)
                .FirstOrDefault(w => w.WaiterId == id);

            if (waiter == null)
                return NotFound();

            return View(waiter);
        }

        // POST: Waiters/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var waiter = _context.Waiters
                .Include(w => w.User)
                .FirstOrDefault(w => w.WaiterId == id);

            if (waiter == null)
                return NotFound();

            waiter.isDeleted = true;
            _context.Waiters.Update(waiter);
            _context.SaveChanges();

            // Audit: waiter delete (soft)
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
                    WaiterId = id,
                    Details = $"Deactivated waiter {id} with username '{waiter.User?.Username}' (Name: {waiter.User?.FirstName} {waiter.User?.LastName})"
                });
                _context.SaveChanges();
            }
            catch { }

            TempData["Success"] = "Waiter deactivated successfully!";
            return RedirectToAction(nameof(Index));
        }
    }
}
