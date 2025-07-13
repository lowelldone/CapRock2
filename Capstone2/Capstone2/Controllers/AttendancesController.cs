using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;

namespace Capstone2.Controllers
{
    public class AttendancesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AttendancesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Attendances
        public IActionResult Index()
        {
            var waiters = _context.Waiters
                .Include(w => w.User)
                .ToList();

            var today = DateTime.Today;
            List<Attendance> attendances = _context.Attendances.ToList();

            return View(Tuple.Create(waiters, attendances));
        }

        // GET: Attendances/TimeIn?
        public IActionResult TimeIn(int waiterId)
        {
            var today = DateTime.Today;
            var existing = _context.Attendances
                .FirstOrDefault(a =>
                    a.WaiterId == waiterId &&
                    a.TimeIn.HasValue &&
                    a.TimeIn.Value.Date == today);

            if (existing == null)
            {
                var attendance = new Attendance
                {
                    WaiterId = waiterId,
                    TimeIn = DateTime.Now
                };
                _context.Attendances.Add(attendance);
                _context.SaveChanges();
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Attendances/TimeOut?waiterId=5
        public IActionResult TimeOut(int waiterId)
        {
            var today = DateTime.Today;
            var existing = _context.Attendances
                .FirstOrDefault(a =>
                    a.WaiterId == waiterId &&
                    a.TimeIn.HasValue &&
                    a.TimeIn.Value.Date == today);

            if (existing != null && !existing.TimeOut.HasValue)
            {
                existing.TimeOut = DateTime.Now;
                _context.Attendances.Update(existing);
                _context.SaveChanges();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
