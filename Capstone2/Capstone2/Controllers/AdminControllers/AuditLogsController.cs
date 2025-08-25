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
            DateTime? filterDate = null, int page = 1, int pageSize = 50)
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
                // Convert to UTC start and end of the selected date to handle timezone issues
                var startOfDay = filterDate.Value.Date.ToUniversalTime();
                var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

                query = query.Where(l => l.Timestamp >= startOfDay && l.Timestamp <= endOfDay);
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Map WaiterId -> Waiter Full Name for display
            var waiterIds = items.Where(i => i.WaiterId.HasValue).Select(i => i.WaiterId!.Value).Distinct().ToList();
            if (waiterIds.Any())
            {
                var waiters = await _context.Waiters
                    .Include(w => w.User)
                    .Where(w => waiterIds.Contains(w.WaiterId))
                    .ToListAsync();
                var waiterNames = waiters.ToDictionary(
                    w => w.WaiterId,
                    w => w.User != null ? ($"{w.User.FirstName} {w.User.LastName}") : ($"Waiter #{w.WaiterId}")
                );
                ViewBag.WaiterNames = waiterNames;
            }

            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.FilterDate = filterDate;
            ViewBag.SelectedRole = role; // Pass selected role to view for filter display

            return View(items);
        }
    }
}


