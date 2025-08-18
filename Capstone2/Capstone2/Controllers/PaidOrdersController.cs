using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;        // adjust namespace
using Capstone2.Models;
using Microsoft.AspNetCore.Mvc.Rendering;      // where your Customer model lives

namespace Capstone2.Controllers
{
    public class PaidOrdersController : GenericController
    {
        private readonly ApplicationDbContext _context;

        public PaidOrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: PaidOrders
        public async Task<IActionResult> Index(string statusFilter, int? headWaiterId, string searchString)
        {
            var role = HttpContext.Session.GetString("Role");

            if (role == "ADMIN")
            {
                // Admin sees all accepted orders and can assign headwaiters
                var acceptedOrders = _context.Customers
                    .Include(c => c.Order)
                        .ThenInclude(o => o.HeadWaiter)
                    .Where(c => !c.isDeleted &&
                               c.Order != null && !c.Order.isDeleted &&
                               c.Order.AmountPaid >= 0.5 * c.Order.TotalPayment &&
                               c.Order.Status == "Accepted");

                if (!string.IsNullOrEmpty(statusFilter))
                {
                    acceptedOrders = acceptedOrders.Where(c => c.Order.Status == statusFilter);
                }

                ViewBag.IsAdmin = true;
                ViewBag.HeadWaiters = await _context.HeadWaiters
                    .Include(h => h.User)
                    .Where(h => h.isActive)
                    .ToListAsync();

                ViewBag.MaterialPullOuts = await _context.MaterialPullOuts.ToListAsync();
                ViewBag.MaterialReturns = await _context.MaterialReturns.ToListAsync();

                return View("AdminIndex", await acceptedOrders.ToListAsync());
            }
            else if (role == "HEADWAITER")
            {
                // Headwaiter sees only their assigned orders
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId != null)
                {
                    var headWaiter = _context.HeadWaiters.FirstOrDefault(h => h.UserId == userId.Value && h.isActive);
                    if (headWaiter != null)
                        headWaiterId = headWaiter.HeadWaiterId;
                }

                var paidOrders = _context.Customers
                    .Include(c => c.Order)
                        .ThenInclude(o => o.HeadWaiter)
                    .Where(c => !c.isDeleted &&
                               c.Order != null && !c.Order.isDeleted &&
                               c.Order.AmountPaid >= 0.5 * c.Order.TotalPayment &&
                               c.Order.HeadWaiterId == headWaiterId);

                if (!string.IsNullOrEmpty(statusFilter))
                {
                    paidOrders = paidOrders.Where(c => c.Order.Status == statusFilter);
                }

                if (!string.IsNullOrWhiteSpace(searchString))
                {
                    var term = searchString.Trim().ToLower();
                    paidOrders = paidOrders.Where(c =>
                        c.Name.ToLower().Contains(term) ||
                        (c.ContactNo != null && c.ContactNo.ToLower().Contains(term)) ||
                        (c.Address != null && c.Address.ToLower().Contains(term)));
                }

                ViewBag.IsAdmin = false;
                ViewBag.SearchString = searchString;
                ViewBag.MaterialPullOuts = await _context.MaterialPullOuts.ToListAsync();
                ViewBag.MaterialReturns = await _context.MaterialReturns.ToListAsync();

                // Build dynamic remaining balance and returns flags for status display
                var customersList = await paidOrders.ToListAsync();
                var orderIds = customersList
                    .Where(c => c.Order != null)
                    .Select(c => c.Order.OrderId)
                    .ToList();

                if (orderIds.Any())
                {
                    // Additional charges per order (lost + damaged)
                    var additionalChargesByOrder = await _context.Set<MaterialReturn>()
                        .Where(r => orderIds.Contains(r.OrderId))
                        .GroupBy(r => r.OrderId)
                        .Select(g => new { OrderId = g.Key, TotalCharge = g.Sum(r => (decimal)((r.Lost + r.Damaged) * r.ChargePerItem)) })
                        .ToListAsync();
                    var additionalChargesDict = additionalChargesByOrder.ToDictionary(x => x.OrderId, x => (double)x.TotalCharge);

                    // Payments grouped by order (chronological) for allocation
                    var payments = await _context.Payments
                        .Where(p => orderIds.Contains(p.OrderId))
                        .OrderBy(p => p.Date)
                        .ToListAsync();
                    var paymentsByOrder = payments.GroupBy(p => p.OrderId).ToDictionary(g => g.Key, g => g.ToList());

                    // Returns existence per order
                    var returnsOrderIds = await _context.Set<MaterialReturn>()
                        .Where(r => orderIds.Contains(r.OrderId))
                        .Select(r => r.OrderId)
                        .Distinct()
                        .ToListAsync();
                    var returnsExistByOrder = new System.Collections.Generic.Dictionary<int, bool>();
                    foreach (var id in returnsOrderIds)
                    {
                        returnsExistByOrder[id] = true;
                    }

                    // Compute remaining balance per order using strict base-then-charges allocation
                    var remainingBalanceByOrder = new System.Collections.Generic.Dictionary<int, double>();
                    foreach (var customer in customersList)
                    {
                        var order = customer.Order;
                        if (order == null) continue;

                        var orderId = order.OrderId;
                        var totalPaymentBase = order.TotalPayment;
                        var additionalCharges = additionalChargesDict.ContainsKey(orderId) ? additionalChargesDict[orderId] : 0d;

                        double baseAllocated = 0d;
                        double chargesAllocated = 0d;
                        if (paymentsByOrder.TryGetValue(orderId, out var paymentList))
                        {
                            foreach (var payment in paymentList)
                            {
                                if (baseAllocated < totalPaymentBase)
                                {
                                    var amountNeededForBase = totalPaymentBase - baseAllocated;
                                    var toBase = Math.Min(payment.Amount, amountNeededForBase);
                                    baseAllocated += toBase;

                                    var remainder = payment.Amount - toBase;
                                    if (remainder > 0 && baseAllocated >= totalPaymentBase)
                                    {
                                        chargesAllocated += remainder;
                                    }
                                }
                                else
                                {
                                    chargesAllocated += payment.Amount;
                                }
                            }
                        }

                        var remainingBase = Math.Max(0d, totalPaymentBase - baseAllocated);
                        var remainingCharges = Math.Max(0d, additionalCharges - chargesAllocated);
                        var remaining = remainingBase + remainingCharges;

                        // If order is already marked Completed, treat remaining as zero for display
                        if (order.Status == "Completed")
                        {
                            remaining = 0d;
                        }

                        remainingBalanceByOrder[orderId] = remaining;
                    }

                    ViewBag.RemainingBalanceByOrder = remainingBalanceByOrder;
                    ViewBag.ReturnsExistByOrder = returnsExistByOrder;
                }

                return View(customersList);
            }
            else
            {
                // Other roles see nothing
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: PaidOrders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return BadRequest();

            var role = HttpContext.Session.GetString("Role");
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Menu)
                .Include(o => o.HeadWaiter)
                    .ThenInclude(h => h.User)
                .FirstOrDefaultAsync(o => o.CustomerID == id.Value && !o.isDeleted && !o.Customer.isDeleted);

