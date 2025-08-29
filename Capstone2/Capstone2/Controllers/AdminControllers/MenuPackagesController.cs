using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;

namespace Capstone2.Controllers.AdminControllers
{
    public class MenuPackagesController : GenericController
    {
        private readonly ApplicationDbContext _context;

        public MenuPackagesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: MenuPackages
        public async Task<IActionResult> Index()
        {
            return View(await _context.MenuPackages.ToListAsync());
        }

        // GET: MenuPackages/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: MenuPackages/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MenuPackageId,MenuPackageName,Price,NoOfMainDish,NoOfSideDish,NoOfDessert,NoOfRice,NoOfSoftDrinks")] MenuPackages menuPackages)
        {
            // Check for blank inputs
            if (string.IsNullOrWhiteSpace(menuPackages.MenuPackageName))
            {
                ModelState.AddModelError("MenuPackageName", "Package name is required.");
            }

            // Validate Price (must be non-negative)
            if (menuPackages.Price < 0)
            {
                ModelState.AddModelError("Price", "Price must be zero or greater.");
            }

            // Check for duplicate package names
            if (!string.IsNullOrWhiteSpace(menuPackages.MenuPackageName))
            {
                var existingPackage = await _context.MenuPackages
                    .FirstOrDefaultAsync(m => m.MenuPackageName.ToLower().Trim() == menuPackages.MenuPackageName.ToLower().Trim());
                if (existingPackage != null)
                {
                    ModelState.AddModelError("MenuPackageName", "A package with this name already exists.");
                }
            }

            // Validate quantities (must be non-negative)
            if (menuPackages.NoOfMainDish < 0)
            {
                ModelState.AddModelError("NoOfMainDish", "Number of main dishes cannot be negative.");
            }
            if (menuPackages.NoOfSideDish < 0)
            {
                ModelState.AddModelError("NoOfSideDish", "Number of side dishes cannot be negative.");
            }
            if (menuPackages.NoOfDessert < 0)
            {
                ModelState.AddModelError("NoOfDessert", "Number of desserts cannot be negative.");
            }
            if (menuPackages.NoOfRice < 0)
            {
                ModelState.AddModelError("NoOfRice", "Number of rice dishes cannot be negative.");
            }
            if (menuPackages.NoOfSoftDrinks < 0)
            {
                ModelState.AddModelError("NoOfSoftDrinks", "Number of soft drinks cannot be negative.");
            }

            // At least one item must be selected
            if (menuPackages.NoOfMainDish == 0 && menuPackages.NoOfSideDish == 0 &&
                menuPackages.NoOfDessert == 0 && menuPackages.NoOfRice == 0 && menuPackages.NoOfSoftDrinks == 0)
            {
                ModelState.AddModelError("", "At least one item type must be selected with quantity greater than 0.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(menuPackages);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Menu Package '{menuPackages.MenuPackageName}' successfully created!";
                    return RedirectToAction("Index", "Menus");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error creating menu package: {ex.Message}");
                }
            }
            return View(menuPackages);
        }

        // GET: MenuPackages/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var menuPackages = await _context.MenuPackages.FindAsync(id);
            if (menuPackages == null)
            {
                return NotFound();
            }
            return View(menuPackages);
        }

        // POST: MenuPackages/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MenuPackageId,MenuPackageName,Price,NoOfMainDish,NoOfSideDish,NoOfDessert,NoOfRice,NoOfSoftDrinks")] MenuPackages menuPackages)
        {
            if (id != menuPackages.MenuPackageId)
            {
                return NotFound();
            }

            // Check for blank inputs
            if (string.IsNullOrWhiteSpace(menuPackages.MenuPackageName))
            {
                ModelState.AddModelError("MenuPackageName", "Package name is required.");
            }

            // Check for duplicate package names (excluding current package)
            if (!string.IsNullOrWhiteSpace(menuPackages.MenuPackageName))
            {
                var existingPackage = await _context.MenuPackages
                    .FirstOrDefaultAsync(m => m.MenuPackageName.ToLower().Trim() == menuPackages.MenuPackageName.ToLower().Trim() && m.MenuPackageId != menuPackages.MenuPackageId);
                if (existingPackage != null)
                {
                    ModelState.AddModelError("MenuPackageName", "A package with this name already exists.");
                }
            }

            // Validate Price and quantities (must be non-negative)
            if (menuPackages.Price < 0)
            {
                ModelState.AddModelError("Price", "Price must be zero or greater.");
            }
            if (menuPackages.NoOfMainDish < 0)
            {
                ModelState.AddModelError("NoOfMainDish", "Number of main dishes cannot be negative.");
            }
            if (menuPackages.NoOfSideDish < 0)
            {
                ModelState.AddModelError("NoOfSideDish", "Number of side dishes cannot be negative.");
            }
            if (menuPackages.NoOfDessert < 0)
            {
                ModelState.AddModelError("NoOfDessert", "Number of desserts cannot be negative.");
            }
            if (menuPackages.NoOfRice < 0)
            {
                ModelState.AddModelError("NoOfRice", "Number of rice dishes cannot be negative.");
            }
            if (menuPackages.NoOfSoftDrinks < 0)
            {
                ModelState.AddModelError("NoOfSoftDrinks", "Number of soft drinks cannot be negative.");
            }

            // At least one item must be selected
            if (menuPackages.NoOfMainDish == 0 && menuPackages.NoOfSideDish == 0 &&
                menuPackages.NoOfDessert == 0 && menuPackages.NoOfRice == 0 && menuPackages.NoOfSoftDrinks == 0)
            {
                ModelState.AddModelError("", "At least one item type must be selected with quantity greater than 0.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(menuPackages);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Menu Package '{menuPackages.MenuPackageName}' successfully updated!";
                    return RedirectToAction("Index", "Menus");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MenuPackagesExists(menuPackages.MenuPackageId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        ModelState.AddModelError("", "The package was modified by another user. Please refresh and try again.");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error updating menu package: {ex.Message}");
                }
            }
            return View(menuPackages);
        }

        // GET: MenuPackages/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var menuPackages = await _context.MenuPackages
                .FirstOrDefaultAsync(m => m.MenuPackageId == id);
            if (menuPackages == null)
            {
                return NotFound();
            }

            return View(menuPackages);
        }

        // POST: MenuPackages/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var menuPackages = await _context.MenuPackages.FindAsync(id);
                if (menuPackages != null)
                {
                    var packageName = menuPackages.MenuPackageName;
                    _context.MenuPackages.Remove(menuPackages);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Menu Package '{packageName}' successfully deleted!";
                }
                else
                {
                    TempData["Error"] = "Menu package not found.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting menu package: {ex.Message}";
            }

            return RedirectToAction("Index", "Menus");
        }

        private bool MenuPackagesExists(int id)
        {
            return _context.MenuPackages.Any(e => e.MenuPackageId == id);
        }
    }
}

