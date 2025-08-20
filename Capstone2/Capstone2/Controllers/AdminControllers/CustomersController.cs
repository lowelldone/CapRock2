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
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Capstone2.Controllers.AdminControllers
{
    public class CustomersController : GenericController
    {
        private readonly ApplicationDbContext _context;

        public CustomersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Allocate payments to the base total first. If a payment crosses the base boundary,
        // allocate the remainder of that same payment to additional charges. After the base has
        // been fully covered, subsequent payments are applied entirely to charges.
        private static (double baseAllocated, double chargesAllocated) AllocatePaymentsToBaseThenCharges(Order order, IEnumerable<Payment> payments)
        {
            double baseAllocated = 0d;
            double chargesAllocated = 0d;
            foreach (var payment in payments.OrderBy(p => p.Date))
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
                    // Base fully covered earlier; subsequent payments go to charges
                    chargesAllocated += payment.Amount;
                }
            }
            return (baseAllocated, chargesAllocated);
        }

        // GET: Customers
        public async Task<IActionResult> Index(string searchString, string cateringStatus)
        {
            var customers = _context.Customers
                                    .Include(c => c.Order)
                                        .ThenInclude(o => o.HeadWaiter)
                                            .ThenInclude(hw => hw.User)
                                    .Where(c => !c.isDeleted && (c.Order == null || !c.Order.isDeleted)) // Filter out soft-deleted customers and orders
                                    .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                var searchTerm = searchString.ToLower().Trim();
                customers = customers.Where(s =>
                    s.Name.ToLower().Contains(searchTerm) ||
                    (s.Order != null && !string.IsNullOrEmpty(s.Order.OrderNumber) &&
                     s.Order.OrderNumber.ToLower().Contains(searchTerm))
                );
            }

            // Filter by catering status if provided
            if (!string.IsNullOrEmpty(cateringStatus))
            {
                customers = customers.Where(s => s.Order != null && s.Order.Status == cateringStatus);
            }

            var customerList = await customers.ToListAsync();

            // Compute remaining balance (base + additional charges minus payments) per order
            var orderIds = customerList.Where(c => c.Order != null).Select(c => c.Order.OrderId).ToList();
            var additionalChargesByOrder = await _context.Set<MaterialReturn>()
                .Where(r => orderIds.Contains(r.OrderId))
                .GroupBy(r => r.OrderId)
                .Select(g => new { OrderId = g.Key, TotalCharge = g.Sum(r => (r.Lost + r.Damaged) * r.ChargePerItem) })
                .ToListAsync();
            var additionalChargesDict = additionalChargesByOrder.ToDictionary(x => x.OrderId, x => x.TotalCharge);

            var paymentsAll = await _context.Payments
                .Where(p => orderIds.Contains(p.OrderId))
                .OrderBy(p => p.Date)
                .ToListAsync();
            var paymentsListByOrder = paymentsAll.GroupBy(p => p.OrderId).ToDictionary(g => g.Key, g => g.ToList());

            var remainingBalanceByOrder = new Dictionary<int, double>();
            foreach (var c in customerList)
            {
                if (c.Order == null) continue;
                var order = c.Order;
                var extra = additionalChargesDict.TryGetValue(order.OrderId, out var total) ? (double)total : 0d;
                var paymentsForOrder = paymentsListByOrder.TryGetValue(order.OrderId, out var plist) ? plist : new List<Payment>();
                var allocation = AllocatePaymentsToBaseThenCharges(order, paymentsForOrder);
                var remainingBase = Math.Max(0d, order.TotalPayment - allocation.baseAllocated);
                var remainingCharges = Math.Max(0d, extra - allocation.chargesAllocated);
                remainingBalanceByOrder[order.OrderId] = remainingBase + remainingCharges;
            }

            ViewBag.RemainingBalanceByOrder = remainingBalanceByOrder;

            // Track which orders already have material returns recorded (used to label Settling Balance)
            var orderIdsWithReturns = await _context.MaterialReturns
                .Where(r => orderIds.Contains(r.OrderId))
                .Select(r => r.OrderId)
                .Distinct()
                .ToListAsync();
            var returnsExistDict = orderIdsWithReturns.ToDictionary(id => id, id => true);
            ViewBag.ReturnsExistByOrder = returnsExistDict;

            return View(customerList);
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
                .FirstOrDefaultAsync(m => m.CustomerID == id && !m.isDeleted);
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
                // Soft delete the customer
                customer.isDeleted = true;
                _context.Customers.Update(customer);

                // Soft delete the order if it exists
                if (customer.Order != null)
                {
                    customer.Order.isDeleted = true;
                    _context.Orders.Update(customer.Order);

                    // Get order waiters and set their availability to Available
                    var orderWaiters = await _context.OrderWaiters
                        .Include(ow => ow.Waiter)
                        .Where(ow => ow.OrderId == customer.Order.OrderId)
                        .ToListAsync();

                    // Set waiters' availability to Available
                    foreach (var orderWaiter in orderWaiters)
                    {
                        if (orderWaiter.Waiter != null)
                        {
                            orderWaiter.Waiter.Availability = "Available";
                            _context.Waiters.Update(orderWaiter.Waiter);
                        }
                    }
                }
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.CustomerID == id && !e.isDeleted);
        }

        public async Task<IActionResult> ViewOrder(int? id, bool? fromPastOrders, bool? showInvoiceModal)
        {
            if (id == null)
                return BadRequest();

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Menu)
                .Include(o => o.HeadWaiter)
                    .ThenInclude(hw => hw.User)
                .FirstOrDefaultAsync(o => o.CustomerID == id.Value && !o.Customer.isDeleted && !o.isDeleted);

            if (order == null)
                return NotFound();

            // Compute additional charges and remaining balance using strict allocation
            var materialReturns = await _context.Set<MaterialReturn>()
                .Where(r => r.OrderId == order.OrderId)
                .ToListAsync();

            var additionalCharges = materialReturns.Sum(r => (r.Lost + r.Damaged) * r.ChargePerItem);
            var chargedItems = materialReturns
                .Where(r => r.Lost > 0 || r.Damaged > 0)
                .Select(r => new { r.MaterialName, r.Lost, r.Damaged, r.ChargePerItem })
                .ToList();

            var effectiveTotal = order.TotalPayment + (double)additionalCharges;

            var existingPayments = await _context.Payments
                .Where(p => p.OrderId == order.OrderId)
                .OrderBy(p => p.Date)
                .ToListAsync();
            var allocation = AllocatePaymentsToBaseThenCharges(order, existingPayments);
            var remainingBase = Math.Max(0d, order.TotalPayment - allocation.baseAllocated);
            var remainingCharges = Math.Max(0d, (double)additionalCharges - allocation.chargesAllocated);
            var remainingBalance = remainingBase + remainingCharges;

            // Pass to View
            ViewBag.AdditionalCharges = additionalCharges;
            ViewBag.ChargedItems = chargedItems;
            ViewBag.EffectiveTotal = effectiveTotal;
            ViewBag.RemainingBalance = remainingBalance;

            // Rush order breakdown (base and fee)
            var baseTotal = order.OrderDetails.Sum(od => (od.Menu?.Price ?? 0) * od.Quantity);
            var rushFee = order.IsRushOrder ? baseTotal * 0.10 : 0d;
            ViewBag.RushBaseTotal = baseTotal;
            ViewBag.RushOrderFee = rushFee;
            ViewBag.RushTotal = baseTotal + rushFee;

            // Role flag for view logic
            var role = HttpContext.Session.GetString("Role");
            ViewBag.IsAdmin = role == "ADMIN";

            // If order is completed, get assigned waiters
            if (order.Status == "Completed")
            {
                var orderWaiters = await _context.OrderWaiters
                    .Include(ow => ow.Waiter)
                        .ThenInclude(w => w.User)
                    .Where(ow => ow.OrderId == order.OrderId)
                    .ToListAsync();

                ViewBag.OrderWaiters = orderWaiters;
            }

            ViewBag.FromPastOrders = fromPastOrders ?? false;
            ViewBag.ShowInvoiceModal = showInvoiceModal ?? false;
            return View(order);
        }

        [HttpGet]
        public async Task<IActionResult> GenerateInvoice(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Menu)
                .FirstOrDefaultAsync(o => o.CustomerID == id && !o.Customer.isDeleted && !o.isDeleted);

            if (order == null)
                return NotFound();

            // Optional guard: only allow when paid and completed
            if (!(order.Customer.IsPaid && order.Status == "Completed"))
            {
                return RedirectToAction(nameof(ViewOrder), new { id });
            }

            // Compute charges
            var materialReturns = await _context.Set<MaterialReturn>()
                .Where(r => r.OrderId == order.OrderId)
                .ToListAsync();
            var additionalCharges = materialReturns.Sum(r => (r.Lost + r.Damaged) * r.ChargePerItem);
            var effectiveTotal = order.TotalPayment + (double)additionalCharges;

            string Peso(double v) => $"₱{v:N2}";

            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Row(row =>
                    {
                        row.RelativeColumn().Stack(stack =>
                        {
                            stack.Item().Text("GRUSH Catering").FontSize(16).SemiBold();
                            stack.Item().Text("Official Invoice").FontColor(Colors.Blue.Medium);
                        });

                        row.ConstantColumn(260).AlignRight().Stack(stack =>
                        {
                            stack.Item().Text(text =>
                            {
                                text.Span("Order No: ").SemiBold();
                                text.Span(string.IsNullOrWhiteSpace(order.OrderNumber) ? $"ORD-{order.OrderDate:yyyyMMdd}-{order.OrderId:D3}" : order.OrderNumber);
                            });
                            stack.Item().Text(text =>
                            {
                                text.Span("Order Date: ").SemiBold();
                                text.Span(order.OrderDate.ToString("dd/MM/yyyy"));
                            });
                            stack.Item().Text(text =>
                            {
                                text.Span("Catering Date: ").SemiBold();
                                text.Span(order.CateringDate.ToString("dd/MM/yyyy"));
                            });
                        });
                    });

                    page.Content().Stack(stack =>
                    {
                        stack.Spacing(10);

                        // Billed To
                        stack.Item().Text("Billed To").SemiBold();
                        stack.Item().Text(order.Customer.Name);
                        if (!string.IsNullOrWhiteSpace(order.Customer.ContactNo))
                            stack.Item().Text(order.Customer.ContactNo).FontColor(Colors.Blue.Medium);
                        if (!string.IsNullOrWhiteSpace(order.Customer.Address))
                            stack.Item().Text(order.Customer.Address);

                        stack.Item().PaddingTop(10).Element(container =>
                        {
                            container.Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(5);   // Menu
                                    columns.RelativeColumn(2);   // Qty
                                    columns.RelativeColumn(3);   // Unit Price
                                    columns.RelativeColumn(3);   // Subtotal
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellHeader).Text("Menu");
                                    header.Cell().Element(CellHeader).AlignRight().Text("Quantity");
                                    header.Cell().Element(CellHeader).AlignRight().Text("Unit Price");
                                    header.Cell().Element(CellHeader).AlignRight().Text("Subtotal");

                                    static IContainer CellHeader(IContainer container) =>
                                        container.DefaultTextStyle(x => x.SemiBold())
                                            .Background(Colors.Grey.Lighten3)
                                            .PaddingVertical(6)
                                            .PaddingHorizontal(8);
                                });

                                foreach (var item in order.OrderDetails)
                                {
                                    var unit = item.Menu?.Price ?? 0;
                                    var subtotal = unit * item.Quantity;
                                    table.Cell().Element(Cell).Text(item.Name);
                                    table.Cell().Element(Cell).AlignRight().Text(item.Quantity.ToString());
                                    table.Cell().Element(Cell).AlignRight().Text(Peso(unit));
                                    table.Cell().Element(Cell).AlignRight().Text(Peso(subtotal));
                                }

                                static IContainer Cell(IContainer container) =>
                                    container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3)
                                             .PaddingVertical(6).PaddingHorizontal(8);
                            });
                        });

                        // Totals
                        stack.Item().PaddingTop(10).AlignRight().Stack(totals =>
                        {
                            totals.Spacing(4);
                            totals.Item().Text(text =>
                            {
                                text.Span("Base Total: ").SemiBold();
                                text.Span(Peso(order.TotalPayment));
                            });

                            if (additionalCharges > 0)
                            {
                                totals.Item().Text(text =>
                                {
                                    text.Span("Additional Charges: ").SemiBold().FontColor(Colors.Red.Medium);
                                    text.Span(Peso((double)additionalCharges)).FontColor(Colors.Red.Medium);
                                });
                            }

                            totals.Item().Text(text =>
                            {
                                text.Span("Invoice Total: ").SemiBold().FontSize(13);
                                text.Span(Peso(effectiveTotal)).SemiBold().FontSize(13);
                            });
                        });
                    });

                    page.Footer().AlignCenter().Text("Thank you for your business!");
                });
            });

            var pdf = document.GeneratePdf();
            var fileName = $"{order.Customer.Name} - Order Invoice.pdf";
            return File(pdf, "application/pdf", fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerID == id && !c.isDeleted);
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
                .FirstOrDefaultAsync(c => c.CustomerID == id && !c.isDeleted);
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
            var customer = await _context.Customers.Include(c => c.Order).FirstOrDefaultAsync(c => c.CustomerID == customerId && !c.isDeleted);
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

            // Update order's AmountPaid (raw sum for reference)
            customer.Order.AmountPaid += paymentAmount;
            _context.Orders.Update(customer.Order);

            // Check if down payment is now met and update status to Accepted
            if (customer.Order.DownPaymentMet && customer.Order.Status == "Pending")
            {
                customer.Order.Status = "Accepted";
                _context.Orders.Update(customer.Order);
            }

            // Determine effective total including all additional charges
            var additionalCharges = await _context.MaterialReturns
                .Where(r => r.OrderId == customer.Order.OrderId)
                .SumAsync(r => (double)((r.Lost + r.Damaged) * r.ChargePerItem));
            var effectiveTotal = customer.Order.TotalPayment + additionalCharges;

            // Allocate strictly using all payments including this one
            var paymentsSoFar = await _context.Payments
                .Where(p => p.OrderId == customer.Order.OrderId)
                .OrderBy(p => p.Date)
                .ToListAsync();
            // paymentsSoFar already includes the newly added payment because we added it to the context above
            var allocation = AllocatePaymentsToBaseThenCharges(customer.Order, paymentsSoFar);
            var remainingBase = Math.Max(0d, customer.Order.TotalPayment - allocation.baseAllocated);
            var remainingCharges = Math.Max(0d, (double)additionalCharges - allocation.chargesAllocated);
            var remainingBalance = remainingBase + remainingCharges;

            // If fully paid per strict allocation, mark as paid; only mark Completed if returns exist
            if (remainingBalance <= 0.000001)
            {
                customer.IsPaid = true;
                var returnsExist = await _context.MaterialReturns.AnyAsync(r => r.OrderId == customer.Order.OrderId);
                if (returnsExist)
                {
                    customer.Order.Status = "Completed";
                    // Set waiters back to Available when truly completed
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
                _context.Customers.Update(customer);
                _context.Orders.Update(customer.Order);
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
                .FirstOrDefaultAsync(c => c.CustomerID == id && !c.isDeleted);

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

            // Prevent marking as Completed unless materials have been returned and all charges are settled
            if (cateringStatus == "Completed")
            {
                var orderId = customer.Order.OrderId;
                var returnsExist = await _context.MaterialReturns.AnyAsync(r => r.OrderId == orderId);
                var additionalCharges = await _context.MaterialReturns
                    .Where(r => r.OrderId == orderId)
                    .SumAsync(r => (decimal)((r.Lost + r.Damaged) * r.ChargePerItem));

                // Compute remaining balance using strict allocation
                var payments = await _context.Payments
                    .Where(p => p.OrderId == orderId)
                    .OrderBy(p => p.Date)
                    .ToListAsync();
                var allocation = AllocatePaymentsToBaseThenCharges(customer.Order, payments);
                var remainingBase = Math.Max(0d, customer.Order.TotalPayment - allocation.baseAllocated);
                var remainingCharges = Math.Max(0d, (double)additionalCharges - allocation.chargesAllocated);
                var remainingBalance = remainingBase + remainingCharges;

                if (!returnsExist || remainingBalance > 0.000001)
                {
                    TempData["CateringStatusError"] = "Cannot mark as Completed until materials are returned and all charges are fully paid.";
                    return RedirectToAction(nameof(Index));
                }
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

            var customer = await _context.Customers.Include(c => c.Order).FirstOrDefaultAsync(c => c.CustomerID == id && !c.isDeleted);
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
            var customer = await _context.Customers.Include(c => c.Order).FirstOrDefaultAsync(c => c.CustomerID == id && !c.isDeleted);
            if (customer == null || customer.Order == null)
                return NotFound();

            customer.Order.HeadWaiterId = headWaiterId;
            _context.Orders.Update(customer.Order);
            await _context.SaveChangesAsync();
            TempData["HeadWaiterAssigned"] = "Head Waiter assigned successfully.";
            return RedirectToAction("AssignWaiter", "PaidOrders", new { id = id, headWaiterId = headWaiterId });
        }

        // GET: Customers/InventoryReport/5
        public async Task<IActionResult> InventoryReport(int id, bool? fromPastOrders)
        {
            // id = CustomerId
            Order order = await _context.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.CustomerID == id && !o.Customer.isDeleted);
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
            ViewBag.FromPastOrders = fromPastOrders ?? false;
            return View(viewModel);
        }

        // GET: Customers/OrdersByDate
        public async Task<IActionResult> OrdersByDate(DateTime? selectedDate = null)
        {
            var date = selectedDate ?? DateTime.Today;

            var ordersForDate = await _context.Orders
                .Include(o => o.Customer)
                .Where(o => o.CateringDate.Date == date.Date && !o.Customer.isDeleted && !o.isDeleted)
                .OrderBy(o => o.timeOfFoodServing)
                .ToListAsync();

            int totalPax = ordersForDate.Where(o => o.Status == "Accepted").Sum(o => o.NoOfPax);
            bool hasLargeOrder = ordersForDate.Any(o => o.NoOfPax >= 701 && o.NoOfPax <= 1500);

            ViewBag.SelectedDate = date;
            ViewBag.TotalPax = totalPax;
            ViewBag.HasLargeOrder = hasLargeOrder;
            ViewBag.MaxPax = 700;

            return View(ordersForDate);
        }

        // GET: Customers/PastOrders
        public async Task<IActionResult> PastOrders(string searchString, DateTime? startDate, DateTime? endDate)
        {
            var ordersQuery = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.HeadWaiter)
                    .ThenInclude(hw => hw.User)
                .Where(o => !o.isDeleted && o.Customer != null && !o.Customer.isDeleted && o.Status == "Completed")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var term = searchString.Trim().ToLower();
                ordersQuery = ordersQuery.Where(o =>
                    (!string.IsNullOrEmpty(o.OrderNumber) && o.OrderNumber.ToLower().Contains(term)) ||
                    (o.Customer != null && o.Customer.Name.ToLower().Contains(term)) ||
                    (!string.IsNullOrEmpty(o.Venue) && o.Venue.ToLower().Contains(term))
                );
            }

            if (startDate.HasValue)
            {
                var start = startDate.Value.Date;
                ordersQuery = ordersQuery.Where(o => o.CateringDate.Date >= start);
            }
            if (endDate.HasValue)
            {
                var end = endDate.Value.Date;
                ordersQuery = ordersQuery.Where(o => o.CateringDate.Date <= end);
            }

            var orders = await ordersQuery
                .OrderByDescending(o => o.CateringDate)
                .ThenByDescending(o => o.OrderDate)
                .ToListAsync();

            // Compute additional charges, payments, and remaining balances per order using strict allocation
            var orderIds = orders.Select(o => o.OrderId).ToList();

            var additionalChargesByOrder = await _context.MaterialReturns
                .Where(r => orderIds.Contains(r.OrderId))
                .GroupBy(r => r.OrderId)
                .Select(g => new { OrderId = g.Key, TotalCharge = g.Sum(r => (r.Lost + r.Damaged) * r.ChargePerItem) })
                .ToListAsync();
            var additionalChargesDict = additionalChargesByOrder.ToDictionary(x => x.OrderId, x => (double)x.TotalCharge);

            var paymentsAll = await _context.Payments
                .Where(p => orderIds.Contains(p.OrderId))
                .OrderBy(p => p.Date)
                .ToListAsync();
            var paymentsByOrder = paymentsAll.GroupBy(p => p.OrderId).ToDictionary(g => g.Key, g => g.ToList());

            var effectiveTotalByOrder = new Dictionary<int, double>();
            var paymentsTotalByOrder = new Dictionary<int, double>();
            var remainingBalanceByOrder = new Dictionary<int, double>();

            foreach (var order in orders)
            {
                var extra = additionalChargesDict.TryGetValue(order.OrderId, out var total) ? total : 0d;
                effectiveTotalByOrder[order.OrderId] = order.TotalPayment + extra;

                var plist = paymentsByOrder.TryGetValue(order.OrderId, out var list) ? list : new List<Payment>();
                paymentsTotalByOrder[order.OrderId] = plist.Sum(p => p.Amount);

                var allocation = AllocatePaymentsToBaseThenCharges(order, plist);
                var remainingBase = Math.Max(0d, order.TotalPayment - allocation.baseAllocated);
                var remainingCharges = Math.Max(0d, extra - allocation.chargesAllocated);
                remainingBalanceByOrder[order.OrderId] = remainingBase + remainingCharges;
            }

            ViewBag.AdditionalChargesByOrder = additionalChargesDict;
            ViewBag.EffectiveTotalByOrder = effectiveTotalByOrder;
            ViewBag.PaymentsTotalByOrder = paymentsTotalByOrder;
            ViewBag.RemainingBalanceByOrder = remainingBalanceByOrder;

            ViewBag.SearchString = searchString;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            return View(orders);
        }

        // GET: Customers/DeletedHistory
        public async Task<IActionResult> DeletedHistory(string searchString)
        {
            var deletedCustomers = _context.Customers
                .Include(c => c.Order)
                    .ThenInclude(o => o.HeadWaiter)
                        .ThenInclude(hw => hw.User)
                .Where(c => c.isDeleted) // Only show soft-deleted customers
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                var searchTerm = searchString.ToLower().Trim();
                deletedCustomers = deletedCustomers.Where(s =>
                    s.Name.ToLower().Contains(searchTerm) ||
                    (s.Order != null && !string.IsNullOrEmpty(s.Order.OrderNumber) &&
                     s.Order.OrderNumber.ToLower().Contains(searchTerm))
                );
            }

            return View(await deletedCustomers.ToListAsync());
        }

        // POST: Customers/RestoreCustomer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreCustomers(int[] customerIds, int? customerId)
        {
            try
            {
                // Normalize input: if single restore is triggered, wrap it into an array
                if (customerId.HasValue)
                    customerIds = new[] { customerId.Value };

                if (customerIds == null || customerIds.Length == 0)
                {
                    TempData["RestoreError"] = "No customers selected for restoration.";
                    return RedirectToAction(nameof(DeletedHistory));
                }

                var customers = await _context.Customers
                    .Include(c => c.Order)
                    .Where(c => customerIds.Contains(c.CustomerID) && c.isDeleted)
                    .ToListAsync();

                if (!customers.Any())
                {
                    TempData["RestoreError"] = "No valid customers found for restoration.";
                    return RedirectToAction(nameof(DeletedHistory));
                }

                int restoredCount = 0;
                int orderRestoredCount = 0;

                foreach (var customer in customers)
                {
                    // Restore customer
                    customer.isDeleted = false;
                    _context.Customers.Update(customer);
                    restoredCount++;

                    // Restore order if exists
                    if (customer.Order != null)
                    {
                        customer.Order.isDeleted = false;
                        _context.Orders.Update(customer.Order);
                        orderRestoredCount++;

                        // If order is not completed, re-mark assigned waiters as Busy so it reappears in their views
                        if (customer.Order.Status != "Completed")
                        {
                            var orderWaiters = await _context.OrderWaiters
                                .Include(ow => ow.Waiter)
                                .Where(ow => ow.OrderId == customer.Order.OrderId)
                                .ToListAsync();

                            foreach (var orderWaiter in orderWaiters)
                            {
                                if (orderWaiter.Waiter != null)
                                {
                                    orderWaiter.Waiter.Availability = "Busy";
                                    _context.Waiters.Update(orderWaiter.Waiter);
                                }
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();

                // Unified success message
                TempData["RestoreSuccess"] =
                    (orderRestoredCount > 0)
                    ? $"{restoredCount} customers and {orderRestoredCount} orders restored successfully."
                    : $"{restoredCount} customers restored successfully.";

                return RedirectToAction(nameof(DeletedHistory));
            }
            catch (Exception ex)
            {
                TempData["RestoreError"] = $"Error restoring customer(s): {ex.Message}";
                return RedirectToAction(nameof(DeletedHistory));
            }
        }


        // POST: Customers/PermanentlyDeleteCustomer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PermanentlyDeleteCustomers(int[] customerIds, int? customerId)
        {
            // Normalize input to an array of IDs
            if (customerId.HasValue)
                customerIds = new[] { customerId.Value };

            if (customerIds == null || customerIds.Length == 0)
            {
                TempData["PermanentDeleteError"] = "No customers selected for permanent deletion.";
                return RedirectToAction(nameof(DeletedHistory));
            }

            var customers = await _context.Customers
                .Include(c => c.Order)
                .Where(c => customerIds.Contains(c.CustomerID) && c.isDeleted)
                .ToListAsync();

            if (!customers.Any())
            {
                TempData["PermanentDeleteError"] = "No valid customers found for permanent deletion.";
                return RedirectToAction(nameof(DeletedHistory));
            }

            int deletedCount = 0;
            int orderDeletedCount = 0;

            foreach (var customer in customers)
            {
                if (customer.Order != null)
                {
                    var orderId = customer.Order.OrderId;

                    // Remove related order data
                    _context.OrderWaiters.RemoveRange(
                        await _context.OrderWaiters.Where(ow => ow.OrderId == orderId).ToListAsync()
                    );

                    _context.MaterialPullOuts.RemoveRange(
                        await _context.MaterialPullOuts.Where(p => p.OrderId == orderId).ToListAsync()
                    );

                    _context.MaterialReturns.RemoveRange(
                        await _context.MaterialReturns.Where(r => r.OrderId == orderId).ToListAsync()
                    );

                    _context.Payments.RemoveRange(
                        await _context.Payments.Where(p => p.OrderId == orderId).ToListAsync()
                    );

                    _context.OrderDetails.RemoveRange(
                        await _context.OrderDetails.Where(od => od.OrderId == orderId).ToListAsync()
                    );

                    _context.Orders.Remove(customer.Order);
                    orderDeletedCount++;
                }

                _context.Customers.Remove(customer);
                deletedCount++;
            }

            await _context.SaveChangesAsync();

            TempData["PermanentDeleteSuccess"] =
                (orderDeletedCount > 0)
                ? $"{deletedCount} customers and {orderDeletedCount} orders permanently deleted from the database."
                : $"{deletedCount} customers permanently deleted from the database.";

            return RedirectToAction(nameof(DeletedHistory));
        }

        // GET: Customers/ViewDeletedOrder
        public async Task<IActionResult> ViewDeletedOrder(int? id)
        {
            if (id == null)
                return BadRequest();

            var customer = await _context.Customers
                .Include(c => c.Order)
                .FirstOrDefaultAsync(c => c.CustomerID == id.Value && c.isDeleted);

            if (customer == null)
                return NotFound();

            // Check if the order is also soft-deleted
            ViewBag.OrderDeleted = customer.Order != null && customer.Order.isDeleted;

            return View(customer);
        }

        //// GET: Customers/DateSummary
        //public async Task<IActionResult> DateSummary(DateTime? startDate = null, DateTime? endDate = null)
        //{
        //    var start = startDate ?? DateTime.Today.AddDays(-30);
        //    var end = endDate ?? DateTime.Today.AddDays(30);

        //    var ordersInRange = await _context.Orders
        //        .Include(o => o.Customer)
        //        .Where(o => o.CateringDate.Date >= start.Date && o.CateringDate.Date <= end.Date)
        //        .OrderBy(o => o.CateringDate)
        //        .ToListAsync();

        //    var dateSummary = ordersInRange
        //        .GroupBy(o => o.CateringDate.Date)
        //        .Select(g => new DateSummaryViewModel
        //        {
        //            Date = g.Key,
        //            TotalPax = g.Sum(o => o.NoOfPax),
        //            HasLargeOrder = g.Any(o => o.NoOfPax >= 701 && o.NoOfPax <= 1500),
        //            OrderCount = g.Count()
        //        })
        //        .OrderBy(x => x.Date)
        //        .ToList();

        //    var viewModel = new DateSummaryPageViewModel
        //    {
        //        StartDate = start,
        //        EndDate = end,
        //        DateSummary = dateSummary
        //    };

        //    return View(viewModel);
        //}
    }
}
