using Microsoft.AspNetCore.Mvc;
using Capstone2.Models;
using Capstone2.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Capstone2.Controllers
{
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Form(string OrderItemsJson, Order? order)
        {
            Order? currentOrder = TempData?["Order"] != null ? JsonSerializer.Deserialize<Order>(TempData?["Order"] as string) : null;

            // Step 1: From ClientMenus
            if (currentOrder != null)
            {
                TempData["OrderItemsJson"] = JsonSerializer.Serialize(currentOrder.OrderDetails);
                ViewBag.SelectedItems = currentOrder.OrderDetails;

                currentOrder.CateringDate = DateTime.Now;
                currentOrder.Customer = new Customer();

                return View(currentOrder);
            }

            ModelState.Remove("Customer.Order");
            ModelState.Remove("Status");
            ModelState.Remove("OrderNumber");
            order.Status = "Pending";

            // Step 2: Final submission
            if (ModelState.IsValid)
            {
                List<OrderDetail> selectedItems = JsonSerializer.Deserialize<List<OrderDetail>>(OrderItemsJson);
                selectedItems.ForEach(x =>
                {
                    _context.Entry(x).Reference(x => x.Menu).Load();
                });

                order.OrderDetails = selectedItems;

                // Calculate base amount and rush order fee
                double baseAmount = selectedItems.Sum(x => x.Menu.Price * x.Quantity);
                order.BaseAmount = baseAmount;

                // Check if it's a rush order (same day)
                if (order.OrderDate.Date == order.CateringDate.Date)
                {
                    order.RushOrderFee = baseAmount * 0.10; // 10% rush order fee
                    order.TotalPayment = baseAmount + order.RushOrderFee;
                }
                else
                {
                    order.RushOrderFee = 0;
                    order.TotalPayment = baseAmount;
                }

                TempData["Order"] = JsonSerializer.Serialize(order);
                return RedirectToAction("Index", "OrderDetails");
            }

            return View(order);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Menu)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.MenuPackage)
                .FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null) return NotFound();

            // Provide package metadata to the view for dynamic total preview
            bool isPackageOrder = order.OrderDetails != null && order.OrderDetails.Any(od => od.MenuPackageId != null);
            ViewBag.IsPackageOrder = isPackageOrder;
            if (isPackageOrder)
            {
                // Prefer live package price
                var livePackagePrice = order.OrderDetails
                    .Where(od => od.MenuPackageId != null)
                    .Select(od => (double?)(od.MenuPackage != null ? od.MenuPackage.Price : (double?)null))
                    .FirstOrDefault() ?? 0d;
                if (livePackagePrice > 0)
                {
                    ViewBag.PackagePrice = livePackagePrice;
                }
                else
                {
                    var storedPackagePrice = order.OrderDetails
                        .Where(od => od.MenuPackageId != null && od.PackagePrice != null)
                        .Select(od => (double)od.PackagePrice.Value)
                        .FirstOrDefault();
                    ViewBag.PackagePrice = storedPackagePrice;
                }

                // Get package minimum pax requirement
                var packageMinPax = order.OrderDetails
                    .Where(od => od.MenuPackageId != null)
                    .Select(od => od.MenuPackage != null ? od.MenuPackage.MinimumPax : 60)
                    .FirstOrDefault();
                ViewBag.PackageMinPax = packageMinPax;

                // Sum of existing extras
                double extrasTotal = 0d;
                foreach (var od in order.OrderDetails)
                {
                    if (!od.IsFreeLechon && string.Equals(od.Type, "Package Extra", StringComparison.OrdinalIgnoreCase))
                    {
                        extrasTotal += od.Menu?.Price ?? 0d;
                    }
                }
                ViewBag.PackageExtrasTotal = extrasTotal;
            }
            else
            {
                // For non-package orders, use default minimum of 60
                ViewBag.PackageMinPax = 60;
            }
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Order model)
        {
            ModelState.Remove("Customer.Order");
            ModelState.Remove("OrderNumber");

            // Get the order with package information for validation
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Menu)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.MenuPackage)
                .FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null) return NotFound();

            // Validate NoOfPax against package minimum requirement BEFORE ModelState.IsValid check
            bool isPackageOrder = order.OrderDetails != null && order.OrderDetails.Any(od => od.MenuPackageId != null);
            if (isPackageOrder)
            {
                var packageMinPax = order.OrderDetails
                    .Where(od => od.MenuPackageId != null)
                    .Select(od => od.MenuPackage != null ? od.MenuPackage.MinimumPax : 60)
                    .FirstOrDefault();

                if (model.NoOfPax < packageMinPax)
                {
                    ModelState.AddModelError("NoOfPax", $"Number of people must be at least {packageMinPax} for this package.");
                }
            }
            else
            {
                // For non-package orders, use default minimum of 60
                if (model.NoOfPax < 60)
                {
                    ModelState.AddModelError("NoOfPax", "Number of people must be at least 60.");
                }
            }

            if (!ModelState.IsValid)
            {
                // Re-populate ViewBag data for the view
                ViewBag.IsPackageOrder = isPackageOrder;
                if (isPackageOrder)
                {
                    var packageMinPax = order.OrderDetails
                        .Where(od => od.MenuPackageId != null)
                        .Select(od => od.MenuPackage != null ? od.MenuPackage.MinimumPax : 60)
                        .FirstOrDefault();
                    ViewBag.PackageMinPax = packageMinPax;
                }
                else
                {
                    ViewBag.PackageMinPax = 60;
                }
                return View(model);
            }

            // Update order fields
            order.Venue = model.Venue;
            order.CateringDate = model.CateringDate;
            order.timeOfFoodServing = model.timeOfFoodServing;
            order.Occasion = model.Occasion;
            order.Motif = model.Motif;
            order.NoOfPax = model.NoOfPax;
            order.AdditionalNotes = model.AdditionalNotes;

            // Update customer fields
            order.Customer.Name = model.Customer.Name;
            order.Customer.ContactNo = model.Customer.ContactNo;
            order.Customer.Address = model.Customer.Address;

            // Recalculate totals in a single, package-agnostic helper
            var pricing = Capstone2.Helpers.OrderPricing.Compute(order, _context);
            order.TotalPayment = pricing.Total;

            _context.Orders.Update(order);
            await _context.SaveChangesAsync();
            return RedirectToAction("ViewOrder", "Customers", new { id = order.CustomerID });
        }
    }
}
