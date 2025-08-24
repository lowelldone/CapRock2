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
                ModelState.AddModelError("User.UserNumber", "User Number must be exactly 11 digits.");
                return View(waiter);
            }

            if (waiter.WaiterId == 0)
            {
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
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Username = username,
                    Role = role,
                    Action = nameof(UpSert),
                    HttpMethod = "POST",
                    Route = HttpContext.Request.Path + HttpContext.Request.QueryString,
                    UserAgent = Request.Headers["User-Agent"].ToString(),
                    Succeeded = true,
                    WaiterId = waiter.WaiterId,
                    Details = isCreateAction ? $"Created waiter {waiter.WaiterId}" : $"Updated waiter {waiter.WaiterId}"
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
            var waiter = _context.Waiters.Find(id);
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
                    UserId = userId,
                    Username = username,
                    Role = role,
                    Action = nameof(Delete),
                    HttpMethod = "POST",
                    Route = HttpContext.Request.Path + HttpContext.Request.QueryString,
                    UserAgent = Request.Headers["User-Agent"].ToString(),
                    Succeeded = true,
                    WaiterId = id,
                    Details = $"Soft deleted waiter {id}"
                });
                _context.SaveChanges();
            }
            catch { }

            TempData["Success"] = "Waiter deactivated successfully!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Waiters/AssignedOrder/5
        //public async Task<IActionResult> AssignedOrder(int id)
        //{
        //    var waiter = await _context.Waiters
        //        .Include(w => w.User)
        //        .FirstOrDefaultAsync(w => w.WaiterId == id);

        //    if (waiter == null)
        //        return NotFound();

        //    // Check if waiter is actually busy
        //    if (waiter.Availability != "Busy")
        //    {
        //        TempData["NoOrderAssigned"] = "This waiter is not currently assigned to any order.";
        //        return RedirectToAction(nameof(Index));
        //    }

        //    // Get the current active order for this waiter
        //    var assignedOrder = await _context.OrderWaiters
        //        .Include(ow => ow.Order)
        //            .ThenInclude(o => o.Customer)
        //        .Include(ow => ow.Order)
        //            .ThenInclude(o => o.HeadWaiter)
        //                .ThenInclude(hw => hw.User)
        //        .Where(ow => ow.WaiterId == id && !ow.Order.isDeleted && ow.Order.Status != "Completed")
        //        .Select(ow => ow.Order)
        //        .FirstOrDefaultAsync();

        //    if (assignedOrder == null)
        //    {
        //        TempData["NoOrderAssigned"] = "This waiter is not currently assigned to any active order.";
        //        return RedirectToAction(nameof(Index));
        //    }

        //    return View(assignedOrder);
        //}

    }
}
