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

        // GET: Attendance/ForOrder/{orderId}
        public IActionResult ForOrder(int orderId)
        {
            // Get all waiter IDs assigned to this order
            var assignedWaiterIds = _context.OrderWaiters
                .Where(ow => ow.OrderId == orderId)
                .Select(ow => ow.WaiterId)
                .ToList();

            // Get attendances for these waiters for this order
            var attendances = _context.Attendances
                .Include(a => a.Waiter)
                    .ThenInclude(w => w.User)
                .Where(a => a.OrderId == orderId && assignedWaiterIds.Contains(a.WaiterId))
                .ToList();

            // Get all assigned waiters (even if they have no attendance yet)
            var waiters = _context.Waiters
                .Include(w => w.User)
                .Where(w => assignedWaiterIds.Contains(w.WaiterId))
                .ToList();

            // Pass both lists as a tuple
            return View("OrderAttendance", (waiters, attendances));
        }

        [HttpPost]
        public IActionResult TimeInForOrder(int orderId, int waiterId)
        {
            var attendance = _context.Attendances
                .FirstOrDefault(a => a.OrderId == orderId && a.WaiterId == waiterId);

            if (attendance == null)
            {
                attendance = new Attendance
                {
                    OrderId = orderId,
                    WaiterId = waiterId,
                    TimeIn = DateTime.Now
                };
                _context.Attendances.Add(attendance);
            }
            else if (attendance.TimeIn == null)
            {
                attendance.TimeIn = DateTime.Now;
                _context.Attendances.Update(attendance);
            }
            _context.SaveChanges();
            return RedirectToAction("ForOrder", new { orderId });
        }

        [HttpPost]
        public IActionResult TimeOutForOrder(int orderId, int waiterId)
        {
            var attendance = _context.Attendances
                .FirstOrDefault(a => a.OrderId == orderId && a.WaiterId == waiterId);

            if (attendance != null && attendance.TimeOut == null)
            {
                attendance.TimeOut = DateTime.Now;
                _context.Attendances.Update(attendance);
                _context.SaveChanges();
            }
            return RedirectToAction("ForOrder", new { orderId });
        }
    }
}
