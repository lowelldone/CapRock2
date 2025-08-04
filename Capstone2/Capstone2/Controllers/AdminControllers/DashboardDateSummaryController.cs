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
    public class DashboardDateSummaryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardDateSummaryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: DashboardDateSummary
        public async Task<IActionResult> Index(DateTime? startDate = null, DateTime? endDate = null)
        {
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

            var ordersInRange = await _context.Orders
                .Include(o => o.Customer)
                .Where(o => o.CateringDate.Date >= start.Date && o.CateringDate.Date <= end.Date)
                .OrderBy(o => o.CateringDate)
                .ToListAsync();

            var dateSummary = ordersInRange
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
                DateSummary = dateSummary
            };

            return View(viewModel);
        }
    }
}