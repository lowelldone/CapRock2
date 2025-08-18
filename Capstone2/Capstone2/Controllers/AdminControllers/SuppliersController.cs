using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;
using System.ComponentModel.DataAnnotations;

namespace Capstone2.Controllers.AdminControllers
{
    public class SuppliersController : GenericController
    {
        private readonly ApplicationDbContext _context;

        public SuppliersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Suppliers
        public async Task<IActionResult> Index(string searchString)
        {
            var suppliers = from c in _context.Suppliers
                            select c;

            if (!String.IsNullOrEmpty(searchString))
            {
                suppliers = suppliers.Where(s => s.CompanyName.ToLower().Contains(searchString.ToLower()));
            }

            return View(await suppliers.ToListAsync());
        }

        // GET: Suppliers/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Suppliers/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SupplierId,CompanyName,ContactPerson,ContactNo")] Supplier supplier)
        {
            if (ModelState.IsValid)
            {
                _context.Add(supplier);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(supplier);
        }

        // GET: Suppliers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                return NotFound();
            }
            return View(supplier);
        }

        // POST: Suppliers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("SupplierId,CompanyName,ContactPerson,ContactNo")] Supplier supplier)
        {
            if (id != supplier.SupplierId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(supplier);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SupplierExists(supplier.SupplierId))
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
            return View(supplier);
        }

