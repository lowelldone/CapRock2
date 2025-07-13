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
                .Include(w => w.HeadWaiter)
                    .ThenInclude(h => h.User)
                .Where(w => !w.isDeleted).ToList();

            return View(waiters);
        }

        // GET: Waiters/UpSert/5 or create
        public IActionResult UpSert(int? id)
        {
            ViewBag.HeadWaiterList = _context.HeadWaiters
                .Include(h => h.User)
                .ToList();

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
            // ✅ Validate HeadWaiter exists
            if (!_context.HeadWaiters.Any(h => h.HeadWaiterId == waiter.HeadWaiterId))
            {
                ModelState.AddModelError("HeadWaiterId", "The selected Head Waiter does not exist.");
                ViewBag.HeadWaiterList = _context.HeadWaiters.Include(h => h.User).ToList();
                return View(waiter);
            }

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
                //if (_context.Waiters.FirstOrDefault(w => w.WaiterId == waiter.WaiterId) == null) return RedirectToAction("Index");
                
                // ✅ Fetch existing waiter from DB to retain existing password if needed
                //var existingWaiter = _context.Waiters
                //    .Include(w => w.User)
                //    .FirstOrDefault(w => w.WaiterId == waiter.WaiterId);

                //if (existingWaiter != null)
                //{
                //    // Prevent password loss unless explicitly changed
                //    if (!string.IsNullOrWhiteSpace(waiter.User.Password))
                //    {
                //        existingWaiter.User.Password = waiter.User.Password;
                //    }
                //}

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
    }
}
