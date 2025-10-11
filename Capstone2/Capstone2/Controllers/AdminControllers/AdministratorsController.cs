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
    public class AdministratorsController : GenericController
    {
        private readonly ApplicationDbContext _context;

        public AdministratorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Administrators
        public async Task<IActionResult> Index()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "ADMIN")
            {
                return RedirectToAction("Login", "Home");
            }

            // Only show users with Admin role
            var admins = await _context.Users
                .Where(u => u.Role.ToUpper() == "ADMIN")
                .OrderByDescending(u => u.UserId)
                .ToListAsync();

            return View(admins);
        }

        // GET: Administrators/Create
        public IActionResult Create()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "ADMIN")
            {
                return RedirectToAction("Login", "Home");
            }

            return View(new User { Role = "ADMIN" });
        }

        // POST: Administrators/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Username,Password,FirstName,LastName,UserNumber")] User user)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "ADMIN")
            {
                return RedirectToAction("Login", "Home");
            }

            try
            {
                // Check if username already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == user.Username);

                if (existingUser != null)
                {
                    ModelState.AddModelError("Username", "Username already exists.");
                    return View(user);
                }

                // Force role to be Admin
                user.Role = "ADMIN";

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Audit log
                try
                {
                    var sessionUsername = HttpContext.Session.GetString("Username");
                    var sessionRole = HttpContext.Session.GetString("Role");

                    _context.AuditLogs.Add(new AuditLog
                    {
                        Username = sessionUsername,
                        Role = sessionRole,
                        Action = "CreateAdministrator",
                        Details = $"Created new administrator: {user.Username} ({user.FirstName} {user.LastName})"
                    });
                    await _context.SaveChangesAsync();
                }
                catch { }

                TempData["SuccessMessage"] = "Administrator created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error creating administrator: {ex.Message}");
                return View(user);
            }
        }

        // GET: Administrators/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "ADMIN")
            {
                return RedirectToAction("Login", "Home");
            }

            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null || user.Role.ToUpper() != "ADMIN")
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Administrators/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("UserId,Username,Password,FirstName,LastName,UserNumber")] User user)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "ADMIN")
            {
                return RedirectToAction("Login", "Home");
            }

            if (id != user.UserId)
            {
                return NotFound();
            }

            try
            {
                // Check if username already exists for another user
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == user.Username && u.UserId != user.UserId);

                if (existingUser != null)
                {
                    ModelState.AddModelError("Username", "Username already exists.");
                    return View(user);
                }

                // Force role to be Admin
                user.Role = "ADMIN";

                _context.Update(user);
                await _context.SaveChangesAsync();

                // Audit log
                try
                {
                    var sessionUsername = HttpContext.Session.GetString("Username");
                    var sessionRole = HttpContext.Session.GetString("Role");

                    _context.AuditLogs.Add(new AuditLog
                    {
                        Username = sessionUsername,
                        Role = sessionRole,
                        Action = "EditAdministrator",
                        Details = $"Updated administrator: {user.Username} ({user.FirstName} {user.LastName})"
                    });
                    await _context.SaveChangesAsync();
                }
                catch { }

                TempData["SuccessMessage"] = "Administrator updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(user.UserId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error updating administrator: {ex.Message}");
                return View(user);
            }
        }

        // GET: Administrators/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "ADMIN")
            {
                return RedirectToAction("Login", "Home");
            }

            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.UserId == id && m.Role.ToUpper() == "ADMIN");
            if (user == null)
            {
                return NotFound();
            }

            // Prevent deleting currently logged-in user
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == id)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account while logged in.";
                return RedirectToAction(nameof(Index));
            }

            return View(user);
        }

        // POST: Administrators/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "ADMIN")
            {
                return RedirectToAction("Login", "Home");
            }

            // Prevent deleting currently logged-in user
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == id)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account while logged in.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user != null && user.Role.ToUpper() == "ADMIN")
                {
                    var username = user.Username;
                    var fullName = $"{user.FirstName} {user.LastName}";

                    _context.Users.Remove(user);
                    await _context.SaveChangesAsync();

                    // Audit log
                    try
                    {
                        var sessionUsername = HttpContext.Session.GetString("Username");
                        var sessionRole = HttpContext.Session.GetString("Role");

                        _context.AuditLogs.Add(new AuditLog
                        {
                            Username = sessionUsername,
                            Role = sessionRole,
                            Action = "DeleteAdministrator",
                            Details = $"Deleted administrator: {username} ({fullName})"
                        });
                        await _context.SaveChangesAsync();
                    }
                    catch { }

                    TempData["SuccessMessage"] = "Administrator deleted successfully!";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting administrator: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.UserId == id);
        }
    }
}

