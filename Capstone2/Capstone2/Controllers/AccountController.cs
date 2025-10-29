using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;
using Capstone2.Helpers;
using Microsoft.AspNetCore.Http;

namespace Capstone2.Controllers
{
    public class AccountController : GenericController
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Allow Recovery action without session (for password reset)
            if (context.ActionDescriptor.RouteValues["action"] == "Recovery")
            {
                // Skip session check - allow anonymous access for password recovery
                return;
            }

            // For other actions, require session (inherit from GenericController)
            base.OnActionExecuting(context);
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

        [HttpGet]
        public IActionResult Recovery()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Recovery(string Username, string RecoveryCode, string NewPassword, string ConfirmPassword)
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(RecoveryCode) || string.IsNullOrWhiteSpace(NewPassword))
            {
                ViewBag.Error = "All fields are required.";
                return View();
            }
            if (NewPassword != ConfirmPassword)
            {
                ViewBag.Error = "Passwords do not match.";
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == Username);
            if (user == null)
            {
                ViewBag.Error = "User not found.";
                return View();
            }

            var codes = await _context.UserRecoveryCodes
                .Where(c => c.UserId == user.UserId && !c.IsUsed)
                .ToListAsync();

            var matched = codes.FirstOrDefault(c => RecoveryCodes.Verify(RecoveryCode, c.CodeHash, c.Salt));
            if (matched == null)
            {
                ViewBag.Error = "Invalid or already used recovery code.";
                return View();
            }

            matched.IsUsed = true;
            matched.UsedUtc = DateTime.UtcNow;
            user.Password = NewPassword; // Note: plain text in current system

            await _context.SaveChangesAsync();

            try
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    Username = user.Username,
                    Role = user.Role,
                    Action = "PasswordRecovery",
                    Details = "Password reset using recovery code"
                });
                await _context.SaveChangesAsync();
            }
            catch { }

            TempData["SuccessMessage"] = "Password updated. You can now log in.";
            return RedirectToAction("Login", "Home");
        }
    }
}
