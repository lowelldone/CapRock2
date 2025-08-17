using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;

namespace Capstone2.Controllers.AdminControllers
{
    public class PaymentsController : GenericController
    {
        private readonly ApplicationDbContext _context;

        public PaymentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Allocate payments strictly to the base total first. Any remainder in the crossing payment
        // (the payment that reaches the base) is treated as change, not applied to additional charges.
        // Only payments that occur after the base has been fully covered are counted toward charges.
        private static (double baseAllocated, double chargesAllocated) AllocatePaymentsToBaseThenCharges(Order order, IEnumerable<Payment> payments)
        {
            double baseAllocated = 0d;
            double chargesAllocated = 0d;
            foreach (var payment in payments.OrderBy(p => p.Date))
            {
                if (baseAllocated < order.TotalPayment)
                {
                    var canAllocateToBase = Math.Min(payment.Amount, order.TotalPayment - baseAllocated);
                    baseAllocated += canAllocateToBase;
                    // Any remainder in this same payment is considered change (not applied to charges)
                    // and therefore intentionally ignored here.
                }
                else
                {
                    // Base fully covered earlier; subsequent payments go to charges
                    chargesAllocated += payment.Amount;
                }
            }
            return (baseAllocated, chargesAllocated);
        }

        // GET: Payments
        public async Task<IActionResult> Index(string searchString)
        {
            var role = HttpContext.Session.GetString("Role");
            // Base query (no balance filter yet; we'll compute effective balance including additional charges in-memory)
            var ordersQuery = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.HeadWaiter)
                    .ThenInclude(hw => hw.User)
                .Where(o => !o.isDeleted && !o.Customer.isDeleted);

            // If head waiter, show only their assigned orders
            if (role == "HEADWAITER")
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var headWaiter = await _context.HeadWaiters
                    .FirstOrDefaultAsync(h => h.UserId == userId.Value && h.isActive);

                if (headWaiter != null)
                {
                    ordersQuery = ordersQuery.Where(o => o.HeadWaiterId == headWaiter.HeadWaiterId);
                }
                else
                {
                    return Forbid();
                }
            }

            var orders = await ordersQuery.OrderBy(o => o.CateringDate).ToListAsync();

            // Compute additional charges per order (lost/damaged materials)
            var additionalChargesByOrder = await _context.Set<MaterialReturn>()
                .GroupBy(r => r.OrderId)
                .Select(g => new
                {
                    OrderId = g.Key,
                    TotalCharge = g.Sum(r => (r.Lost + r.Damaged) * r.ChargePerItem)
                })
                .ToListAsync();
            var additionalChargesDict = additionalChargesByOrder.ToDictionary(x => x.OrderId, x => x.TotalCharge);

            // Compute payments per order (chronological lists) to allocate accurately to base and charges
            var orderIds = orders.Select(o => o.OrderId).ToList();
            var paymentsAll = await _context.Payments
                .Where(p => orderIds.Contains(p.OrderId))
                .OrderBy(p => p.Date)
                .ToListAsync();
            var paymentsListByOrder = paymentsAll
                .GroupBy(p => p.OrderId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Filter orders with outstanding balance: exclude Pending and Completed; require unpaid effective total (base + charges)
            // For consistency with details page, compute per-order remaining balance using strict allocation
            var remainingBalanceByOrder = new Dictionary<int, double>();
            var allocatedPaidByOrder = new Dictionary<int, double>();
            foreach (var order in orders)
            {
                var extra = additionalChargesDict.TryGetValue(order.OrderId, out var totalDec) ? (double)totalDec : 0d;
                var paymentsForOrder = paymentsListByOrder.TryGetValue(order.OrderId, out var list) ? list : new List<Payment>();
                var allocation = AllocatePaymentsToBaseThenCharges(order, paymentsForOrder);
                var remainingBase = Math.Max(0d, order.TotalPayment - allocation.baseAllocated);
                var remainingCharges = Math.Max(0d, extra - allocation.chargesAllocated);
                remainingBalanceByOrder[order.OrderId] = remainingBase + remainingCharges;
                allocatedPaidByOrder[order.OrderId] = allocation.baseAllocated + allocation.chargesAllocated;
            }

            var ordersWithBalance = orders
                .Where(o => o.Status != "Completed" && o.Status != "Pending" && remainingBalanceByOrder.TryGetValue(o.OrderId, out var rb) && rb > 0)
                .ToList();

            if (!string.IsNullOrEmpty(searchString))
            {
                var searchTerm = searchString.ToLower().Trim();
                ordersWithBalance = ordersWithBalance.Where(o =>
                    o.OrderNumber.ToLower().Contains(searchTerm) ||
                    o.Customer.Name.ToLower().Contains(searchTerm)
                ).ToList();
            }

            // Expose additional charges to the view for balance display per order
            ViewBag.AdditionalChargesByOrder = additionalChargesDict;
            ViewBag.RemainingBalanceByOrder = remainingBalanceByOrder;
            ViewBag.AllocatedPaidByOrder = allocatedPaidByOrder;
            ViewBag.IsAdmin = role == "ADMIN";

            return View(ordersWithBalance);
        }

