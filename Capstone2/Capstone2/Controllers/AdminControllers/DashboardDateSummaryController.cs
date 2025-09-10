using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;
using NuGet.Packaging.Signing;

namespace Capstone2.Controllers.AdminControllers
{
    public class DashboardDateSummaryController : GenericController
    {
        private readonly ApplicationDbContext _context;

        public DashboardDateSummaryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: DashboardDateSummary
        public async Task<IActionResult> Index(DateTime? startDate = null, DateTime? endDate = null)
        {
            var role = HttpContext.Session.GetString("Role");
            ViewBag.IsAdmin = role == "ADMIN";

            // If no dates provided, default to current month
            if (!startDate.HasValue && !endDate.HasValue)
            {
                var today = DateTime.Today;
                startDate = new DateTime(today.Year, today.Month, 1);
                endDate = startDate.Value.AddMonths(1).AddDays(-1);
            }
            else if (startDate.HasValue && !endDate.HasValue)
            {
                // If only start date provided, set end date to end of that month
                endDate = startDate.Value.AddMonths(1).AddDays(-1);
            }
            else if (!startDate.HasValue && endDate.HasValue)
            {
                // If only end date provided, set start date to beginning of that month
                startDate = new DateTime(endDate.Value.Year, endDate.Value.Month, 1);
            }

            var start = startDate.Value;
            var end = endDate.Value;

            // Get all orders for summary statistics and orders by date (across all months)
            var allOrders = await _context.Orders
                .Include(o => o.Customer)
                .Where(o => o.Status != "Completed" && !o.isDeleted && !o.Customer.isDeleted)
                .OrderBy(o => o.CateringDate)
                .ToListAsync();

            // Get filtered orders for calendar view (only selected date range)
            var ordersInRange = allOrders
                .Where(o => o.CateringDate.Date >= start.Date && o.CateringDate.Date <= end.Date)
                .ToList();

            // Date summary for filtered range (calendar view)
            var dateSummaryFiltered = ordersInRange
                .GroupBy(o => o.CateringDate.Date)
                .Select(g => new DateSummaryViewModel
                {
                    Date = g.Key,
                    TotalPax = g.Sum(o => o.NoOfPax),
                    HasLargeOrder = g.Any(o => o.NoOfPax >= 701 && o.NoOfPax <= 1500),
                    OrderCount = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            // Date summary for all orders (summary statistics and orders by date)
            var dateSummaryAll = allOrders
                .GroupBy(o => o.CateringDate.Date)
                .Select(g => new DateSummaryViewModel
                {
                    Date = g.Key,
                    TotalPax = g.Sum(o => o.NoOfPax),
                    HasLargeOrder = g.Any(o => o.NoOfPax >= 701 && o.NoOfPax <= 1500),
                    OrderCount = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            var viewModel = new DateSummaryPageViewModel
            {
                StartDate = start,
                EndDate = end,
                DateSummary = dateSummaryFiltered, // For calendar view
                AllDateSummary = dateSummaryAll // For summary statistics and orders by date
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> RecentLogs(DateTime? since = null, int take = 5)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "ADMIN")
            {
                return Forbid();
            }

            var baseQuery = _context.AuditLogs
                .Where(l => l.Action != "Logout");

            var latestItems = await baseQuery
                .OrderByDescending(l => l.Timestamp)
                .Take(Math.Max(1, Math.Min(20, take)))
                .Select(l => new
                {
                    l.AuditLogId,
                    l.Timestamp,
                    l.Username,
                    l.Role,
                    l.Action,
                    l.OrderNumber
                })
                .ToListAsync();

            int newCount = 0;
            if (since.HasValue)
            {
                var s = since.Value;
                newCount = await baseQuery.CountAsync(l => l.Timestamp > s);
            }

            var latestUtc = latestItems.FirstOrDefault()?.Timestamp;

            return Ok(new
            {
                items = latestItems,
                newCount,
                latestUtc
            });
        }

        [HttpGet]
        public async Task<IActionResult> RecentOrders(DateTime? since = null, int take = 5)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "ADMIN")
            {
                return Forbid();
            }

            var baseQuery = _context.Orders
                .Include(o => o.Customer)
                .Where(o => !o.isDeleted && !o.Customer.isDeleted);

            if (since.HasValue)
            {
                var s = since.Value;
                baseQuery = baseQuery.Where(o => o.OrderDate > s);
            }

            var latestItems = await baseQuery
                .OrderByDescending(o => o.OrderDate)
                .Take(Math.Max(1, Math.Min(20, take)))
                .Select(o => new
                {
                    o.OrderId,
                    o.CustomerID,
                    o.OrderNumber,
                    CustomerName = o.Customer.Name,
                    Timestamp = o.OrderDate,
                    o.CateringDate,
                    o.NoOfPax
                })
                .ToListAsync();

            int newCount = 0;
            if (since.HasValue)
            {
                var s = since.Value;
                newCount = await _context.Orders
                    .Where(o => !o.isDeleted && o.OrderDate > s)
                    .CountAsync();
            }

            var latestUtc = latestItems.FirstOrDefault()?.Timestamp;

            return Ok(new
            {
                items = latestItems,
                newCount,
                latestUtc
            });
        }

    }
}