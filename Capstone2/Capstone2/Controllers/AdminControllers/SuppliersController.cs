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

        private async Task<string> GenerateNextTransactionOrderNumberAsync(int supplierId)
        {
            var today = DateTime.Now.Date;
            var prefix = today.ToString("yyyyMMdd");

            var existingNumbers = await _context.ViewTransactions
                .Where(vt => vt.SupplierId == supplierId && vt.TransactionOrderNumber != null && vt.TransactionOrderNumber.StartsWith(prefix))
                .Select(vt => vt.TransactionOrderNumber!)
                .ToListAsync();

            int maxSeq = 0;
            foreach (var num in existingNumbers)
            {
                if (num.Length >= 10)
                {
                    var seqStr = num.Substring(9);
                    if (int.TryParse(seqStr, out int parsed))
                    {
                        if (parsed > maxSeq) maxSeq = parsed;
                    }
                }
            }

            int nextSeq = maxSeq + 1;
            return $"{prefix}-{nextSeq:000}";
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

        // GET: Suppliers/ManageTransactions/ (view)
        [HttpGet]
        public IActionResult ManageTransactions(int id, string view = "po", int? vtId = null)
        {
            ViewBag.SupplierId = id;
            ViewBag.ViewMode = (view ?? "po").ToLower();
            ViewBag.ViewTransactionId = vtId;
            return View();
        }

        // GET: Suppliers/PurchaseOrderHistory/5
         [HttpGet]
         public async Task<IActionResult> PurchaseOrderHistory(int id, int vtId)
         {
             var supplier = await _context.Suppliers.FindAsync(id);
             if (supplier == null) return NotFound();
 
             ViewBag.SupplierId = id;
             ViewBag.SupplierName = supplier.CompanyName;
             ViewBag.ViewTransactionId = vtId;
              return View();
         }

    // GET: Suppliers/ViewTransactions (shows only Ordered)
    [HttpGet]
        public async Task<IActionResult> ViewTransactions(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();

            var viewTransactions = await _context.ViewTransactions
                .Where(v => v.SupplierId == id && v.Status == "Ordered")
                .OrderByDescending(v => v.OrderDate)
                .ToListAsync();

            // No need to map per-PO numbers; use ViewTransaction.TransactionOrderNumber

            ViewBag.SupplierId = id;
            ViewBag.SupplierName = supplier.CompanyName;
            return View(viewTransactions);
        }

        // GET: Suppliers/TransactionHistory/ (shows Delivered with date range filter)
        [HttpGet]
        public async Task<IActionResult> TransactionHistory(int id, DateTime? from, DateTime? to)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();

            var query = _context.ViewTransactions
                 .Where(v => v.SupplierId == id && v.Status == "Delivered")
                 .AsQueryable();
            
            if (from.HasValue)
            {
                var fromDate = from.Value.Date;
                query = query.Where(v => v.OrderDate >= fromDate);
            }
            if (to.HasValue)
                {
                var toDate = to.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(v => v.OrderDate <= toDate);
            }
            var delivered = await query
                .OrderByDescending(v => v.OrderDate)
                .ToListAsync();

            ViewBag.SupplierId = id;
            ViewBag.SupplierName = supplier.CompanyName;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
                         return View(delivered);
                     }
 
         // GET: Suppliers/Materials/5
         [HttpGet]
         public async Task<IActionResult> Materials(int id)
         {
             var supplier = await _context.Suppliers.FindAsync(id);
             if (supplier == null) return NotFound();
 
             var materials = await _context.Materials
                .Select(m => new { m.MaterialId, m.Name, m.IsConsumable, m.Quantity })
                .ToListAsync();

            return Ok(new { Supplier = supplier, Materials = materials });
        }

        // ================== Transactions & Deliveries ==================

        // GET: Suppliers/Transactions/5
        [HttpGet]
        public async Task<IActionResult> Transactions(int id, int? vtId)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();

            var txQuery = _context.SupplierTransactions
                .Include(t => t.Material)
                .Where(t => t.SupplierId == id)
                .AsQueryable();

            if (vtId.HasValue)
            {
                txQuery = txQuery.Where(t => t.ViewTransactionId == vtId.Value);
            }

            var transactions = await txQuery
                .OrderByDescending(t => t.OrderDate)
                .Select(t => new
                {
                    t.SupplierTransactionId,
                    t.MaterialId,
                    MaterialName = t.Material.Name,
                    t.Quantity,
                    t.ReceivedQuantity,
                    t.OrderDate,
                    t.DeliveredDate,
                    t.Status
                })
                .ToListAsync();

            return Ok(new { Supplier = supplier, Transactions = transactions });
        }

        // GET: Suppliers/PurchaseOrders/5
        [HttpGet]
        public async Task<IActionResult> PurchaseOrders(int id, int? vtId)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();

            var query = _context.PurchaseOrders
                .Include(po => po.Material)
                .Where(po => po.SupplierId == id)
                .OrderByDescending(po => po.CreatedAt)
                .AsQueryable();

            if (vtId.HasValue)
            {
                query = query.Where(po => po.ViewTransactionId == vtId.Value);
            }

            var pos = await query.Select(po => new
            {
                po.PurchaseOrderId,
                po.MaterialId,
                MaterialName = po.Material.Name,
                po.Quantity,
                po.ReceivedQuantity,
                po.CreatedAt,
                DeliveredDate = _context.SupplierTransactions
                     .Where(t => t.SupplierId == po.SupplierId && t.MaterialId == po.MaterialId && t.ViewTransactionId == po.ViewTransactionId && t.Status == "Delivered")
                     .OrderByDescending(t => t.DeliveredDate)
                     .Select(t => (DateTime?)t.DeliveredDate)
                     .FirstOrDefault(),
                po.Status
            }).ToListAsync();

            return Ok(new { Supplier = supplier, PurchaseOrders = pos });
        }

        // POST: Suppliers/CreatePurchaseOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePurchaseOrder(int supplierId, int materialId, int quantity)
        {
            if (quantity <= 0)
            {
                TempData["SupplierError"] = "Quantity must be greater than zero.";
                return RedirectToAction(nameof(ManageTransactions), new { id = supplierId });
            }

            var supplier = await _context.Suppliers.FindAsync(supplierId);
            var material = await _context.Materials.FindAsync(materialId);
            if (supplier == null || material == null) return NotFound();

            var poNumber = await GenerateNextTransactionOrderNumberAsync(supplierId);
            // Create a ViewTransaction for this purchase order
            var viewTransaction = new ViewTransaction
            {
                SupplierId = supplierId,
                OrderDate = DateTime.Now,
                Status = "Ordered",
                TransactionOrderNumber = poNumber
            };
            _context.ViewTransactions.Add(viewTransaction);

            var po = new PurchaseOrder
            {
                SupplierId = supplierId,
                MaterialId = materialId,
                Quantity = quantity,
                Status = "Ordered",
                ViewTransaction = viewTransaction
            };
            _context.PurchaseOrders.Add(po);

            // Create pending transaction record (to be completed when delivered)
            var tx = new SupplierTransaction
            {
                SupplierId = supplierId,
                MaterialId = materialId,
                Quantity = quantity,
                OrderDate = DateTime.Now,
                Status = "Ordered",
                ViewTransaction = viewTransaction
            };
            _context.SupplierTransactions.Add(tx);

            await _context.SaveChangesAsync();
            TempData["SupplierSuccess"] = "Purchase order created.";
            return RedirectToAction(nameof(ManageTransactions), new { id = supplierId, vtId = viewTransaction.ViewTransactionId, view = "po" });
        }

        // POST: Suppliers/CreatePurchaseOrderBatch
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePurchaseOrderBatch(int supplierId, List<int> materialIds, List<int> quantities)
        {
            if (materialIds == null || quantities == null || materialIds.Count == 0 || quantities.Count == 0 || materialIds.Count != quantities.Count)
            {
                TempData["SupplierError"] = "Please add at least one item with a valid quantity.";
                return RedirectToAction(nameof(ManageTransactions), new { id = supplierId });
            }

            var supplier = await _context.Suppliers.FindAsync(supplierId);
            if (supplier == null)
                return NotFound();

            // Build valid items list (materialId > 0 and quantity > 0)
            var items = materialIds
                .Select((matId, idx) => new { MaterialId = matId, Quantity = (quantities != null && quantities.Count > idx) ? quantities[idx] : 0 })
                .Where(x => x.MaterialId > 0 && x.Quantity > 0)
                .Select(x => new { x.MaterialId, x.Quantity })
                .ToList();

            if (!items.Any())
            {
                TempData["SupplierError"] = "No valid items to create a purchase order.";
                return RedirectToAction(nameof(ManageTransactions), new { id = supplierId });
            }

            // Create a single ViewTransaction for this batch
            var poNumberBatch = await GenerateNextTransactionOrderNumberAsync(supplierId);
            var viewTransaction = new ViewTransaction
            {
                SupplierId = supplierId,
                OrderDate = DateTime.Now,
                Status = "Ordered",
                TransactionOrderNumber = poNumberBatch
            };
            _context.ViewTransactions.Add(viewTransaction);

            foreach (var item in items)
            {
                var material = await _context.Materials.FindAsync(item.MaterialId);
                if (material == null)
                    continue;

                var po = new PurchaseOrder
                {
                    SupplierId = supplierId,
                    MaterialId = item.MaterialId,
                    Quantity = item.Quantity,
                    Status = "Ordered",
                    ViewTransaction = viewTransaction
                };
                _context.PurchaseOrders.Add(po);

                var tx = new SupplierTransaction
                {
                    SupplierId = supplierId,
                    MaterialId = item.MaterialId,
                    Quantity = item.Quantity,
                    OrderDate = DateTime.Now,
                    Status = "Ordered",
                    ViewTransaction = viewTransaction
                };
                _context.SupplierTransactions.Add(tx);
            }

            await _context.SaveChangesAsync();
            TempData["SupplierSuccess"] = "Purchase order(s) created.";
            return RedirectToAction(nameof(ManageTransactions), new { id = supplierId, vtId = viewTransaction.ViewTransactionId, view = "po" });
        }

        // POST: Suppliers/ReceivePurchaseOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceivePurchaseOrder(int purchaseOrderId, int? deliveredQuantity)
        {
            var po = await _context.PurchaseOrders.FindAsync(purchaseOrderId);
            if (po == null) return NotFound();

            int qty = deliveredQuantity.HasValue && deliveredQuantity.Value > 0 ? deliveredQuantity.Value : po.Quantity;

            // Skip auto restock inventory update
            var material = await _context.Materials.FindAsync(po.MaterialId);
            if (material == null) return NotFound();

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
                if (!tx.ViewTransactionId.HasValue && po.ViewTransactionId.HasValue)
                {
                    tx.ViewTransactionId = po.ViewTransactionId;
                }
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
                    OrderDate = po.CreatedAt,
                    DeliveredDate = DateTime.Now,
                    Status = "Delivered",
                    ViewTransactionId = po.ViewTransactionId
                });
            }

            await _context.SaveChangesAsync();

            // After saving PO updates, only mark linked ViewTransaction as delivered
            // when all POs under the same ViewTransaction are delivered
            if (po.ViewTransactionId.HasValue)
            {
                int vtId = po.ViewTransactionId.Value;
                bool anyOpen = await _context.PurchaseOrders
                    .AnyAsync(x => x.ViewTransactionId == vtId && x.Status != "Delivered");
                if (!anyOpen)
                {
                    var vt = await _context.ViewTransactions.FindAsync(vtId);
                    if (vt != null && vt.Status != "Delivered")
                    {
                        vt.Status = "Delivered";
                        _context.ViewTransactions.Update(vt);
                        await _context.SaveChangesAsync();
                    }
                }
            }
            // AJAX support
            if (string.Equals(Request?.Headers["X-Requested-With"].ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new { success = true, message = "Purchase order received.", supplierId = po.SupplierId });
            }
            TempData["SupplierSuccess"] = "Purchase order received.";
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

            var impactedViewTransactionIds = new HashSet<int>();
            foreach (var item in map)
            {
                var po = await _context.PurchaseOrders.FirstOrDefaultAsync(x => x.PurchaseOrderId == item.id && x.SupplierId == supplierId && x.Status == "Ordered");
                if (po == null) continue;

                int qty = item.qty > 0 ? item.qty : po.Quantity;

                // Skip auto restock inventory update
                var material = await _context.Materials.FindAsync(po.MaterialId);
                if (material == null)
                    continue;

                po.Status = "Delivered";
                po.ReceivedQuantity = qty;
                _context.PurchaseOrders.Update(po);

                if (po.ViewTransactionId.HasValue)
                {
                    impactedViewTransactionIds.Add(po.ViewTransactionId.Value);
                }

                var tx = await _context.SupplierTransactions
                    .Where(t => t.SupplierId == po.SupplierId && t.MaterialId == po.MaterialId && t.Status == "Ordered")
                    .OrderByDescending(t => t.OrderDate)
                    .FirstOrDefaultAsync();
                if (tx != null)
                {
                    tx.DeliveredDate = DateTime.Now;
                    tx.ReceivedQuantity = qty;
                    tx.Status = "Delivered";
                    if (!tx.ViewTransactionId.HasValue && po.ViewTransactionId.HasValue)
                    {
                        tx.ViewTransactionId = po.ViewTransactionId;
                    }
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
                        OrderDate = po.CreatedAt,
                        DeliveredDate = DateTime.Now,
                        Status = "Delivered",
                        ViewTransactionId = po.ViewTransactionId
                    });
                }

            }

            await _context.SaveChangesAsync();

            // After saving PO updates, only mark impacted ViewTransactions as delivered
            // when all POs under the same ViewTransaction are delivered
            foreach (var vtId in impactedViewTransactionIds)
            {
                bool anyOpen = await _context.PurchaseOrders
                    .AnyAsync(x => x.ViewTransactionId == vtId && x.Status != "Delivered");
                if (!anyOpen)
                {
                    var vt = await _context.ViewTransactions.FindAsync(vtId);
                    if (vt != null && vt.Status != "Delivered")
                    {
                        vt.Status = "Delivered";
                        _context.ViewTransactions.Update(vt);
                    }
                }
            }
            if (impactedViewTransactionIds.Count > 0)
            {
                await _context.SaveChangesAsync();
            }
            // AJAX support
            if (string.Equals(Request?.Headers["X-Requested-With"].ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new { success = true, message = "Selected purchase orders received.", supplierId });
            }
            TempData["SupplierSuccess"] = "Selected purchase orders received.";
            return RedirectToAction(nameof(ManageTransactions), new { id = supplierId });
        }
    }
}
