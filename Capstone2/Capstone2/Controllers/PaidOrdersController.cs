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
        public async Task<IActionResult> Index(string statusFilter, int? headWaiterId)
        {
            // If not provided, get from session for logged-in headwaiters
            if (!headWaiterId.HasValue && HttpContext.Session.GetString("Role") == "HeadWaiter")
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId != null)
                {
                    var headWaiter = _context.HeadWaiters.FirstOrDefault(h => h.UserId == userId.Value && h.isActive);
                    if (headWaiter != null)
                        headWaiterId = headWaiter.HeadWaiterId;
                }
            }

            var paidOrders = _context.Customers
                .Include(c => c.Order)
                    .ThenInclude(o => o.HeadWaiter)
                .Where(c => c.Order != null && c.Order.AmountPaid >= 0.5 * c.Order.TotalPayment);

            if (headWaiterId.HasValue)
            {
                paidOrders = paidOrders.Where(c => c.Order.HeadWaiterId == headWaiterId.Value);
            }

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

            var order = customer.Order;
            // Get all waiters that are not deleted
            var waiters = _context.Waiters.Include(w => w.User).Where(w => !w.isDeleted).ToList();
            // Get IDs of waiters already assigned to this order
            var assignedWaiterIds = _context.OrderWaiters.Where(ow => ow.OrderId == order.OrderId).Select(ow => ow.WaiterId).ToList();

            ViewBag.Waiters = waiters;
            ViewBag.AssignedWaiterIds = assignedWaiterIds;
            return View(order);
        }

        // POST: PaidOrders/DeployWaiter/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeployWaiter(int id, int[] waiterIds, int? headWaiterId)
        {
            var order = _context.Orders.FirstOrDefault(o => o.CustomerID == id);
            if (order == null)
                return NotFound();

            // Declare assignedWaiterIds at the top
            List<int> assignedWaiterIds;

            // Check for busy waiters
            var busyWaiters = _context.Waiters
                .Where(w => waiterIds.Contains(w.WaiterId) && w.Availability == "Busy")
                .Include(w => w.User)
                .ToList();

            if (busyWaiters.Any())
            {
                var names = string.Join(", ", busyWaiters.Select(w => $"{w.User.FirstName} {w.User.LastName}"));
                ModelState.AddModelError("", $"Selected waiter(s) already deployed to other orders: {names}. Please select again.");

                // Repopulate view data
                var waiters = _context.Waiters.Include(w => w.User).Where(w => !w.isDeleted).ToList();
                assignedWaiterIds = _context.OrderWaiters.Where(ow => ow.OrderId == order.OrderId).Select(ow => ow.WaiterId).ToList();
                ViewBag.Waiters = waiters;
                ViewBag.AssignedWaiterIds = assignedWaiterIds;
                return View(order);
            }

            // Get already assigned waiter IDs
            assignedWaiterIds = _context.OrderWaiters.Where(ow => ow.OrderId == order.OrderId).Select(ow => ow.WaiterId).ToList();

            foreach (var waiterId in waiterIds)
            {
                if (!assignedWaiterIds.Contains(waiterId))
                {
                    // Assign waiter to order
                    var orderWaiter = new OrderWaiter { OrderId = order.OrderId, WaiterId = waiterId };
                    _context.OrderWaiters.Add(orderWaiter);
                }
                // Set waiter status to Busy
                var waiter = _context.Waiters.FirstOrDefault(w => w.WaiterId == waiterId);
                if (waiter != null && waiter.Availability != "Busy")
                {
                    waiter.Availability = "Busy";
                    _context.Waiters.Update(waiter);
                }
            }
            // If order was Upcoming, set to Ongoing
            if (order.Status == "Upcoming")
            {
                order.Status = "Ongoing";
                _context.Orders.Update(order);
            }
            _context.SaveChanges();

            if (headWaiterId.HasValue)
                return RedirectToAction(nameof(Index), new { headWaiterId = headWaiterId.Value });
            else
                return RedirectToAction(nameof(Index));
        }

        // GET: PaidOrders/PartialIndex
        public async Task<IActionResult> PartialIndex(string statusFilter, int? headWaiterId)
        {
            // If not provided, get from session for logged-in headwaiters
            if (!headWaiterId.HasValue && HttpContext.Session.GetString("Role") == "HeadWaiter")
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId != null)
                {
                    var headWaiter = _context.HeadWaiters.FirstOrDefault(h => h.UserId == userId.Value && h.isActive);
                    if (headWaiter != null)
                        headWaiterId = headWaiter.HeadWaiterId;
                }
            }

            var paidOrders = _context.Customers
                .Include(c => c.Order)
                    .ThenInclude(o => o.HeadWaiter)
                .Where(c => c.Order != null && c.Order.AmountPaid >= 0.5 * c.Order.TotalPayment);

            if (headWaiterId.HasValue)
            {
                paidOrders = paidOrders.Where(c => c.Order.HeadWaiterId == headWaiterId.Value);
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                paidOrders = paidOrders.Where(c => c.Order.Status == statusFilter);
            }

            return PartialView("Index", await paidOrders.ToListAsync());
        }

        // GET: PaidOrders/ReturnMaterials/5
        public async Task<IActionResult> ReturnMaterials(int id)
        {
            // id = CustomerId
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.CustomerID == id);
            if (order == null) return NotFound();

            var pullOut = await _context.MaterialPullOuts
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.OrderId == order.OrderId);

            var materials = await _context.Materials.ToListAsync();

            var pulledOutItems = pullOut?.Items.Select(i => new ReturnMaterialItem
            {
                MaterialName = i.MaterialName,
                PulledOut = i.Quantity,
                Returned = i.Quantity,
                Lost = 0,
                Damaged = 0,
                ChargePerItem = materials.FirstOrDefault(m => m.Name == i.MaterialName)?.GetType().GetProperty("ChargePerItem")?.GetValue(materials.FirstOrDefault(m => m.Name == i.MaterialName)) as decimal? ?? 0
            }).ToList() ?? new List<ReturnMaterialItem>();

            var viewModel = new ReturnMaterialsViewModel
            {
                OrderId = order.OrderId,
                CustomerId = id,
                Items = pulledOutItems
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnMaterials(ReturnMaterialsViewModel model)
        {
            decimal totalCharge = 0;
            foreach (var item in model.Items)
            {
                // Update inventory for returned items
                var material = await _context.Materials.FirstOrDefaultAsync(m => m.Name == item.MaterialName);
                if (material != null)
                {
                    material.Quantity += item.Returned;
                    _context.Materials.Update(material);
                }
                // Calculate charge for lost/damaged
                totalCharge += (item.Lost + item.Damaged) * item.ChargePerItem;
                // Optionally: log lost/damaged per order/material
            }
            // Charge customer for lost/damaged
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == model.OrderId);
            if (order != null)
            {
                order.TotalPayment += (double)totalCharge;
                _context.Orders.Update(order);
            }
            await _context.SaveChangesAsync();
            TempData["ReturnSuccess"] = "Materials returned and charges applied successfully!";
            return RedirectToAction("Index");
        }
    }
}
