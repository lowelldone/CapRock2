using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;

namespace Capstone2.Controllers
{
    public class AccountController : GenericController
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Account
        public IActionResult Index()
        {
            return View();
        }

        // POST: Account/UpdateAccount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAccount(string currentPassword, string newPassword)
        {
            var role = HttpContext.Session.GetString("Role");
            try
            {
                // Get current user from session
                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    TempData["ProfileError"] = "User session not found. Please log in again.";
                    return RedirectToAction("Index");
                }

                // Find the current user
                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId.Value);
                if (currentUser == null)
                {
                    TempData["ProfileError"] = "User not found.";
                    return RedirectToAction("Index");
                }

                // Verify current password
                if (currentUser.Password != currentPassword)
                {
                    TempData["ProfileError"] = "Current password is incorrect.";
                    return RedirectToAction("Index");
                }

                // Track changes for audit logging
                var changes = new List<string>();
                if (currentUser.Password != newPassword)
                    changes.Add("password changed");

                // Update user information
                currentUser.Password = newPassword;
                _context.Users.Update(currentUser);
                await _context.SaveChangesAsync();

                // Audit: profile update
                try
                {
                    var sessionUsername = HttpContext.Session.GetString("Username");

                    _context.AuditLogs.Add(new AuditLog
                    {
                        Username = sessionUsername,
                        Role = role,
                        Action = "UpdateAccount",
                        Details = changes.Any() ?
                            $"Account updated ({string.Join(", ", changes)})" :
                            "Account updated (no changes detected)"
                    });
                    await _context.SaveChangesAsync();
                }
                catch { }

                TempData["ProfileSuccess"] = "Account updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ProfileError"] = $"Error updating account: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
    }
}
