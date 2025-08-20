using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Capstone2.Data;
using Capstone2.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Capstone2.Controllers
{
    public class ReportsController : GenericController
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // SALES REPORTS -------------------------------------------------------
        public async Task<IActionResult> Sales(DateTime? startDate = null, DateTime? endDate = null, string groupBy = "day")
        {
            var start = startDate?.Date ?? DateTime.Today.AddDays(-29).Date;
            var end = (endDate ?? DateTime.Today).Date;
            if (end < start) (start, end) = (end, start);

            // Consider payments within range (revenue by payment date)
            var payments = await _context.Payments
                .Include(p => p.Order)
                .Where(p => p.Date.Date >= start && p.Date.Date <= end)
                .OrderBy(p => p.Date)
                .ToListAsync();

            // Preload additional charges per order to know how to split base vs charges via strict allocation
            var orderIds = payments.Select(p => p.OrderId).Distinct().ToList();
            var additionalChargesByOrder = await _context.MaterialReturns
                .Where(r => orderIds.Contains(r.OrderId))
                .GroupBy(r => r.OrderId)
                .Select(g => new { OrderId = g.Key, TotalCharge = g.Sum(r => (r.Lost + r.Damaged) * r.ChargePerItem) })
                .ToDictionaryAsync(x => x.OrderId, x => (double)x.TotalCharge);

            // Load all payments for the involved orders (for correct prior allocation, including before start date)
            var paymentsAll = await _context.Payments
                .Where(p => orderIds.Contains(p.OrderId))
                .OrderBy(p => p.Date)
                .ToListAsync();
            var paymentsAllByOrder = paymentsAll
                .GroupBy(p => p.OrderId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Group by period helper
            DateTime Truncate(DateTime d)
            {
                return groupBy switch
                {
                    "week" => d.Date.AddDays(-(int)d.DayOfWeek),
                    "month" => new DateTime(d.Year, d.Month, 1),
                    _ => d.Date
                };
            }

            string LabelFor(DateTime d)
            {
                return groupBy switch
                {
                    "week" => $"Week of {Truncate(d):MMM dd, yyyy}",
                    "month" => Truncate(d).ToString("MMM yyyy"),
                    _ => Truncate(d).ToString("MMM dd, yyyy")
                };
            }

            var periodsDict = new Dictionary<DateTime, SalesPeriodItem>();

            foreach (var group in payments.GroupBy(p => Truncate(p.Date)))
            {
                var periodKey = group.Key;
                var periodItem = new SalesPeriodItem { PeriodStart = periodKey, Label = LabelFor(periodKey) };

                foreach (var byOrder in group.GroupBy(p => p.OrderId))
                {
                    var order = byOrder.First().Order;
                    var orderPaymentsChrono = byOrder.OrderBy(p => p.Date).ToList();

                    // Allocate per period only for the payments in this period bucket
                    // Compute effective base and charges still remaining before this period's payments
                    double orderBase = order.TotalPayment;
                    double orderCharges = additionalChargesByOrder.TryGetValue(order.OrderId, out var c) ? c : 0d;

                    // To allocate properly per-period, compute cumulative allocations up to (but excluding) this group
                    var priorPayments = paymentsAllByOrder.TryGetValue(order.OrderId, out var allList)
                        ? allList.Where(p => Truncate(p.Date) < periodKey).OrderBy(p => p.Date).ToList()
                        : new List<Payment>();

                    var (priorBaseAlloc, priorChargeAlloc) = AllocateBaseThenCharges(orderBase, orderCharges, priorPayments.Select(p => p.Amount));

                    // Now allocate only current group's payments starting from remaining
                    double remainingBaseBefore = Math.Max(0d, orderBase - priorBaseAlloc);
                    double remainingChargeBefore = Math.Max(0d, orderCharges - priorChargeAlloc);
                    var (currBaseAlloc, currChargeAlloc) = AllocateBaseThenCharges(remainingBaseBefore, remainingChargeBefore, orderPaymentsChrono.Select(p => p.Amount));

                    periodItem.BasePaid += currBaseAlloc;
                    periodItem.ChargesPaid += currChargeAlloc;
                }

                periodsDict[periodKey] = periodItem;
            }

            var model = new SalesReportViewModel
            {
                StartDate = start,
                EndDate = end,
                GroupBy = groupBy,
                Periods = periodsDict.Values.OrderBy(p => p.PeriodStart).ToList()
            };
            model.TotalBasePaid = model.Periods.Sum(p => p.BasePaid);
            model.TotalChargesPaid = model.Periods.Sum(p => p.ChargesPaid);

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> SalesCsv(DateTime? startDate = null, DateTime? endDate = null, string groupBy = "day")
        {
            var result = await Sales(startDate, endDate, groupBy) as ViewResult;
            var model = result?.Model as SalesReportViewModel;
            if (model == null) return NotFound();

            var sb = new StringBuilder();
            sb.AppendLine("Label,Base Paid,Charges Paid,Total Paid");
            foreach (var p in model.Periods)
            {
                sb.AppendLine($"{Escape(p.Label)},{p.BasePaid.ToString("F2", CultureInfo.InvariantCulture)},{p.ChargesPaid.ToString("F2", CultureInfo.InvariantCulture)},{p.TotalPaid.ToString("F2", CultureInfo.InvariantCulture)}");
            }
            sb.AppendLine($"TOTAL,{model.TotalBasePaid.ToString("F2", CultureInfo.InvariantCulture)},{model.TotalChargesPaid.ToString("F2", CultureInfo.InvariantCulture)},{model.GrandTotal.ToString("F2", CultureInfo.InvariantCulture)}");

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"sales_{DateTime.Now:yyyyMMddHHmmss}.csv");
        }

        private static (double baseAllocated, double chargesAllocated) AllocateBaseThenCharges(double baseAmount, double chargesAmount, IEnumerable<double> payments)
        {
            double baseAllocated = 0d;
            double chargesAllocated = 0d;
            foreach (var amount in payments)
            {
                if (baseAllocated < baseAmount)
                {
                    var need = baseAmount - baseAllocated;
                    var toBase = Math.Min(amount, need);
                    baseAllocated += toBase;
                    var remainder = amount - toBase;
                    if (remainder > 0 && baseAllocated >= baseAmount)
                    {
                        chargesAllocated += Math.Min(remainder, Math.Max(0d, chargesAmount - chargesAllocated));
                    }
                }
                else
                {
                    chargesAllocated += Math.Min(amount, Math.Max(0d, chargesAmount - chargesAllocated));
                }
            }
            return (baseAllocated, chargesAllocated);
        }

        private static string Escape(string s)
        {
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            {
                return '"' + s.Replace("\"", "\"\"") + '"';
            }
            return s;
        }

        // TRENDS --------------------------------------------------------------
        public async Task<IActionResult> Trends(DateTime? startDate = null, DateTime? endDate = null, string groupBy = "day")
        {
            var start = startDate?.Date ?? DateTime.Today.AddDays(-29).Date;
            var end = (endDate ?? DateTime.Today).Date;
            if (end < start) (start, end) = (end, start);

            DateTime Truncate(DateTime d)
            {
                return groupBy switch
                {
                    "week" => d.Date.AddDays(-(int)d.DayOfWeek),
                    "month" => new DateTime(d.Year, d.Month, 1),
                    _ => d.Date
                };
            }

            string LabelFor(DateTime d)
            {
                return groupBy switch
                {
                    "week" => $"Week of {Truncate(d):MMM dd, yyyy}",
                    "month" => Truncate(d).ToString("MMM yyyy"),
                    _ => Truncate(d).ToString("MMM dd, yyyy")
                };
            }

            var orders = await _context.Orders
                .Where(o => !o.isDeleted)
                .Where(o => o.CateringDate.Date >= start && o.CateringDate.Date <= end)
                .Include(o => o.Customer)
                .ToListAsync();

            var payments = await _context.Payments
                .Where(p => p.Date.Date >= start && p.Date.Date <= end)
                .ToListAsync();

            var additionalChargesByOrder = await _context.MaterialReturns
                .GroupBy(r => r.OrderId)
                .Select(g => new { OrderId = g.Key, TotalCharge = g.Sum(r => (r.Lost + r.Damaged) * r.ChargePerItem) })
                .ToDictionaryAsync(x => x.OrderId, x => (double)x.TotalCharge);

            var periods = new Dictionary<DateTime, TrendsPeriodItem>();

            foreach (var o in orders)
            {
                var key = Truncate(o.CateringDate);
                if (!periods.TryGetValue(key, out var item))
                {
                    item = new TrendsPeriodItem { PeriodStart = key, Label = LabelFor(key) };
                    periods[key] = item;
                }
                item.OrdersCount += 1;
                item.PaxTotal += o.NoOfPax;
            }

            foreach (var g in payments.GroupBy(p => Truncate(p.Date)))
            {
                if (!periods.TryGetValue(g.Key, out var item))
                {
                    item = new TrendsPeriodItem { PeriodStart = g.Key, Label = LabelFor(g.Key) };
                    periods[g.Key] = item;
                }
                item.RevenueTotal += g.Sum(x => x.Amount);
            }

            var model = new TrendsReportViewModel
            {
                StartDate = start,
                EndDate = end,
                GroupBy = groupBy,
                Periods = periods.Values.OrderBy(p => p.PeriodStart).ToList(),
                PendingCount = orders.Count(o => o.Status == "Pending"),
                AcceptedCount = orders.Count(o => o.Status == "Accepted"),
                OngoingCount = orders.Count(o => o.Status == "Ongoing"),
                CompletedCount = orders.Count(o => o.Status == "Completed"),
                CancelledCount = orders.Count(o => o.Status == "Cancelled"),
            };

            return View(model);
        }

        // PREFERENCES ---------------------------------------------------------
        public async Task<IActionResult> Preferences(DateTime? startDate = null, DateTime? endDate = null)
        {
            var start = startDate?.Date ?? DateTime.Today.AddDays(-29).Date;
            var end = (endDate ?? DateTime.Today).Date;
            if (end < start) (start, end) = (end, start);

            var orders = await _context.Orders
                .Where(o => !o.isDeleted)
                .Where(o => o.CateringDate.Date >= start && o.CateringDate.Date <= end)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Menu)
                .Include(o => o.Customer)
                .ToListAsync();

            var topMenusByQty = orders
                .SelectMany(o => o.OrderDetails)
                .GroupBy(od => od.Menu?.Name ?? od.Name)
                .Select(g => new PreferencesItem { Name = g.Key, Quantity = g.Sum(x => x.Quantity), Revenue = g.Sum(x => (x.Menu?.Price ?? 0) * x.Quantity) })
                .OrderByDescending(x => x.Quantity)
                .Take(10)
                .ToList();

            var topMenusByRevenue = orders
                .SelectMany(o => o.OrderDetails)
                .GroupBy(od => od.Menu?.Name ?? od.Name)
                .Select(g => new PreferencesItem { Name = g.Key, Quantity = g.Sum(x => x.Quantity), Revenue = g.Sum(x => (x.Menu?.Price ?? 0) * x.Quantity) })
                .OrderByDescending(x => x.Revenue)
                .Take(10)
                .ToList();

            var topCategoriesByQty = orders
                .SelectMany(o => o.OrderDetails)
                .GroupBy(od => od.Menu?.Category ?? "Uncategorized")
                .Select(g => new PreferencesItem { Name = g.Key, Quantity = g.Sum(x => x.Quantity), Revenue = g.Sum(x => (x.Menu?.Price ?? 0) * x.Quantity) })
                .OrderByDescending(x => x.Quantity)
                .Take(10)
                .ToList();

            var topCategoriesByRevenue = orders
                .SelectMany(o => o.OrderDetails)
                .GroupBy(od => od.Menu?.Category ?? "Uncategorized")
                .Select(g => new PreferencesItem { Name = g.Key, Quantity = g.Sum(x => x.Quantity), Revenue = g.Sum(x => (x.Menu?.Price ?? 0) * x.Quantity) })
                .OrderByDescending(x => x.Revenue)
                .Take(10)
                .ToList();

            var avgPaxByOccasion = orders
                .GroupBy(o => o.Occasion)
                .Select(g => new AveragePaxItem { Name = g.Key, AveragePax = g.Average(x => x.NoOfPax) })
                .OrderByDescending(x => x.AveragePax)
                .ToList();

            var avgPaxByVenue = orders
                .GroupBy(o => o.Venue)
                .Select(g => new AveragePaxItem { Name = g.Key, AveragePax = g.Average(x => x.NoOfPax) })
                .OrderByDescending(x => x.AveragePax)
                .ToList();

            var repeatCustomers = orders
                .GroupBy(o => o.CustomerID)
                .Select(g => new RepeatCustomerItem
                {
                    CustomerId = g.Key,
                    CustomerName = g.First().Customer?.Name ?? $"Customer {g.Key}",
                    OrdersCount = g.Count()
                })
                .Where(x => x.OrdersCount >= 2)
                .OrderByDescending(x => x.OrdersCount)
                .ToList();

            var model = new PreferencesReportViewModel
            {
                StartDate = start,
                EndDate = end,
                TopMenusByQuantity = topMenusByQty,
                TopMenusByRevenue = topMenusByRevenue,
                TopCategoriesByQuantity = topCategoriesByQty,
                TopCategoriesByRevenue = topCategoriesByRevenue,
                AveragePaxByOccasion = avgPaxByOccasion,
                AveragePaxByVenue = avgPaxByVenue,
                RepeatCustomers = repeatCustomers
            };

            return View(model);
        }

        // INVENTORY TRENDS ---------------------------------------------------
        public async Task<IActionResult> InventoryTrends(DateTime? startDate = null, DateTime? endDate = null, string groupBy = "month")
        {
            var start = startDate?.Date ?? DateTime.Today.AddMonths(-11).Date;
            var end = (endDate ?? DateTime.Today).Date;
            if (end < start) (start, end) = (end, start);

            // Helper functions for period grouping
            DateTime Truncate(DateTime d)
            {
                return groupBy switch
                {
                    "day" => d.Date,
                    "week" => d.Date.AddDays(-(int)d.DayOfWeek),
                    "quarter" => new DateTime(d.Year, ((d.Month - 1) / 3) * 3 + 1, 1),
                    "year" => new DateTime(d.Year, 1, 1),
                    _ => new DateTime(d.Year, d.Month, 1) // month
                };
            }

            string LabelFor(DateTime d)
            {
                return groupBy switch
                {
                    "day" => d.ToString("MMM dd, yyyy"),
                    "week" => $"Week of {d:MMM dd, yyyy}",
                    "quarter" => $"Q{(d.Month - 1) / 3 + 1} {d.Year}",
                    "year" => d.Year.ToString(),
                    _ => d.ToString("MMM yyyy") // month
                };
            }

            // Get all materials
            var materials = await _context.Materials.ToListAsync();
            var materialsDict = materials.ToDictionary(m => m.MaterialId, m => m);

            // Get all orders in the date range
            var orders = await _context.Orders
                .Where(o => !o.isDeleted && o.Status == "Completed")
                .Where(o => o.CateringDate.Date >= start && o.CateringDate.Date <= end)
                .ToListAsync();

            // Get all material pull outs
            var materialPullOuts = await _context.MaterialPullOuts
                .Include(p => p.Items)
                .Where(p => orders.Select(o => o.OrderId).Contains(p.OrderId))
                .ToListAsync();

            // Get all material returns - use Set<T>() approach as it seems to work in other controllers
            var materialReturns = await _context.Set<MaterialReturn>()
                .Where(r => orders.Select(o => o.OrderId).Contains(r.OrderId))
                .ToListAsync();





            // Create periods dictionary
            var periodsDict = new Dictionary<DateTime, ConsumptionPeriod>();
            var current = start;
            while (current <= end)
            {
                var periodKey = Truncate(current);
                if (!periodsDict.ContainsKey(periodKey))
                {
                    periodsDict[periodKey] = new ConsumptionPeriod
                    {
                        PeriodStart = periodKey,
                        Label = LabelFor(periodKey)
                    };
                }
                current = current.AddDays(1);
            }

            // Process material consumption by period
            foreach (var order in orders)
            {
                var periodKey = Truncate(order.CateringDate);
                if (periodsDict.ContainsKey(periodKey))
                {
                    periodsDict[periodKey].OrderCount++;
                }

                // Get pull out for this order
                var pullOut = materialPullOuts.FirstOrDefault(p => p.OrderId == order.OrderId);
                if (pullOut?.Items != null)
                {
                    foreach (var pullOutItem in pullOut.Items)
                    {
                        var material = materials.FirstOrDefault(m => m.Name == pullOutItem.MaterialName);
                        if (material != null)
                        {
                            if (periodsDict.ContainsKey(periodKey))
                            {
                                var period = periodsDict[periodKey];
                                period.Consumed += pullOutItem.Quantity;

                                // Find corresponding return data for this specific material and order
                                var returnData = materialReturns.FirstOrDefault(r =>
                                    r.OrderId == order.OrderId && r.MaterialName == pullOutItem.MaterialName);

                                if (returnData != null)
                                {
                                    // Add returned, lost, and damaged items
                                    period.Returned += returnData.Returned;
                                    period.Lost += returnData.Lost;
                                    period.Damaged += returnData.Damaged;
                                }
                                else
                                {
                                    // If no return data exists, check if it's a consumable
                                    if (material.IsConsumable)
                                    {
                                        // For consumables, everything pulled out is consumed
                                        period.Returned = 0;
                                        period.Lost = 0;
                                        period.Damaged = 0;
                                    }
                                    // For non-consumables, if no return data, assume everything was returned
                                    // (this might need adjustment based on your business logic)
                                }
                            }
                        }
                    }
                }
            }

            // Create material trends
            var materialTrends = new List<MaterialConsumptionTrend>();
            foreach (var material in materials)
            {
                var trend = new MaterialConsumptionTrend
                {
                    MaterialId = material.MaterialId,
                    MaterialName = material.Name,
                    IsConsumable = material.IsConsumable
                };

                // Calculate totals and populate consumption by period
                foreach (var period in periodsDict.Values.OrderBy(p => p.PeriodStart))
                {
                    var materialPeriod = new ConsumptionPeriod
                    {
                        PeriodStart = period.PeriodStart,
                        Label = period.Label,
                        OrderCount = period.OrderCount
                    };

                    // Get material-specific data for this period
                    var periodOrders = orders.Where(o => Truncate(o.CateringDate) == period.PeriodStart).ToList();

                    foreach (var order in periodOrders)
                    {
                        var pullOut = materialPullOuts.FirstOrDefault(p => p.OrderId == order.OrderId);
                        var pullOutItem = pullOut?.Items?.FirstOrDefault(i => i.MaterialName == material.Name);

                        if (pullOutItem != null)
                        {
                            materialPeriod.Consumed += pullOutItem.Quantity;

                            // Find return data for this material and order
                            var returnData = materialReturns.FirstOrDefault(r =>
                                r.OrderId == order.OrderId && r.MaterialName == material.Name);

                            if (returnData != null)
                            {
                                materialPeriod.Returned += returnData.Returned;
                                materialPeriod.Lost += returnData.Lost;
                                materialPeriod.Damaged += returnData.Damaged;
                            }
                            else if (material.IsConsumable)
                            {
                                // For consumables with no return data, everything is consumed
                                materialPeriod.Returned = 0;
                                materialPeriod.Lost = 0;
                                materialPeriod.Damaged = 0;
                            }
                            // For non-consumables with no return data, assume everything was returned
                        }
                    }

                    trend.ConsumptionByPeriod.Add(materialPeriod);
                }

                // Calculate totals
                trend.TotalConsumption = trend.ConsumptionByPeriod.Sum(p => p.Consumed);
                trend.TotalLoss = trend.ConsumptionByPeriod.Sum(p => p.Lost);
                trend.TotalDamage = trend.ConsumptionByPeriod.Sum(p => p.Damaged);
                trend.OrderCount = trend.ConsumptionByPeriod.Sum(p => p.OrderCount);
                trend.AverageConsumptionPerOrder = trend.OrderCount > 0 ? trend.TotalConsumption / trend.OrderCount : 0;

                materialTrends.Add(trend);
            }



            // Create summary
            var summary = new InventorySummary
            {
                TotalMaterials = materials.Count,
                ConsumableMaterials = materials.Count(m => m.IsConsumable),
                NonConsumableMaterials = materials.Count(m => !m.IsConsumable),
                TotalConsumption = materialTrends.Sum(m => m.TotalConsumption),
                TotalLoss = materialTrends.Sum(m => m.TotalLoss),
                TotalDamage = materialTrends.Sum(m => m.TotalDamage),
                TotalOrders = orders.Count
            };

            var model = new InventoryTrendsViewModel
            {
                StartDate = start,
                EndDate = end,
                GroupBy = groupBy,
                MaterialTrends = materialTrends.OrderByDescending(m => m.TotalConsumption).ToList(),
                Periods = periodsDict.Values.OrderBy(p => p.PeriodStart).ToList(),
                Summary = summary
            };

            return View(model);
        }
    }
}