        // GET: Payments/ProcessPayment/5
        public async Task<IActionResult> ProcessPayment(int? id)
        {
            if (id == null)
                return NotFound();

            var role = HttpContext.Session.GetString("Role");
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Menu)
                .FirstOrDefaultAsync(o => o.OrderId == id && !o.isDeleted && !o.Customer.isDeleted);

            if (order == null)
                return NotFound();

            if (order.AmountPaid >= order.TotalPayment)
            {
                // Even if AmountPaid >= TotalPayment, there may still be additional charges.
                // We'll continue to show the page but validate remaining balance against the effective total below.
            }

            // Get existing payments for this order
            var existingPayments = await _context.Payments
                .Where(p => p.OrderId == order.OrderId)
                .OrderByDescending(p => p.Date)
                .ToListAsync();
            var totalPaid = existingPayments.Sum(p => p.Amount);
            // Allocate strictly: remainder of crossing payment is ignored for charges
            var allocation = AllocatePaymentsToBaseThenCharges(order, existingPayments);
            var appliedPaidToBase = allocation.baseAllocated;

            // Compute additional charges and charged items list
            var materialReturns = await _context.Set<MaterialReturn>()
                .Where(r => r.OrderId == order.OrderId)
                .ToListAsync();
            var additionalCharges = materialReturns.Sum(r => (r.Lost + r.Damaged) * r.ChargePerItem);
            ViewBag.AdditionalCharges = additionalCharges;

            var chargedItems = materialReturns
                .Where(r => r.Lost > 0 || r.Damaged > 0)
                .Select(r => new { r.MaterialName, r.Lost, r.Damaged, r.ChargePerItem })
                .ToList();
            ViewBag.ChargedItems = chargedItems;

            // Remaining balance: use the actual sum of payments instead of relying on Order.AmountPaid
            var appliedPaidToCharges = allocation.chargesAllocated;
            var remainingBase = Math.Max(0d, order.TotalPayment - appliedPaidToBase);
            var remainingCharges = Math.Max(0d, (double)additionalCharges - appliedPaidToCharges);
            var remainingBalance = remainingBase + remainingCharges;

            ViewBag.ExistingPayments = existingPayments;
            ViewBag.RemainingBalance = remainingBalance;
            ViewBag.TotalPaid = totalPaid;
            ViewBag.AppliedPaidToBase = appliedPaidToBase;
            ViewBag.IsHeadWaiter = role == "HEADWAITER";

