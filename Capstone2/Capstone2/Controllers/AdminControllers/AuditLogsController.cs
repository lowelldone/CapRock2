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
        public async Task<IActionResult> Index(string? role = "HEADWAITER", string? actionName = null, string? orderNumber = null, string? username = null, int page = 1, int pageSize = 50)
        {
            var currentRole = HttpContext.Session.GetString("Role");
            if (currentRole != "ADMIN")
                return Forbid();

            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(role))
                query = query.Where(l => l.Role == role);
            if (!string.IsNullOrWhiteSpace(actionName))
                query = query.Where(l => l.Action == actionName);
            if (!string.IsNullOrWhiteSpace(orderNumber))
                query = query.Where(l => l.OrderNumber == orderNumber);
            if (!string.IsNullOrWhiteSpace(username))
                query = query.Where(l => l.Username == username);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            return View(items);
        }
    }
}