            if (order == null)
                return NotFound();

            // Check if user has access to this order
            if (role == "HEADWAITER")
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var headWaiter = _context.HeadWaiters.FirstOrDefault(h => h.UserId == userId.Value && h.isActive);
                if (headWaiter == null || order.HeadWaiterId != headWaiter.HeadWaiterId)
                {
                    return Forbid();
                }
            }

            // Calculate additional charges for lost/damaged materials
            var materialReturns = await _context.Set<MaterialReturn>().Where(r => r.OrderId == order.OrderId).ToListAsync();
            var additionalCharges = materialReturns.Sum(r => (r.Lost + r.Damaged) * r.ChargePerItem);
            ViewBag.AdditionalCharges = additionalCharges;

            // Compute remaining balance consistent with Payments page allocation rules
            var payments = await _context.Payments
                .Where(p => p.OrderId == order.OrderId)
                .OrderBy(p => p.Date)
                .ToListAsync();

            double baseAllocated = 0d;
            double chargesAllocated = 0d;
            foreach (var payment in payments)
            {
                if (baseAllocated < order.TotalPayment)
                {
                    var amountNeededForBase = order.TotalPayment - baseAllocated;
                    var toBase = Math.Min(payment.Amount, amountNeededForBase);
                    baseAllocated += toBase;

                    var remainder = payment.Amount - toBase;
                    if (remainder > 0 && baseAllocated >= order.TotalPayment)
                    {
                        // Base completed by this payment; apply remainder to charges
                        chargesAllocated += remainder;
                    }
                }
                else
                {
                    chargesAllocated += payment.Amount;
                }
            }

