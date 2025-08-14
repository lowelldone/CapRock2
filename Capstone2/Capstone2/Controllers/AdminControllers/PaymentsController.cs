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

            // Filter orders that still have outstanding balance considering additional charges
            var ordersWithBalance = orders
                .Where(o =>
                {
                    var extra = additionalChargesDict.TryGetValue(o.OrderId, out var totalDec) ? (double)totalDec : 0d;
                    var effectiveTotal = o.TotalPayment + extra;
                    return o.AmountPaid < effectiveTotal;
                })
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

            // Remaining balance includes additional charges
            var remainingBalance = (order.TotalPayment + (double)additionalCharges) - order.AmountPaid;

            ViewBag.ExistingPayments = existingPayments;
            ViewBag.RemainingBalance = remainingBalance;
            ViewBag.IsHeadWaiter = role == "HEADWAITER";

            return View(order);
        }

        // POST: Payments/ProcessPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int orderId, double paymentAmount)
        {
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

            // Compute additional charges to validate against effective remaining balance
            var additionalCharges = await _context.Set<MaterialReturn>()
                .Where(r => r.OrderId == order.OrderId)
                .Select(r => (r.Lost + r.Damaged) * r.ChargePerItem)
                .SumAsync();

            var remainingBalance = (order.TotalPayment + (double)additionalCharges) - order.AmountPaid;

            if (paymentAmount > remainingBalance)
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

            // Update order's AmountPaid
            order.AmountPaid += paymentAmount;

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
                order.Status = "Completed"; // Fully paid = Completed

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
            else
            {
                order.Status = "Ongoing"; // Not fully paid yet
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

            // Include additional charges and compute updated remaining balance
            var additionalCharges = await _context.Set<MaterialReturn>()
                .Where(r => r.OrderId == order.OrderId)
                .Select(r => (r.Lost + r.Damaged) * r.ChargePerItem)
                .SumAsync();

            ViewBag.Payments = payments;
            ViewBag.AdditionalCharges = additionalCharges;
            ViewBag.RemainingBalance = (order.TotalPayment + (double)additionalCharges) - order.AmountPaid;

            return View(order);
        }
    }
}
