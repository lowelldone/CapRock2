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
            var ordersWithBalance = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.HeadWaiter)
                    .ThenInclude(hw => hw.User)
                .Where(o => !o.isDeleted && !o.Customer.isDeleted && o.AmountPaid < o.TotalPayment);

            // If head waiter, show only their assigned orders
            if (role == "HEADWAITER")
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var headWaiter = await _context.HeadWaiters
                    .FirstOrDefaultAsync(h => h.UserId == userId.Value && h.isActive);

                if (headWaiter != null)
                {
                    ordersWithBalance = ordersWithBalance
                        .Where(o => o.HeadWaiterId == headWaiter.HeadWaiterId);
                }
                else
                {
                    return Forbid();
                }
            }

            var list = await ordersWithBalance.OrderBy(o => o.CateringDate).ToListAsync();

            if (!string.IsNullOrEmpty(searchString))
            {
                var searchTerm = searchString.ToLower().Trim();
                list = list.Where(o =>
                    o.OrderNumber.ToLower().Contains(searchTerm) ||
                    o.Customer.Name.ToLower().Contains(searchTerm)
                ).ToList();
            }

            return View(list);
        }

        // GET: Payments/ProcessPayment/5
        public async Task<IActionResult> ProcessPayment(int? id)
        {
            if (id == null)
                return NotFound();

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Menu)
                .FirstOrDefaultAsync(o => o.OrderId == id && !o.isDeleted && !o.Customer.isDeleted);

            if (order == null)
                return NotFound();

            if (order.AmountPaid >= order.TotalPayment)
            {
                TempData["PaymentError"] = "This order is already fully paid.";
                return RedirectToAction(nameof(Index));
            }

            // Get existing payments for this order
            var existingPayments = await _context.Payments
                .Where(p => p.OrderId == order.OrderId)
                .OrderByDescending(p => p.Date)
                .ToListAsync();

            ViewBag.ExistingPayments = existingPayments;
            ViewBag.RemainingBalance = order.TotalPayment - order.AmountPaid;

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

            var remainingBalance = order.TotalPayment - order.AmountPaid;
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
            _context.Orders.Update(order);

            // Check if down payment is now met and update status to Accepted
            if (order.DownPaymentMet && order.Status == "Pending")
            {
                order.Status = "Accepted";
                _context.Orders.Update(order);
            }

            // Mark customer as paid if fully paid
            if (order.AmountPaid >= order.TotalPayment)
            {
                order.Customer.IsPaid = true;
                _context.Customers.Update(order.Customer);
            }

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

            ViewBag.Payments = payments;
            ViewBag.RemainingBalance = order.TotalPayment - order.AmountPaid;

            return View(order);
        }
    }
}
