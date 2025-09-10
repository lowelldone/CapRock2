using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Controllers;

namespace Capstone2.Controllers.AdminControllers
{
    public class AuditLogsController : GenericController
    {
        private readonly ApplicationDbContext _context;

        public AuditLogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Admin/AuditLogs
        public async Task<IActionResult> Index(string? role = null, string? orderNumber = null, string? username = null,
            DateTime? filterDate = null, int? tzOffset = null, int page = 1, int pageSize = 50, int? highlightId = null)
        {
            var currentRole = HttpContext.Session.GetString("Role");
            if (currentRole != "ADMIN")
                return Forbid();

            var query = _context.AuditLogs.AsQueryable();

            // Filter out logout logs
            query = query.Where(l => l.Action != "Logout");

            // Only apply role filter if a specific role is selected
            if (!string.IsNullOrWhiteSpace(role))
                query = query.Where(l => l.Role == role);
            if (!string.IsNullOrWhiteSpace(orderNumber))
                query = query.Where(l => l.OrderNumber == orderNumber);
            if (!string.IsNullOrWhiteSpace(username))
                query = query.Where(l => l.Username == username);

            // Add single date filtering
            if (filterDate.HasValue)
            {
                // Interpret the provided date as LOCAL (browser) midnight and convert to UTC using provided offset (in minutes)
                // tzOffset follows JS Date.getTimezoneOffset(): minutes between UTC and local time (UTC - Local)
                var offsetMinutes = tzOffset ?? 0;
                var localDate = DateTime.SpecifyKind(filterDate.Value.Date, DateTimeKind.Unspecified);
                var startUtc = localDate.AddMinutes(offsetMinutes);
                var endUtc = startUtc.AddDays(1).AddTicks(-1);

                query = query.Where(l => l.Timestamp >= startUtc && l.Timestamp <= endUtc);
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();


            // Map HeadWaiterId -> HeadWaiter Full Name for display (used when details contain "head waiter <id>")
            var headWaiters = await _context.HeadWaiters
                             .Include(h => h.User)
                             .Where(h => h.isActive)
                             .ToListAsync();
            var headWaiterNames = headWaiters.ToDictionary(
            h => h.HeadWaiterId,
            h => h.User != null ? ($"{h.User.FirstName} {h.User.LastName}") : ($"HeadWaiter #{h.HeadWaiterId}")
                         );
            ViewBag.HeadWaiterNames = headWaiterNames;

            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.FilterDate = filterDate;
            ViewBag.SelectedRole = role; // Pass selected role to view for filter display
            ViewBag.HighlightId = highlightId;

            return View(items);
        }
    }
}