        // GET: Suppliers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(m => m.SupplierId == id);
            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        // POST: Suppliers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier != null)
            {
                _context.Suppliers.Remove(supplier);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SupplierExists(int id)
        {
            return _context.Suppliers.Any(e => e.SupplierId == id);
        }

        // ================== Extended Supplier Management ==================
        // Maintains materials supplied and their pricing per supplier

        // GET: Suppliers/ManagePrices/5 (view)
        [HttpGet]
        public IActionResult ManagePrices(int id)
        {
            ViewBag.SupplierId = id;
            return View();
        }

        // GET: Suppliers/ManageTransactions/5 (view)
        [HttpGet]
        public IActionResult ManageTransactions(int id, string view = "po")
        {
            ViewBag.SupplierId = id;
            ViewBag.ViewMode = (view ?? "po").ToLower();
            return View();
        }

        // GET: Suppliers/ViewTransactions/5
        [HttpGet]
        public async Task<IActionResult> ViewTransactions(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();

            var viewTransactions = await _context.ViewTransactions
                .Where(v => v.SupplierId == id)
                .OrderByDescending(v => v.OrderDate)
                .ToListAsync();

            ViewBag.SupplierId = id;
            ViewBag.SupplierName = supplier.CompanyName;
            return View(viewTransactions);
        }

        // GET: Suppliers/Prices/5
        [HttpGet]
        public async Task<IActionResult> Prices(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();

            var prices = await _context.SupplierMaterialPrices
                .Include(smp => smp.Material)
                .Where(smp => smp.SupplierId == id)
                .Select(smp => new
                {
                    smp.SupplierMaterialPriceId,
                    smp.MaterialId,
                    MaterialName = smp.Material.Name,
                    smp.UnitPrice,
                    smp.LastUpdated
                })
                .ToListAsync();

            var allMaterials = await _context.Materials
                .Select(m => new { m.MaterialId, m.Name, m.IsConsumable })
                .ToListAsync();

            return Ok(new { Supplier = supplier, Materials = allMaterials, Prices = prices });
        }

        // POST: Suppliers/SetPrice
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPrice(int supplierId, int materialId, decimal unitPrice)
        {
            if (unitPrice < 0)
            {
                TempData["SupplierError"] = "Unit price must be non-negative.";
                return RedirectToAction(nameof(ManagePrices), new { id = supplierId });
            }

            var supplier = await _context.Suppliers.FindAsync(supplierId);
            var material = await _context.Materials.FindAsync(materialId);
            if (supplier == null || material == null) return NotFound();

            var existing = await _context.SupplierMaterialPrices
                .FirstOrDefaultAsync(x => x.SupplierId == supplierId && x.MaterialId == materialId);

            if (existing == null)
            {
                existing = new SupplierMaterialPrice
                {
                    SupplierId = supplierId,
                    MaterialId = materialId,
                    UnitPrice = unitPrice,
                    LastUpdated = DateTime.Now
                };
                _context.SupplierMaterialPrices.Add(existing);
            }
            else
            {
                existing.UnitPrice = unitPrice;
                existing.LastUpdated = DateTime.Now;
                _context.SupplierMaterialPrices.Update(existing);
            }

            await _context.SaveChangesAsync();
            TempData["SupplierSuccess"] = "Supplier material price saved.";
            return RedirectToAction(nameof(ManagePrices), new { id = supplierId });
        }

        // ================== Transactions & Deliveries ==================

        // GET: Suppliers/Transactions/5
        [HttpGet]
        public async Task<IActionResult> Transactions(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();

            var transactions = await _context.SupplierTransactions
                .Include(t => t.Material)
                .Where(t => t.SupplierId == id)
                .OrderByDescending(t => t.OrderDate)
                .Select(t => new
                {
                    t.SupplierTransactionId,
                    t.MaterialId,
                    MaterialName = t.Material.Name,
                    t.Quantity,
                    t.ReceivedQuantity,
                    t.UnitPrice,
                    t.OrderDate,
                    t.ExpectedDeliveryDate,
                    t.DeliveredDate,
                    t.Status
                })
                .ToListAsync();

            return Ok(new { Supplier = supplier, Transactions = transactions });
        }

        // GET: Suppliers/PurchaseOrders/5
        [HttpGet]
        public async Task<IActionResult> PurchaseOrders(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();

            var pos = await _context.PurchaseOrders
                .Include(po => po.Material)
                .Where(po => po.SupplierId == id)
                .OrderByDescending(po => po.CreatedAt)
                .Select(po => new
                {
                    po.PurchaseOrderId,
                    po.MaterialId,
                    MaterialName = po.Material.Name,
                    po.Quantity,
                    po.ReceivedQuantity,
                    po.UnitPrice,
                    po.CreatedAt,
                    po.ScheduledDelivery,
                    po.Status
                })
                .ToListAsync();

            return Ok(new { Supplier = supplier, PurchaseOrders = pos });
        }

        // POST: Suppliers/CreatePurchaseOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePurchaseOrder(int supplierId, int materialId, int quantity, DateTime? scheduledDelivery)
        {
            if (quantity <= 0)
            {
                TempData["SupplierError"] = "Quantity must be greater than zero.";
                return RedirectToAction(nameof(ManageTransactions), new { id = supplierId });
            }

            var supplier = await _context.Suppliers.FindAsync(supplierId);
            var material = await _context.Materials.FindAsync(materialId);
            if (supplier == null || material == null) return NotFound();

            var price = await _context.SupplierMaterialPrices
                .Where(p => p.SupplierId == supplierId && p.MaterialId == materialId)
                .Select(p => (decimal?)p.UnitPrice)
                .FirstOrDefaultAsync() ?? material.Price;

            var po = new PurchaseOrder
            {
                SupplierId = supplierId,
                MaterialId = materialId,
                Quantity = quantity,
                UnitPrice = price,
                ScheduledDelivery = scheduledDelivery,
                Status = "Ordered"
            };
            _context.PurchaseOrders.Add(po);

            // Create pending transaction record (to be completed when delivered)
            var tx = new SupplierTransaction
            {
                SupplierId = supplierId,
                MaterialId = materialId,
                Quantity = quantity,
                UnitPrice = price,
                OrderDate = DateTime.Now,
                ExpectedDeliveryDate = scheduledDelivery,
                Status = "Ordered"
            };
            _context.SupplierTransactions.Add(tx);

            await _context.SaveChangesAsync();
            TempData["SupplierSuccess"] = "Purchase order created.";
            return RedirectToAction(nameof(ManageTransactions), new { id = supplierId });
        }

        // POST: Suppliers/ReceivePurchaseOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceivePurchaseOrder(int purchaseOrderId, int? deliveredQuantity)
        {
            var po = await _context.PurchaseOrders.FindAsync(purchaseOrderId);
            if (po == null) return NotFound();

            int qty = deliveredQuantity.HasValue && deliveredQuantity.Value > 0 ? deliveredQuantity.Value : po.Quantity;

            // Update inventory
            var material = await _context.Materials.FindAsync(po.MaterialId);
            if (material == null) return NotFound();
            material.Quantity += qty;
            _context.Materials.Update(material);

            // Mark PO delivered
            po.Status = "Delivered";
            po.ReceivedQuantity = qty;
            _context.PurchaseOrders.Update(po);

            // Update or create transaction as delivered
            var tx = await _context.SupplierTransactions
                .Where(t => t.SupplierId == po.SupplierId && t.MaterialId == po.MaterialId && t.Status == "Ordered")
                .OrderByDescending(t => t.OrderDate)
                .FirstOrDefaultAsync();
            if (tx != null)
            {
                tx.DeliveredDate = DateTime.Now;
                tx.Status = "Delivered";
                tx.ReceivedQuantity = qty;
                _context.SupplierTransactions.Update(tx);
            }
            else
            {
                _context.SupplierTransactions.Add(new SupplierTransaction
                {
                    SupplierId = po.SupplierId,
                    MaterialId = po.MaterialId,
                    Quantity = qty,
                    ReceivedQuantity = qty,
                    UnitPrice = po.UnitPrice,
                    OrderDate = po.CreatedAt,
                    DeliveredDate = DateTime.Now,
                    Status = "Delivered"
                });
            }

            await _context.SaveChangesAsync();
            // AJAX support
            if (string.Equals(Request?.Headers["X-Requested-With"].ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new { success = true, message = "Purchase order received and inventory updated.", supplierId = po.SupplierId });
            }
            TempData["SupplierSuccess"] = "Purchase order received and inventory updated.";
            return RedirectToAction(nameof(ManageTransactions), new { id = po.SupplierId });
        }

        // POST: Suppliers/ReceiveAllPurchaseOrders
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceiveAllPurchaseOrders(int supplierId, List<int> purchaseOrderIds, List<int> deliveredQuantities)
        {
            if (purchaseOrderIds == null || deliveredQuantities == null || purchaseOrderIds.Count == 0 || deliveredQuantities.Count == 0)
            {
                TempData["SupplierError"] = "No purchase orders selected.";
                return RedirectToAction(nameof(ManageTransactions), new { id = supplierId });
            }

            var map = purchaseOrderIds.Zip(deliveredQuantities, (id, qty) => new { id, qty }).ToList();

            foreach (var item in map)
            {
                var po = await _context.PurchaseOrders.FirstOrDefaultAsync(x => x.PurchaseOrderId == item.id && x.SupplierId == supplierId && x.Status == "Ordered");
                if (po == null) continue;

                int qty = item.qty > 0 ? item.qty : po.Quantity;

                var material = await _context.Materials.FindAsync(po.MaterialId);
                if (material == null)
                    continue;

                material.Quantity += qty;
                _context.Materials.Update(material);

                po.Status = "Delivered";
                po.ReceivedQuantity = qty;
                _context.PurchaseOrders.Update(po);

                var tx = await _context.SupplierTransactions
                    .Where(t => t.SupplierId == po.SupplierId && t.MaterialId == po.MaterialId && t.Status == "Ordered")
                    .OrderByDescending(t => t.OrderDate)
                    .FirstOrDefaultAsync();
                if (tx != null)
                {
                    tx.DeliveredDate = DateTime.Now;
                    tx.ReceivedQuantity = qty;
                    tx.Status = "Delivered";
                    _context.SupplierTransactions.Update(tx);
                }
                else
                {
                    _context.SupplierTransactions.Add(new SupplierTransaction
                    {
                        SupplierId = po.SupplierId,
                        MaterialId = po.MaterialId,
                        Quantity = qty,
                        ReceivedQuantity = qty,
                        UnitPrice = po.UnitPrice,
                        OrderDate = po.CreatedAt,
                        DeliveredDate = DateTime.Now,
                        Status = "Delivered"
                    });
                }
            }

            await _context.SaveChangesAsync();
            // AJAX support
            if (string.Equals(Request?.Headers["X-Requested-With"].ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new { success = true, message = "Selected purchase orders received. Inventory updated.", supplierId });
            }
            TempData["SupplierSuccess"] = "Selected purchase orders received. Inventory updated.";
            return RedirectToAction(nameof(ManageTransactions), new { id = supplierId });
        }

        // ================== Auto-Restock ==================
        // Automatically create purchase orders for materials below a threshold
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoRestock(int reorderPointConsumable = 50, int targetLevelConsumable = 200, int reorderPointNonConsumable = 5, int targetLevelNonConsumable = 10)
        {
            var materials = await _context.Materials.ToListAsync();
            var created = new List<object>();
            var createdSupplierIds = new HashSet<int>();

            foreach (var mat in materials)
            {
                int rp = mat.IsConsumable ? reorderPointConsumable : reorderPointNonConsumable;
                int target = mat.IsConsumable ? targetLevelConsumable : targetLevelNonConsumable;
                if (mat.Quantity <= rp)
                {
                    int qtyToOrder = Math.Max(1, target - mat.Quantity);

                    // Pick the cheapest supplier
                    var bestOffer = await _context.SupplierMaterialPrices
                        .Where(p => p.MaterialId == mat.MaterialId)
                        .OrderBy(p => p.UnitPrice)
                        .FirstOrDefaultAsync();

                    if (bestOffer == null)
                        continue; // No supplier price configured, skip

                    var po = new PurchaseOrder
                    {
                        SupplierId = bestOffer.SupplierId,
                        MaterialId = mat.MaterialId,
                        Quantity = qtyToOrder,
                        UnitPrice = bestOffer.UnitPrice,
                        ScheduledDelivery = DateTime.Now.AddDays(3),
                        Status = "Ordered"
                    };
                    _context.PurchaseOrders.Add(po);

                    _context.SupplierTransactions.Add(new SupplierTransaction
                    {
                        SupplierId = bestOffer.SupplierId,
                        MaterialId = mat.MaterialId,
                        Quantity = qtyToOrder,
                        UnitPrice = bestOffer.UnitPrice,
                        OrderDate = DateTime.Now,
                        ExpectedDeliveryDate = DateTime.Now.AddDays(3),
                        Status = "Ordered"
                    });

                    created.Add(new { MaterialId = mat.MaterialId, MaterialName = mat.Name, Quantity = qtyToOrder, SupplierId = bestOffer.SupplierId, bestOffer.UnitPrice });
                    createdSupplierIds.Add(bestOffer.SupplierId);
                }
            }

            // Create exactly one ViewTransaction summary per supplier for this auto-restock run
            foreach (var supplierId in createdSupplierIds)
            {
                _context.ViewTransactions.Add(new ViewTransaction
                {
                    SupplierId = supplierId,
                    OrderDate = DateTime.Now,
                    ExpectedDate = DateTime.Now.AddDays(3),
                    Status = "Ordered"
                });
            }

            await _context.SaveChangesAsync();
            TempData["SupplierSuccess"] = created.Any() ? $"Auto-restock created {created.Count} purchase order(s)." : "No materials at or below threshold.";

            // Flow: AutoRestock -> ViewTransactions -> ManageTransactions
            if (createdSupplierIds.Any())
            {
                var firstSupplierId = createdSupplierIds.First();
                return RedirectToAction(nameof(ViewTransactions), new { id = firstSupplierId });
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
