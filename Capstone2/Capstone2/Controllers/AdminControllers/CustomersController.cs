using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;
using Newtonsoft.Json;
using Capstone2.Helpers;

namespace Capstone2.Controllers.AdminControllers
{
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CustomersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Customers
        public async Task<IActionResult> Index(string searchString)
        {
            var customers = _context.Customers
                                    .Include(c => c.Order)
                                        .ThenInclude(o => o.HeadWaiter)
                                            .ThenInclude(hw => hw.User)
                                    .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                customers = customers.Where(s => s.Name.ToLower().Contains(searchString.ToLower()));
            }

            return View(await customers.ToListAsync());
        }

        // GET: Customers/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Customers/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CustomerID,Name,ContactNo,Address")] Customer customer)
        {
            if (ModelState.IsValid)
            {
                _context.Add(customer);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(customer);
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.

        // GET: Customers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(m => m.CustomerID == id);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customers.Include(c => c.Order).FirstOrDefaultAsync(c => c.CustomerID == id);
            if (customer != null)
            {
                // Remove related records if order exists
                if (customer.Order != null)
                {
                    var orderId = customer.Order.OrderId;

                    // Remove order waiters
                    var orderWaiters = _context.OrderWaiters.Where(ow => ow.OrderId == orderId);
                    _context.OrderWaiters.RemoveRange(orderWaiters);
                    // Remove material pull outs
                    var pullOuts = _context.MaterialPullOuts.Where(p => p.OrderId == orderId);
                    _context.MaterialPullOuts.RemoveRange(pullOuts);
                    // Remove material returns
                    var returns = _context.MaterialReturns.Where(r => r.OrderId == orderId);
                    _context.MaterialReturns.RemoveRange(returns);
                    // Remove the order
                    _context.Orders.Remove(customer.Order);
                }
                // Remove the customer
                _context.Customers.Remove(customer);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.CustomerID == id);
        }

        public async Task<IActionResult> ViewOrder(int? id)
        {
            if (id == null)
                return BadRequest();

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Menu)
                .Include(o => o.HeadWaiter)
                    .ThenInclude(hw => hw.User)
                .FirstOrDefaultAsync(o => o.CustomerID == id.Value);

            if (order == null)
                return NotFound();

            // If order is completed, get the waiters who were assigned to this order
            if (order.Status == "Completed")
            {
                var orderWaiters = await _context.OrderWaiters
                    .Include(ow => ow.Waiter)
                        .ThenInclude(w => w.User)
                    .Where(ow => ow.OrderId == order.OrderId)
                    .ToListAsync();

                ViewBag.OrderWaiters = orderWaiters;
            }

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound();

            customer.IsPaid = !customer.IsPaid;
            await _context.SaveChangesAsync();

            // send back to the list (preserving any search/filter could be extra work)
            return RedirectToAction(nameof(Index));
        }

        // GET: Customers/PaymentDetails/5
        public async Task<IActionResult> PaymentDetails(int? id)
        {
            if (id == null)
                return NotFound();

            var customer = await _context.Customers
                .Include(c => c.Order)
                .FirstOrDefaultAsync(c => c.CustomerID == id);
            if (customer == null || customer.Order == null)
                return NotFound();

            var payments = await _context.Payments
                .Where(p => p.OrderId == customer.Order.OrderId)
                .OrderByDescending(p => p.Date)
                .ToListAsync();

            // Calculate additional charges for lost/damaged materials
            var materialReturns = await _context.Set<MaterialReturn>().Where(r => r.OrderId == customer.Order.OrderId).ToListAsync();
            var additionalCharges = materialReturns.Sum(r => (r.Lost + r.Damaged) * r.ChargePerItem);
            ViewBag.AdditionalCharges = additionalCharges;

            // Prepare list of charged items for modal
            var chargedItems = materialReturns
                .Where(r => r.Lost > 0 || r.Damaged > 0)
                .Select(r => new { r.MaterialName, r.Lost, r.Damaged, r.ChargePerItem })
                .ToList();
            ViewBag.ChargedItems = chargedItems;

            ViewBag.Payments = payments;
            return View(customer);
        }

        // POST: Customers/AddPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPayment(int customerId, double paymentAmount)
        {
            var customer = await _context.Customers.Include(c => c.Order).FirstOrDefaultAsync(c => c.CustomerID == customerId);
            if (customer == null || customer.Order == null)
                return NotFound();

            if (paymentAmount <= 0)
            {
                TempData["PaymentError"] = "Payment amount must be greater than zero.";
                return RedirectToAction("PaymentDetails", new { id = customerId });
            }

            // Add payment record
            var payment = new Payment
            {
                OrderId = customer.Order.OrderId,
                Amount = paymentAmount,
                Date = DateTime.Now
            };
            _context.Payments.Add(payment);

            // Update order's AmountPaid
            customer.Order.AmountPaid += paymentAmount;
            _context.Orders.Update(customer.Order);

            // Mark as paid if fully paid
            if (customer.Order.AmountPaid >= customer.Order.TotalPayment)
            {
                customer.IsPaid = true;
                _context.Customers.Update(customer);
            }

            await _context.SaveChangesAsync();
            TempData["PaymentSuccess"] = $"Payment Recorded.";
            return RedirectToAction("PaymentDetails", new { id = customerId });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCateringStatus(int id, string cateringStatus)
        {
            var customer = await _context.Customers
                .Include(c => c.Order)
                .FirstOrDefaultAsync(c => c.CustomerID == id);

            if (customer == null || customer.Order == null)
            {
                return NotFound();
            }

            // Validation: Require at least 50% down payment for Ongoing/Completed
            if ((cateringStatus == "Ongoing" || cateringStatus == "Completed") && !customer.Order.DownPaymentMet)
            {
                TempData["CateringStatusError"] = "At least 50% down payment is required to proceed with the order.";
                return RedirectToAction(nameof(Index));
            }

            customer.Order.Status = cateringStatus;

            if (cateringStatus == "Completed")
            {
                var orderWaiters = _context.OrderWaiters.Where(ow => ow.OrderId == customer.Order.OrderId).ToList();
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

            _context.Orders.Update(customer.Order);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: Customers/AssignHeadWaiter/5
        public async Task<IActionResult> AssignHeadWaiter(int? id)
        {
            if (id == null)
                return NotFound();

            var customer = await _context.Customers.Include(c => c.Order).FirstOrDefaultAsync(c => c.CustomerID == id);
            if (customer == null || customer.Order == null)
                return NotFound();

            // Get all active headwaiters
            var headWaiters = await _context.HeadWaiters.Include(h => h.User).Where(h => h.isActive).ToListAsync();
            ViewBag.HeadWaiters = headWaiters;
            ViewBag.SelectedHeadWaiterId = customer.Order.HeadWaiterId;
            return View(customer);
        }

        // POST: Customers/AssignHeadWaiter/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignHeadWaiter(int id, int headWaiterId)
        {
            var customer = await _context.Customers.Include(c => c.Order).FirstOrDefaultAsync(c => c.CustomerID == id);
            if (customer == null || customer.Order == null)
                return NotFound();

            customer.Order.HeadWaiterId = headWaiterId;
            _context.Orders.Update(customer.Order);
            await _context.SaveChangesAsync();
            TempData["HeadWaiterAssigned"] = "Head Waiter assigned successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Customers/InventoryReport/5
        public async Task<IActionResult> InventoryReport(int id)
        {
            // id = CustomerId
            Order order = await _context.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.CustomerID == id);
            if (order == null || order.Status != "Completed")
                return NotFound();

            // Get all material pull outs for this order
            var materialPullOut = await _context.MaterialPullOuts
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.OrderId == order.OrderId);

            // Get all material returns for this order
            var materialReturns = await _context.Set<MaterialReturn>().Where(r => r.OrderId == order.OrderId).ToListAsync();

            // Get all materials with their consumable status
            var materialsDict = _context.Materials.ToDictionary(m => m.MaterialId, m => m.IsConsumable);

            var reportItems = new List<InventoryReportItemViewModel>();

            if (materialPullOut?.Items != null)
            {
                foreach (var pullOutItem in materialPullOut.Items)
                {
                    // Find the material to get its ID and consumable status
                    var material = _context.Materials.FirstOrDefault(m => m.Name == pullOutItem.MaterialName);
                    var isConsumable = material?.IsConsumable ?? false;
                    var materialId = material?.MaterialId ?? 0;

                    // Find corresponding return data (if any)
                    var returnData = materialReturns.FirstOrDefault(r => r.MaterialName == pullOutItem.MaterialName);

                    var reportItem = new InventoryReportItemViewModel
                    {
                        MaterialId = materialId,
                        MaterialName = pullOutItem.MaterialName,
                        PulledOut = pullOutItem.Quantity,
                        Returned = isConsumable ? 0 : (returnData?.Returned ?? 0),
                        Lost = isConsumable ? 0 : (returnData?.Lost ?? 0),
                        Damaged = isConsumable ? 0 : (returnData?.Damaged ?? 0),
                        IsConsumable = isConsumable
                    };

                    reportItems.Add(reportItem);
                }
            }

            var viewModel = new InventoryReportViewModel
            {
                OrderId = order.OrderId,
                CustomerName = order.Customer.Name,
                CustomerId = id,
                Items = reportItems
            };
            return View(viewModel);
        }

        // GET: Customers/OrdersByDate
        public async Task<IActionResult> OrdersByDate(DateTime? selectedDate = null)
        {
            var date = selectedDate ?? DateTime.Today;

            var ordersForDate = await _context.Orders
                .Include(o => o.Customer)
                .Where(o => o.CateringDate.Date == date.Date)
                .OrderBy(o => o.timeOfFoodServing)
                .ToListAsync();

            int totalPax = ordersForDate.Sum(o => o.NoOfPax);
            bool hasLargeOrder = ordersForDate.Any(o => o.NoOfPax >= 701 && o.NoOfPax <= 1500);

            ViewBag.SelectedDate = date;
            ViewBag.TotalPax = totalPax;
            ViewBag.HasLargeOrder = hasLargeOrder;
            ViewBag.MaxPax = 700;

            return View(ordersForDate);
        }

        // GET: Customers/DateSummary
        public async Task<IActionResult> DateSummary(DateTime? startDate = null, DateTime? endDate = null)
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today.AddDays(30);

            var ordersInRange = await _context.Orders
                .Include(o => o.Customer)
                .Where(o => o.CateringDate.Date >= start.Date && o.CateringDate.Date <= end.Date)
                .OrderBy(o => o.CateringDate)
                .ToListAsync();

            var dateSummary = ordersInRange
                .GroupBy(o => o.CateringDate.Date)
                .Select(g => new DateSummaryViewModel
                {
                    Date = g.Key,
                    TotalPax = g.Sum(o => o.NoOfPax),
                    HasLargeOrder = g.Any(o => o.NoOfPax >= 701 && o.NoOfPax <= 1500),
                    OrderCount = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            var viewModel = new DateSummaryPageViewModel
            {
                StartDate = start,
                EndDate = end,
                DateSummary = dateSummary
            };

            return View(viewModel);
        }
    }
}
