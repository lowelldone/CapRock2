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
                headWaiter.User.Role = "HEADWAITER";
                headWaiter.isActive = true;
                _context.HeadWaiters.Add(headWaiter);
            }
            else
            {
                _context.HeadWaiters.Update(headWaiter);
            }
            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Delete(int id)
        {
            HeadWaiter headWaiter = _context.HeadWaiters.Find(id);
            headWaiter.isActive = false;

            _context.HeadWaiters.Update(headWaiter);
            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }
    }
}