            return View(order);
        }

        // POST: Payments/ProcessPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int orderId, double paymentAmount)
        {
            var role = HttpContext.Session.GetString("Role");
            var order = await _context.Orders
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && !o.isDeleted && !o.Customer.isDeleted);

            if (order == null)
                return NotFound();

            if (paymentAmount <= 0)
            {
                TempData["PaymentError"] = "Payment amount must be greater than zero.";
                return RedirectToAction("ProcessPayment", new { id = orderId });
            }

            // Compute additional charges to validate against remaining balance
            var additionalCharges = await _context.Set<MaterialReturn>()
                .Where(r => r.OrderId == order.OrderId)
                .Select(r => (r.Lost + r.Damaged) * r.ChargePerItem)
                .SumAsync();

            // Validate against the sum of payments so far to avoid stale Order.AmountPaid
            var paymentsSoFar = await _context.Payments
                .Where(p => p.OrderId == order.OrderId)
                .OrderBy(p => p.Date)
                .ToListAsync();
            var totalPaidSoFar = paymentsSoFar.Sum(p => p.Amount);
            var allocationSoFar = AllocatePaymentsToBaseThenCharges(order, paymentsSoFar);
            var appliedPaidToBase = allocationSoFar.baseAllocated;
            var appliedPaidToCharges = allocationSoFar.chargesAllocated;
            var remainingBase = Math.Max(0d, order.TotalPayment - appliedPaidToBase);
            var remainingCharges = Math.Max(0d, (double)additionalCharges - appliedPaidToCharges);
            var remainingBalance = remainingBase + remainingCharges;

            // Headwaiter must pay EXACT remaining balance; Admin can pay partially up to remaining
            if (role == "HEADWAITER")
            {
                if (Math.Round(paymentAmount, 2) != Math.Round(remainingBalance, 2))
                {
                    TempData["PaymentError"] = $"Headwaiters must pay the exact balance of ₱{remainingBalance:F2}.";
                    return RedirectToAction("ProcessPayment", new { id = orderId });
                }
            }
            else if (paymentAmount > remainingBalance)
            {
                TempData["PaymentError"] = $"Payment amount cannot exceed the remaining balance of ₱{remainingBalance:F2}.";
                return RedirectToAction("ProcessPayment", new { id = orderId });
            }

            // Add payment record
            var payment = new Payment
            {
                OrderId = order.OrderId,
                Amount = paymentAmount,
                Date = DateTime.Now
            };
            _context.Payments.Add(payment);

            // Update order's AmountPaid based on the canonical sum of payments
            order.AmountPaid = totalPaidSoFar + paymentAmount;

            // Check if down payment is now met and update status to Accepted
            if (order.DownPaymentMet && order.Status == "Pending")
            {
                order.Status = "Accepted";
            }

            // Calculate effective total (including additional charges)
            var effectiveTotal = order.TotalPayment + (double)additionalCharges;

            // Mark customer as paid if fully paid including additional charges
            if (order.AmountPaid >= effectiveTotal)
            {
                order.Customer.IsPaid = true;
                // Only mark Completed when materials have been returned (i.e., returns exist)
                var returnsExist = await _context.MaterialReturns.AnyAsync(r => r.OrderId == order.OrderId);
                if (returnsExist)
                {
                    order.Status = "Completed";
                    // Set waiters back to Available
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
                // If returns do not exist yet, keep current status (Accepted/Ongoing) until return is processed
            }
            else
            {
                order.Customer.IsPaid = false;
                // Not fully paid yet: set to Ongoing unless still Pending/Accepted
                if (order.Status != "Pending" && order.Status != "Accepted")
                {
                    order.Status = "Ongoing";
                }
            }

            _context.Orders.Update(order);

            await _context.SaveChangesAsync();

            TempData["PaymentSuccess"] = $"Payment of ₱{paymentAmount:F2} has been recorded successfully.";
            return RedirectToAction("ProcessPayment", new { id = orderId });
        }


        // GET: Payments/PaymentHistory/5
        public async Task<IActionResult> PaymentHistory(int? id)
        {
            if (id == null)
                return NotFound();

            var order = await _context.Orders
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.OrderId == id && !o.isDeleted && !o.Customer.isDeleted);

            if (order == null)
                return NotFound();

            var payments = await _context.Payments
                .Where(p => p.OrderId == order.OrderId)
                .OrderByDescending(p => p.Date)
                .ToListAsync();
            var totalPaid = payments.Sum(p => p.Amount);
            var allocation = AllocatePaymentsToBaseThenCharges(order, payments);
            var appliedPaidToBase = allocation.baseAllocated;
            var appliedPaidToCharges = allocation.chargesAllocated;

            // Include additional charges and compute updated remaining balance
            var additionalCharges = await _context.Set<MaterialReturn>()
                .Where(r => r.OrderId == order.OrderId)
                .Select(r => (r.Lost + r.Damaged) * r.ChargePerItem)
                .SumAsync();

            ViewBag.Payments = payments;
            ViewBag.AdditionalCharges = additionalCharges;
            var remainingBase = Math.Max(0d, order.TotalPayment - appliedPaidToBase);
            var remainingCharges = Math.Max(0d, (double)additionalCharges - appliedPaidToCharges);
            ViewBag.RemainingBalance = remainingBase + remainingCharges;
            ViewBag.TotalPaid = totalPaid;
            ViewBag.AppliedPaidToBase = appliedPaidToBase;

            return View(order);
        }
    }
}
