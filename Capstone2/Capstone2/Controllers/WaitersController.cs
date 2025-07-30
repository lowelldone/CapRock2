using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;

namespace Capstone2.Controllers
{
    public class WaitersController : Controller
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
            if (waiter.WaiterId == 0)
            {
                // ✅ Create User first
                waiter.User.Role = "WAITER";
                _context.Users.Add(waiter.User);
                _context.SaveChanges();

                // ✅ Link new User to Waiter
                waiter.UserId = waiter.User.UserId;

                _context.Waiters.Add(waiter);
            }
            else
            {
                _context.Waiters.Update(waiter);
            }

            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

        // GET: Waiters/Delete/5
        public IActionResult Delete(int id)
        {
            var waiter = _context.Waiters.Find(id);
            if (waiter == null)
                return NotFound();

            waiter.isDeleted = true;
            _context.Waiters.Update(waiter);
            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }

        // GET: Waiters/AssignedOrder/5
        public async Task<IActionResult> AssignedOrder(int id)
        {
            var waiter = await _context.Waiters
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.WaiterId == id);

            if (waiter == null)
                return NotFound();

            // Check if waiter is actually busy
            if (waiter.Availability != "Busy")
            {
                TempData["NoOrderAssigned"] = "This waiter is not currently assigned to any order.";
                return RedirectToAction(nameof(Index));
            }

            // Get the current active order for this waiter
            var assignedOrder = await _context.OrderWaiters
                .Include(ow => ow.Order)
                    .ThenInclude(o => o.Customer)
                .Include(ow => ow.Order)
                    .ThenInclude(o => o.HeadWaiter)
                        .ThenInclude(hw => hw.User)
                .Where(ow => ow.WaiterId == id && ow.Order.Status != "Completed")
                .Select(ow => ow.Order)
                .FirstOrDefaultAsync();

            if (assignedOrder == null)
            {
                TempData["NoOrderAssigned"] = "This waiter is not currently assigned to any active order.";
                return RedirectToAction(nameof(Index));
            }

            return View(assignedOrder);
        }

    }
}