            var remainingBase = Math.Max(0d, order.TotalPayment - baseAllocated);
            var remainingCharges = Math.Max(0d, (double)additionalCharges - chargesAllocated);
            var remainingBalance = (decimal)(remainingBase + remainingCharges);
            // If the order is completed, treat the balance as fully paid for display
            if (order.Status == "Completed")
            {
                remainingBalance = 0m;
            }
            ViewBag.RemainingBalance = remainingBalance;

            // Prepare list of charged items for modal
            var chargedItems = materialReturns
                .Where(r => r.Lost > 0 || r.Damaged > 0)
                .Select(r => new { r.MaterialName, r.Lost, r.Damaged, r.ChargePerItem })
                .ToList();
            ViewBag.ChargedItems = chargedItems;

            ViewBag.IsAdmin = role == "ADMIN";
            return View(order);
        }

        // GET: PaidOrders/AssignHeadWaiter/5
        public async Task<IActionResult> AssignHeadWaiter(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "ADMIN")
                return Forbid();

            var customer = await _context.Customers
                .Include(c => c.Order)
                .FirstOrDefaultAsync(c => c.CustomerID == id && !c.isDeleted && c.Order != null && !c.Order.isDeleted);

            if (customer == null || customer.Order == null)
                return NotFound();

            var headWaiters = await _context.HeadWaiters
                .Include(h => h.User)
                .Where(h => h.isActive)
                .ToListAsync();

            ViewBag.HeadWaiters = headWaiters;
            ViewBag.CurrentHeadWaiterId = customer.Order.HeadWaiterId;
            return View(customer);
        }

        // POST: PaidOrders/AssignHeadWaiter/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignHeadWaiter(int id, int headWaiterId)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "ADMIN")
                return Forbid();

            var customer = await _context.Customers
                .Include(c => c.Order)
                .FirstOrDefaultAsync(c => c.CustomerID == id);

            if (customer == null || customer.Order == null)
                return NotFound();

            var headWaiter = await _context.HeadWaiters
                .Include(h => h.User)
                .FirstOrDefaultAsync(h => h.HeadWaiterId == headWaiterId && h.isActive);

            if (headWaiter == null)
                return NotFound();

            customer.Order.HeadWaiterId = headWaiterId;
            _context.Orders.Update(customer.Order);
            await _context.SaveChangesAsync();

            // Repopulate data for the view and show success toast without navigating away
            var headWaiters = await _context.HeadWaiters
                .Include(h => h.User)
                .Where(h => h.isActive)
                .ToListAsync();
            ViewBag.HeadWaiters = headWaiters;
            ViewBag.CurrentHeadWaiterId = customer.Order.HeadWaiterId;
            ViewBag.SuccessMessage = "Assigning head waiter success";

            return View(customer);
        }

        // GET: PaidOrders/AssignWaiter/5 (ADMIN)
        public IActionResult AssignWaiter(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "ADMIN")
                return Forbid();

            var customer = _context.Customers
                .Include(c => c.Order)
                .FirstOrDefault(c => c.CustomerID == id && !c.isDeleted && c.Order != null && !c.Order.isDeleted);
            if (customer == null || customer.Order == null)
                return NotFound();

            var order = customer.Order;

            // Get IDs of waiters already assigned to this order
            var assignedWaiterIds = _context.OrderWaiters.Where(ow => ow.OrderId == order.OrderId).Select(ow => ow.WaiterId).ToList();
            // Only show available waiters in the selection
            var waiters = _context.Waiters
                .Include(w => w.User)
                .Where(w => !w.isDeleted && w.Availability == "Available")
                .ToList();

            // Populate the list of currently assigned waiters for display
            var assignedWaiters = _context.Waiters
                .Include(w => w.User)
                .Where(w => assignedWaiterIds.Contains(w.WaiterId))
                .ToList();

            ViewBag.Waiters = waiters;
            ViewBag.AssignedWaiterIds = assignedWaiterIds;
            ViewBag.AssignedWaiters = assignedWaiters;
            ViewBag.IsAdmin = true;
            return View("DeployWaiter", order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveAssignedWaiter(int id, int waiterId, int? headWaiterId)
        {
            var role = HttpContext.Session.GetString("Role");

            var order = _context.Orders.FirstOrDefault(o => o.CustomerID == id);
            if (order == null)
                return NotFound();

            // Check if user has access to this order
            if (role == "HEADWAITER")
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var headWaiter = _context.HeadWaiters.FirstOrDefault(h => h.UserId == userId.Value && h.isActive);
                if (headWaiter == null || order.HeadWaiterId != headWaiter.HeadWaiterId)
                {
                    return Forbid();
                }
            }

            // Remove the waiter from this order
            var links = _context.OrderWaiters
                .Where(ow => ow.OrderId == order.OrderId && ow.WaiterId == waiterId)
                .ToList();
            if (links.Any())
            {
                _context.OrderWaiters.RemoveRange(links);
            }

            // Set the waiter back to Available
            var waiter = _context.Waiters.FirstOrDefault(w => w.WaiterId == waiterId);
            if (waiter != null)
            {
                waiter.Availability = "Available";
                _context.Waiters.Update(waiter);
            }

            _context.SaveChanges();

            // Redirect back to the appropriate page so the updated lists are shown
            if (role == "ADMIN")
                return RedirectToAction(nameof(AssignWaiter), new { id });
            else if (headWaiterId.HasValue)
                return RedirectToAction(nameof(DeployWaiter), new { id, headWaiterId = headWaiterId.Value });
            else
                return RedirectToAction(nameof(DeployWaiter), new { id });
        }

        // GET: PaidOrders/DeployWaiter/5
        public IActionResult DeployWaiter(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            var customer = _context.Customers
                .Include(c => c.Order)
                .FirstOrDefault(c => c.CustomerID == id && !c.isDeleted && c.Order != null && !c.Order.isDeleted);
            if (customer == null || customer.Order == null)
                return NotFound();

            var order = customer.Order;

            // Check if user has access to this order
            if (role == "HEADWAITER")
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var headWaiter = _context.HeadWaiters.FirstOrDefault(h => h.UserId == userId.Value && h.isActive);
                if (headWaiter == null || order.HeadWaiterId != headWaiter.HeadWaiterId)
                {
                    return Forbid();
                }
            }

            // Get IDs of waiters already assigned to this order
            var assignedIds = _context.OrderWaiters.Where(ow => ow.OrderId == order.OrderId).Select(ow => ow.WaiterId).ToList();
            // Only show available waiters in the selection; fetch assigned separately for display
            var availableWaiters = _context.Waiters
                .Include(w => w.User)
                .Where(w => !w.isDeleted && w.Availability == "Available")
                .ToList();
            var assignedWaiters = _context.Waiters
                .Include(w => w.User)
                .Where(w => assignedIds.Contains(w.WaiterId))
                .ToList();

            ViewBag.Waiters = availableWaiters;
            ViewBag.AssignedWaiters = assignedWaiters;
            ViewBag.AssignedWaiterIds = assignedIds;
            ViewBag.IsAdmin = role == "ADMIN";
            return View(order);
        }

        // POST: PaidOrders/DeployWaiter/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeployWaiter(int id, int[] waiterIds, int? headWaiterId)
        {
            var role = HttpContext.Session.GetString("Role");
            var order = _context.Orders.FirstOrDefault(o => o.CustomerID == id);
            if (order == null)
                return NotFound();

            // Check if user has access to this order
            if (role == "HEADWAITER")
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var headWaiter = _context.HeadWaiters.FirstOrDefault(h => h.UserId == userId.Value && h.isActive);
                if (headWaiter == null || order.HeadWaiterId != headWaiter.HeadWaiterId)
                {
                    return Forbid();
                }
            }

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
                assignedWaiterIds = _context.OrderWaiters.Where(ow => ow.OrderId == order.OrderId).Select(ow => ow.WaiterId).ToList();
                var waiters = _context.Waiters.Include(w => w.User)
                    .Where(w => !w.isDeleted && w.Availability == "Available")
                    .ToList();
                var assignedWaiters = _context.Waiters
                    .Include(w => w.User)
                    .Where(w => assignedWaiterIds.Contains(w.WaiterId))
                    .ToList();
                ViewBag.Waiters = waiters;
                ViewBag.AssignedWaiterIds = assignedWaiterIds;
                ViewBag.IsAdmin = role == "ADMIN";
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
            _context.SaveChanges();

            if (role == "ADMIN")
                return RedirectToAction(nameof(Index));
            else if (headWaiterId.HasValue)
                return RedirectToAction(nameof(Index), new { headWaiterId = headWaiterId.Value });
            else
                return RedirectToAction(nameof(Index));
        }

        // GET: PaidOrders/PullOutMaterials/5
        public async Task<IActionResult> PullOutMaterials(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.CustomerID == id && !o.isDeleted);
            if (order == null) return NotFound();

            // Check if user has access to this order
            if (role == "HEADWAITER")
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var headWaiter = _context.HeadWaiters.FirstOrDefault(h => h.UserId == userId.Value && h.isActive);
                if (headWaiter == null || order.HeadWaiterId != headWaiter.HeadWaiterId)
                {
                    return Forbid();
                }
            }

            int pax = order.NoOfPax;
            // Count ordered lechon items for this order
            var lechonCount = await _context.OrderDetails
                .Include(od => od.Menu)
                .Where(od => od.OrderId == order.OrderId && od.Menu != null && od.Menu.Name.ToLower().Contains("lechon"))
                .SumAsync(od => (int?)od.Quantity) ?? 0;

            // Get existing pull-out for this order
            var existingPullOut = await _context.MaterialPullOuts
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.OrderId == order.OrderId);

            var allMaterials = await _context.Materials.ToListAsync();
            var materialsVm = allMaterials.Select(m => {
                var existingItem = existingPullOut?.Items?.FirstOrDefault(i => i.MaterialName == m.Name);
                var suggestedQuantity = Capstone2.Helpers.MaterialCalculator.GetSuggestedQuantity(m.Name, pax, lechonCount);

                return new PullOutMaterialItemViewModel
                {
                    MaterialId = m.MaterialId,
                    Name = m.Name,
                    CurrentQuantity = m.Quantity,
                    PullOutQuantity = existingItem != null ? existingItem.Quantity : 0,
                    IsFirstPullOut = existingPullOut == null
                };
            }).ToList();

            var viewModel = new PullOutMaterialsViewModel
            {
                CustomerId = id,
                Pax = pax,
                LechonCount = lechonCount,
                Materials = materialsVm
            };
            ViewBag.IsAdmin = role == "ADMIN";
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PullOutMaterials(PullOutMaterialsViewModel model)
        {
            var role = HttpContext.Session.GetString("Role");
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.CustomerID == model.CustomerId && !o.isDeleted);
            if (order == null) return NotFound();

            // Check if user has access to this order
            if (role == "HEADWAITER")
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var headWaiter = _context.HeadWaiters.FirstOrDefault(h => h.UserId == userId.Value && h.isActive);
                if (headWaiter == null || order.HeadWaiterId != headWaiter.HeadWaiterId)
                {
                    return Forbid();
                }
            }

            // Check if there's an existing pull-out for this order
            var existingPullOut = await _context.MaterialPullOuts
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.OrderId == order.OrderId);

            if (existingPullOut != null)
            {
                // Update existing pull-out items
                foreach (var item in model.Materials.Where(m => m.PullOutQuantity > 0))
                {
                    var existingItem = existingPullOut.Items.FirstOrDefault(i => i.MaterialName == item.Name);
                    if (existingItem != null)
                    {
                        // Add new quantity to existing quantity
                        existingItem.Quantity += item.PullOutQuantity;
                    }
                    else
                    {
                        // Add new item to existing pull-out
                        existingPullOut.Items.Add(new MaterialPullOutItem
                        {
                            MaterialName = item.Name,
                            Quantity = item.PullOutQuantity
                        });
                    }
                }
                existingPullOut.Date = DateTime.Now; // Update the date
                _context.MaterialPullOuts.Update(existingPullOut);
            }
            else
            {
                // Create new pull-out
                var pullOut = new MaterialPullOut
                {
                    OrderId = order.OrderId,
                    Date = DateTime.Now,
                    Items = model.Materials.Where(m => m.PullOutQuantity > 0).Select(m => new MaterialPullOutItem
                    {
                        MaterialName = m.Name,
                        Quantity = m.PullOutQuantity
                    }).ToList()
                };
                _context.MaterialPullOuts.Add(pullOut);
            }

            // Update material inventory
            foreach (var item in model.Materials)
            {
                if (item.PullOutQuantity > 0)
                {
                    var material = await _context.Materials.FirstOrDefaultAsync(m => m.MaterialId == item.MaterialId);
                    if (material != null)
                    {
                        material.Quantity -= item.PullOutQuantity;
                        if (material.Quantity < 0) material.Quantity = 0;
                        _context.Materials.Update(material);
                    }
                }
            }
            // If order was Accepted, set to Ongoing when materials are pulled out
            if (order.Status == "Accepted")
            {
                order.Status = "Ongoing";
                _context.Orders.Update(order);
            }

            await _context.SaveChangesAsync();
            TempData["PullOutSuccess"] = "Materials pulled out successfully!";

            // Show pull-out summary page
            return RedirectToAction(nameof(PullOutSummary), new { id = model.CustomerId });
        }

        // GET: PaidOrders/PullOutSummary/5
        public async Task<IActionResult> PullOutSummary(int id)
        {
            // id = CustomerId
            var order = await _context.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.CustomerID == id && !o.isDeleted && !o.Customer.isDeleted);
            if (order == null) return NotFound();
            var pullOut = await _context.MaterialPullOuts.Include(p => p.Items).FirstOrDefaultAsync(p => p.OrderId == order.OrderId);
            if (pullOut == null) return RedirectToAction(nameof(Index));
            ViewBag.CustomerName = order.Customer?.Name ?? "";
            return View(pullOut);
        }

        // GET: PaidOrders/ReturnMaterials/5
        public async Task<IActionResult> ReturnMaterials(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            // id = CustomerId
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.CustomerID == id && !o.isDeleted);
            if (order == null) return NotFound();

            // Check if user has access to this order
            if (role == "HEADWAITER")
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var headWaiter = _context.HeadWaiters.FirstOrDefault(h => h.UserId == userId.Value && h.isActive);
                if (headWaiter == null || order.HeadWaiterId != headWaiter.HeadWaiterId)
                {
                    return Forbid();
                }
            }

            var pullOut = await _context.MaterialPullOuts
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.OrderId == order.OrderId);

            var materials = await _context.Materials.Where(m => !m.IsConsumable).ToListAsync();

            var pulledOutItems = pullOut?.Items
                .Where(i => materials.Any(m => string.Equals(m.Name, i.MaterialName, StringComparison.OrdinalIgnoreCase)))
                .Select(i => {
                    var mat = materials.First(m => string.Equals(m.Name, i.MaterialName, StringComparison.OrdinalIgnoreCase));
                    return new ReturnMaterialItem
                    {
                        MaterialId = mat.MaterialId,
                        MaterialName = mat.Name,
                        PulledOut = i.Quantity,
                        Returned = i.Quantity,
                        Lost = 0,
                        Damaged = 0,
                        ChargePerItem = mat.Price
                    };
                }).ToList() ?? new List<ReturnMaterialItem>();

            var viewModel = new ReturnMaterialsViewModel
            {
                OrderId = order.OrderId,
                CustomerId = id,
                Items = pulledOutItems
            };
            ViewBag.IsAdmin = role == "ADMIN";
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnMaterials(ReturnMaterialsViewModel model)
        {
            var role = HttpContext.Session.GetString("Role");
            decimal totalCharge = 0;
            foreach (var item in model.Items)
            {
                // Use MaterialId for update
                var material = await _context.Materials.FirstOrDefaultAsync(m => m.MaterialId == item.MaterialId);
                if (material != null)
                {
                    material.Quantity += item.Returned;
                    _context.Materials.Update(material);

                    // Use current price from DB; fallback to stored default charge if price is not set
                    var chargePerItem = material.Price > 0 ? material.Price : material.ChargePerItem;
                    totalCharge += (item.Lost + item.Damaged) * chargePerItem;
                    // Store the price used at the time of return
                    var materialReturn = new MaterialReturn
                    {
                        OrderId = model.OrderId,
                        MaterialId = item.MaterialId,
                        MaterialName = material != null ? material.Name : item.MaterialName,
                        Returned = item.Returned,
                        Lost = item.Lost,
                        Damaged = item.Damaged,
                        ChargePerItem = chargePerItem
                    };
                    _context.Add(materialReturn);
                }
            }
            // Update order status based on payments vs effective total (base + all additional charges)
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == model.OrderId && !o.isDeleted);
            if (order != null)
            {
                // Sum of existing additional charges (before this batch)
                var previousCharges = await _context.MaterialReturns
                    .Where(r => r.OrderId == order.OrderId)
                    .SumAsync(r => (decimal)((r.Lost + r.Damaged) * r.ChargePerItem));
                var effectiveTotal = (decimal)order.TotalPayment + previousCharges + totalCharge;

                // Only complete if fully paid; otherwise set to Settling Balance
                if ((decimal)order.AmountPaid >= effectiveTotal)
                {
                    order.Status = "Completed";
                    var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerID == order.CustomerID);
                    if (customer != null)
                    {
                        customer.IsPaid = true;
                        _context.Customers.Update(customer);
                    }
                }
                else
                {
                    order.Status = "Settling Balance";
                    var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerID == order.CustomerID);
                    if (customer != null)
                    {
                        customer.IsPaid = false; // if generating new charges, not fully paid anymore
                        _context.Customers.Update(customer);
                    }
                }
                _context.Orders.Update(order);

                // Set waiters back to Available only if completed
                if (order.Status == "Completed")
                {
                    var orderWaiters = _context.OrderWaiters.Where(ow => ow.OrderId == order.OrderId).ToList();
                    foreach (var ow in orderWaiters)
                    {
                        var waiter = _context.Waiters.FirstOrDefault(w => w.WaiterId == ow.WaiterId);
                        if (waiter != null)
                        {
                            waiter.Availability = "Available";
                            _context.Waiters.Update(waiter);
                        }
                    }
                }
            }
            await _context.SaveChangesAsync();
            TempData["ReturnSuccess"] = $"Materials returned successfully! Additional charge for lost/damaged: ₱{totalCharge}.";

            // Show return summary page
            return RedirectToAction(nameof(ReturnSummary), new { id = model.CustomerId });
        }

        // GET: PaidOrders/ReturnSummary/5
        public async Task<IActionResult> ReturnSummary(int id)
        {
            // id = CustomerId
            var order = await _context.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.CustomerID == id && !o.isDeleted && !o.Customer.isDeleted);
            if (order == null) return NotFound();
            var returns = await _context.MaterialReturns.Where(r => r.OrderId == order.OrderId).ToListAsync();
            ViewBag.CustomerName = order.Customer?.Name ?? "";
            ViewBag.TotalCharge = returns.Sum(r => (r.Lost + r.Damaged) * r.ChargePerItem);
            return View(returns);
        }

        // POST: PaidOrders/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string username, string currentPassword, string newPassword)
        {
            try
            {
                // Get current user from session
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    TempData["ProfileError"] = "User session not found. Please log in again.";
                    return RedirectToAction("Index");
                }

                // Find the current user
                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId.Value);
                if (currentUser == null)
                {
                    TempData["ProfileError"] = "User not found.";
                    return RedirectToAction("Index");
                }

                // Verify current password
                if (currentUser.Password != currentPassword)
                {
                    TempData["ProfileError"] = "Current password is incorrect.";
                    return RedirectToAction("Index");
                }

                // Check if new username already exists (if username is being changed)
                if (username != currentUser.Username)
                {
                    var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username && u.UserId != userId.Value);
                    if (existingUser != null)
                    {
                        TempData["ProfileError"] = "Username already exists. Please choose a different username.";
                        return RedirectToAction("Index");
                    }
                }

                // Update user information
                currentUser.Username = username;
                currentUser.Password = newPassword;
                _context.Users.Update(currentUser);
                await _context.SaveChangesAsync();

                TempData["ProfileSuccess"] = "Profile updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ProfileError"] = $"Error updating profile: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
    }
}
