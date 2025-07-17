using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;

namespace Capstone2.Controllers
{
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AttendanceController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Attendance
        public IActionResult Index()
        {
            var attendances = _context.Attendances
                .Include(a => a.Waiter)
                    .ThenInclude(w => w.User)
                .ToList();

            return View(attendances);
        }

        // GET: Attendance/Create
        public IActionResult Create()
        {
            ViewBag.Waiters = _context.Waiters
                .Include(w => w.User)
                .Where(w => !w.isDeleted)
                .ToList();

            return View();
        }

        // POST: Attendance/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Attendance attendance)
        {
            if (ModelState.IsValid)
            {
                _context.Attendances.Add(attendance);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Waiters = _context.Waiters
                .Include(w => w.User)
                .Where(w => !w.isDeleted)
                .ToList();

            return View(attendance);
        }

        // GET: Attendance/Delete/5
        public IActionResult Delete(int id)
        {
            var attendance = _context.Attendances.Find(id);
            if (attendance == null) return NotFound();

            _context.Attendances.Remove(attendance);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
    }
}
