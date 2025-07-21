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
                                    .Include(c => c.Order) // Include the related Order
                                    .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                customers = customers.Where(s => s.Name.ToLower().Contains(searchString.ToLower()));
            }

            return View(await customers.ToListAsync());
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> Details(int? id)
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

        // GET: Customers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }
            return View(customer);
        }

        // POST: Customers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("CustomerID,Name,ContactNo,Address")] Customer customer)
        {
            if (id != customer.CustomerID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(customer);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomerExists(customer.CustomerID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(customer);
        }

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
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
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
                .FirstOrDefaultAsync(o => o.CustomerID == id.Value);

            if (order == null)
                return NotFound();

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

        // POST: Customers/RecordPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordPayment(int customerId, double paymentAmount)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.CustomerID == customerId);
            if (order == null)
            {
                return NotFound();
            }
            if (paymentAmount <= 0)
            {
                TempData["PaymentError"] = "Payment amount must be greater than zero.";
                return RedirectToAction(nameof(Index));
            }
            order.AmountPaid += paymentAmount;

            // Automatically mark as paid if fully paid
            if (order.AmountPaid >= order.TotalPayment)
            {
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerID == customerId);
                if (customer != null)
                {
                    customer.IsPaid = true;
                    _context.Customers.Update(customer);
                }
            }

            _context.Orders.Update(order);
            await _context.SaveChangesAsync();
            TempData["PaymentSuccess"] = $"Payment of {paymentAmount:C} recorded.";
            return RedirectToAction(nameof(Index));
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
                _context.OrderWaiters.RemoveRange(orderWaiters);
                var attendances = _context.Attendances.Where(a => a.OrderId == customer.Order.OrderId).ToList();
                _context.Attendances.RemoveRange(attendances);
            }

            _context.Orders.Update(customer.Order);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
