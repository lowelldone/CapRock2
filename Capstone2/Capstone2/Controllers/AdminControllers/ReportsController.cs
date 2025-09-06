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
                var periodItem = new SalesPeriodItem
                {
                    PeriodStart = periodKey,
                    Label = LabelFor(periodKey),
                    NumberOfOrders = group.Select(p => p.OrderId).Distinct().Count(),
                    NumberOfTransactions = group.Count(),
                    GrandTotal = group.Sum(p => p.Amount)
                };

                periodsDict[periodKey] = periodItem;
            }

            var model = new SalesReportViewModel
            {
                StartDate = start,
                EndDate = end,
                GroupBy = groupBy,
                Periods = periodsDict.Values.OrderBy(p => p.PeriodStart).ToList()
            };
            model.TotalOrders = model.Periods.Sum(p => p.NumberOfOrders);
            model.TotalTransactions = model.Periods.Sum(p => p.NumberOfTransactions);
            model.GrandTotal = model.Periods.Sum(p => p.GrandTotal);

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> SalesCsv(DateTime? startDate = null, DateTime? endDate = null, string groupBy = "day")
        {
            var result = await Sales(startDate, endDate, groupBy) as ViewResult;
            var model = result?.Model as SalesReportViewModel;
            if (model == null) return NotFound();

            var sb = new StringBuilder();
            sb.AppendLine("Date,No. of Orders,No. of Transactions,Grand Total");
            foreach (var p in model.Periods)
            {
                sb.AppendLine($"{Escape(p.Label)},{p.NumberOfOrders},{p.NumberOfTransactions},{p.GrandTotal.ToString("F2", CultureInfo.InvariantCulture)}");
            }
            sb.AppendLine($"TOTAL,{model.TotalOrders},{model.TotalTransactions},{model.GrandTotal.ToString("F2", CultureInfo.InvariantCulture)}");

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"sales_{DateTime.Now:yyyyMMddHHmmss}.csv");
        }

        [HttpGet]
        public async Task<IActionResult> GetTransactions(DateTime periodStart, string groupBy = "day")
        {
            try
            {
                // Calculate the period end based on groupBy
                DateTime periodEnd = groupBy switch
                {
                    "week" => periodStart.AddDays(7),
                    "month" => periodStart.AddMonths(1),
                    _ => periodStart.AddDays(1) // day
                };

                // Get payments within the period
                var payments = await _context.Payments
                    .Include(p => p.Order)
                        .ThenInclude(o => o.Customer)
                    .Where(p => p.Date.Date >= periodStart && p.Date.Date < periodEnd)
                    .OrderBy(p => p.Date)
                    .Select(p => new
                    {
                        date = p.Date,
                        transactionNumber = p.TransactionNumber,
                        orderNumber = p.Order.OrderNumber,
                        customerName = p.Order.Customer.Name,
                        amount = p.Amount,
                        paymentType = p.PaymentType
                    })
                    .ToListAsync();

                return Json(payments);
            }
            catch (Exception ex)
            {
                return Json(new { error = "Failed to load transactions" });
            }
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
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.MenuPackage)
                .ToListAsync();

            var periods = new Dictionary<DateTime, TrendsPeriodItem>();

            foreach (var order in orders)
            {
                var key = Truncate(order.CateringDate);
                if (!periods.TryGetValue(key, out var item))
                {
                    item = new TrendsPeriodItem { PeriodStart = key, Label = LabelFor(key) };
                    periods[key] = item;
                }
                item.OrdersCount += 1;

                // Track package usage for this order
                var packageGroups = order.OrderDetails
                    .Where(od => od.Type == "Package Item" && od.MenuPackage != null)
                    .GroupBy(od => od.MenuPackageId)
                    .ToList();

                foreach (var packageGroup in packageGroups)
                {
                    var package = packageGroup.First().MenuPackage;
                    var existingPackage = item.Packages.FirstOrDefault(p => p.PackageName == package.MenuPackageName);

                    if (existingPackage != null)
                    {
                        existingPackage.OrderCount += 1;
                        existingPackage.TotalPax += order.NoOfPax;
                    }
                    else
                    {
                        item.Packages.Add(new PackageFrequency
                        {
                            PackageName = package.MenuPackageName,
                            OrderCount = 1,
                            TotalPax = order.NoOfPax
                        });
                    }
                }
            }

            // Sort packages by order count (most frequent first) for each period
            foreach (var period in periods.Values)
            {
                period.Packages = period.Packages
                    .OrderByDescending(p => p.OrderCount)
                    .ThenByDescending(p => p.TotalPax)
                    .ToList();
            }

            var model = new TrendsReportViewModel
            {
                StartDate = start,
                EndDate = end,
                GroupBy = groupBy,
                Periods = periods.Values.OrderBy(p => p.PeriodStart).ToList()
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

                // Get pull out for this order
                var pullOut = materialPullOuts.FirstOrDefault(p => p.OrderId == order.OrderId);
                if (pullOut?.Items != null)
                {
                    foreach (var pullOutItem in pullOut.Items)
                    {
                        var material = materials.FirstOrDefault(m => m.Name == pullOutItem.MaterialName);
                        if (material != null && periodsDict.ContainsKey(periodKey))
                        {
                            var period = periodsDict[periodKey];

                            // Add to total consumed
                            period.Consumed += pullOutItem.Quantity;

                            // Track by consumable vs non-consumable
                            if (material.IsConsumable)
                            {
                                period.ConsumableConsumed += pullOutItem.Quantity;
                            }
                            else
                            {
                                period.NonConsumableConsumed += pullOutItem.Quantity;
                            }

                            // Find corresponding return data for this specific material and order
                            var returnData = materialReturns.FirstOrDefault(r =>
                                r.OrderId == order.OrderId && r.MaterialName == pullOutItem.MaterialName);

                            if (returnData != null)
                            {
                                // Add returned, lost, and damaged items based on material type
                                if (material.IsConsumable)
                                {
                                    // For consumables, everything pulled out is consumed
                                    // Return data for consumables represents items that were actually returned (rare case)
                                    period.ConsumableLost += returnData.Lost;
                                    period.ConsumableDamaged += returnData.Damaged;
                                    period.Returned += returnData.Returned; // Keep for backward compatibility
                                }
                                else
                                {
                                    // For non-consumables, track returned, lost, and damaged separately
                                    period.NonConsumableReturned += returnData.Returned;
                                    period.NonConsumableLost += returnData.Lost;
                                    period.NonConsumableDamaged += returnData.Damaged;
                                    period.Returned += returnData.Returned; // Keep for backward compatibility
                                }

                                // Add to total lost and damaged for backward compatibility
                                period.Lost += returnData.Lost;
                                period.Damaged += returnData.Damaged;
                            }
                            else
                            {
                                // If no return data exists
                                if (material.IsConsumable)
                                {
                                    // For consumables with no return data, everything pulled out is consumed
                                    // No items returned, lost, or damaged (they were all consumed)
                                }
                                else
                                {
                                    // For non-consumables with no return data, assume everything was returned
                                    // This is a business rule assumption
                                    period.NonConsumableReturned += pullOutItem.Quantity;
                                    period.Returned += pullOutItem.Quantity; // Keep for backward compatibility
                                }
                            }
                        }
                    }
                }

                // Also process materials that have return data but might not have pull-out data
                var orderReturns = materialReturns.Where(r => r.OrderId == order.OrderId).ToList();
                foreach (var returnData in orderReturns)
                {
                    var material = materials.FirstOrDefault(m => m.Name == returnData.MaterialName);
                    if (material != null && periodsDict.ContainsKey(periodKey))
                    {
                        var period = periodsDict[periodKey];

                        // Only add if we haven't already processed this material from pull-out data
                        var alreadyProcessed = pullOut?.Items?.Any(i => i.MaterialName == returnData.MaterialName) ?? false;

                        if (!alreadyProcessed)
                        {
                            // This material has return data but no pull-out data
                            // Add returned, lost, and damaged items based on material type
                            if (material.IsConsumable)
                            {
                                period.ConsumableLost += returnData.Lost;
                                period.ConsumableDamaged += returnData.Damaged;
                                period.Returned += returnData.Returned; // Keep for backward compatibility
                            }
                            else
                            {
                                period.NonConsumableReturned += returnData.Returned;
                                period.NonConsumableLost += returnData.Lost;
                                period.NonConsumableDamaged += returnData.Damaged;
                                period.Returned += returnData.Returned; // Keep for backward compatibility
                            }

                            // Add to total lost and damaged for backward compatibility
                            period.Lost += returnData.Lost;
                            period.Damaged += returnData.Damaged;
                        }
                    }
                }
            }

            // Create summary
            var summary = new InventorySummary
            {
                TotalMaterials = materials.Sum(m => m.Quantity)
            };

            var model = new InventoryTrendsViewModel
            {
                StartDate = start,
                EndDate = end,
                GroupBy = groupBy,
                Periods = periodsDict.Values.OrderBy(p => p.PeriodStart).ToList(),
                Summary = summary
            };

            return View(model);
        }
    }
}


