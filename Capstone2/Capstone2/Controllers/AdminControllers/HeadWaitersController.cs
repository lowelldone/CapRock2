using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;
using Microsoft.Identity.Client;

namespace Capstone2.Controllers.AdminControllers
{
    public class HeadWaitersController : Controller
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
                    existingUser.Role = "HEADWAITER";
                    _context.Users.Update(existingUser);
                    _context.SaveChanges();
                    headWaiter.UserId = existingUser.UserId;
                    headWaiter.User = existingUser;
                }
                else
                {
                    headWaiter.User.Role = "HEADWAITER";
                    _context.Users.Add(headWaiter.User);
                    _context.SaveChanges();
                    headWaiter.UserId = headWaiter.User.UserId;
                }
                headWaiter.isActive = true;
                _context.HeadWaiters.Add(headWaiter);
            }
            else
            {
                // Update user info if needed
                var user = _context.Users.FirstOrDefault(u => u.UserId == headWaiter.UserId);
                if (user != null)
                {
                    user.Username = headWaiter.User.Username;
                    user.Password = headWaiter.User.Password;
                    user.FirstName = headWaiter.User.FirstName;
                    user.LastName = headWaiter.User.LastName;
                    user.Role = "HEADWAITER";
                    _context.Users.Update(user);
                }
                _context.HeadWaiters.Update(headWaiter);
            }
            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Delete(int id)
        {
            var headWaiter = _context.HeadWaiters.Find(id);
            if (headWaiter == null)
                return NotFound();

            headWaiter.isActive = false;
            _context.HeadWaiters.Update(headWaiter);

            //// Check if this user is not used by any other HeadWaiter/Waiter
            //var userId = headWaiter.UserId;
            //bool userUsedElsewhere =
            //    _context.HeadWaiters.Any(h => h.UserId == userId && h.HeadWaiterId != id && h.isActive) ||
            //    _context.Waiters.Any(w => w.UserId == userId && !w.isDeleted);
            //if (!userUsedElsewhere)
            //{
            //    var user = _context.Users.Find(userId);
            //    if (user != null)
            //    {
            //        _context.Users.Remove(user);
            //    }
            //}

            // Only perform a soft delete; do not delete the User entity
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
    }
}