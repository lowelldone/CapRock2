using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Capstone2.Data;
using Capstone2.Models;
using Capstone2.Helpers;

namespace Capstone2.Controllers.AdminControllers
{
    public class RecoveryCodesController : GenericController
    {
        private readonly ApplicationDbContext _context;

        public RecoveryCodesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public IActionResult Generate(int userId, string returnController = "Administrators", string returnAction = "Index")
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "ADMIN")
            {
                return RedirectToAction("Login", "Home");
            }

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                return NotFound();
            }

            var existing = _context.UserRecoveryCodes.Where(c => c.UserId == userId && !c.IsUsed).ToList();
            foreach (var c in existing)
            {
                c.IsUsed = true;
                c.UsedUtc = DateTime.UtcNow;
            }

            var plainCodes = RecoveryCodes.Generate(10, 8);
            var now = DateTime.UtcNow;
            foreach (var code in plainCodes)
            {
                var (hash, salt) = RecoveryCodes.HashSecret(code);
                _context.UserRecoveryCodes.Add(new UserRecoveryCode
                {
                    UserId = userId,
                    CodeHash = hash,
                    Salt = salt,
                    IsUsed = false,
                    CreatedUtc = now
                });
            }

            try
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    Username = HttpContext.Session.GetString("Username"),
                    Role = role,
                    Action = "GenerateRecoveryCodes",
                    Details = $"Generated {plainCodes.Count} codes for userId={userId}, username={user.Username}"
                });
            }
            catch { }

            _context.SaveChanges();

            ViewBag.Username = user.Username;
            ViewBag.Generated = now.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            ViewBag.ReturnController = returnController;
            ViewBag.ReturnAction = returnAction;
            return View("Show", plainCodes);
        }
    }
}


