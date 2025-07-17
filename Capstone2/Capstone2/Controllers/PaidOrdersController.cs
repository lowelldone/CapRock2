using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;        // adjust namespace
using Capstone2.Models;
using Microsoft.AspNetCore.Mvc.Rendering;      // where your Customer model lives

namespace Capstone2.Controllers
{
    public class PaidOrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PaidOrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: PaidOrders
        public async Task<IActionResult> Index(string statusFilter)
        {
            var paidOrders = _context.Customers
                .Include(c => c.Order)
                .Where(c => c.IsPaid);

            if (!string.IsNullOrEmpty(statusFilter))
            {
                paidOrders = paidOrders.Where(c => c.Order.Status == statusFilter);
            }

            return View(await paidOrders.ToListAsync());
        }

        // GET: PaidOrders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return BadRequest();

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Menu)
                .FirstOrDefaultAsync(o => o.CustomerID == id.Value);

            if (order == null)
                return NotFound();

            return View(order);
        }
        // GET: PaidOrders/Attendances/5  (5 == CustomerId)
        public async Task<IActionResult> Attendances(int id)
        {
            // 1) find that customer’s order
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.CustomerID == id);
            if (order == null) return NotFound();

            // 2) pull in all waiters assigned & any existing attendance for this order
            var waiters = await _context.Waiters
                .Include(w => w.User)
                .Include(w => w.Attendance
                              .Where(a => a.OrderId == order.OrderId))
                .ToListAsync();

            var vm = new AttendancesViewModel
            {
                CustomerId = id,
                OrderId = order.OrderId,
                Waiters = waiters
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordTimeIn(int customerId, int orderId, int waiterId)
        {
            var att = await _context.Attendances
                .FirstOrDefaultAsync(a => a.OrderId == orderId
                                       && a.WaiterId == waiterId);

            if (att == null)
            {
                att = new Attendance
                {
                    OrderId = orderId,
                    WaiterId = waiterId,
                    TimeIn = DateTime.Now
                };
                _context.Attendances.Add(att);
            }
            else
            {
                att.TimeIn = DateTime.Now;
                _context.Attendances.Update(att);
            }
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Attendances), new { id = customerId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordTimeOut(int customerId, int orderId, int waiterId)
        {
            var att = await _context.Attendances
                .FirstOrDefaultAsync(a => a.OrderId == orderId
                                       && a.WaiterId == waiterId);
            if (att != null)
            {
                att.TimeOut = DateTime.Now;
                _context.Attendances.Update(att);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Attendances), new { id = customerId });
        }

        // GET: PaidOrders/DeployWaiter/5
        public IActionResult DeployWaiter(int id)
        {
            var customer = _context.Customers.Include(c => c.Order).FirstOrDefault(c => c.CustomerID == id);
            if (customer == null || customer.Order == null)
                return NotFound();

            // Only show waiters that are not deleted
            ViewBag.Waiters = _context.Waiters.Include(w => w.User).Where(w => !w.isDeleted).ToList();
            return View(customer.Order);
        }

        // POST: PaidOrders/DeployWaiter/5
        [HttpPost]
        public IActionResult DeployWaiter(int id, int waiterId)
        {
            var order = _context.Orders.FirstOrDefault(o => o.CustomerID == id);
            if (order == null)
                return NotFound();

            _context.Orders.Update(order);
            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }
    }
}
