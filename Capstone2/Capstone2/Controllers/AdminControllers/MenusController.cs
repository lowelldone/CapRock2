using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace Capstone2.Controllers.AdminControllers
{
    public class MenusController : GenericController
    {
        private readonly ApplicationDbContext _context;

        public MenusController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Menus
        public async Task<IActionResult> Index(string searchString)
        {
            var menus = await _context.Menu.ToListAsync();
            var menuPackages = await _context.MenuPackages.ToListAsync();

            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.ToLower();
                menus = menus.Where(m => m.Name.ToLower().Contains(searchString)).ToList();
            }

            var viewModel = new Capstone2.Models.MenusManagementViewModel
            {
                Menus = menus,
                MenuPackagesList = menuPackages,
                SearchString = searchString
            };
            return View(viewModel);
        }

        // GET: Menus/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Menus/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MenuId,Name,Category,Price,DishType")] Menu menu, IFormFile ImageFile)
        {
            // Check for blank inputs
            if (string.IsNullOrWhiteSpace(menu.Name))
            {
                ModelState.AddModelError("Name", "Dish name is required.");
            }
            if (string.IsNullOrWhiteSpace(menu.Category))
            {
                ModelState.AddModelError("Category", "Category is required.");
            }
            if (string.IsNullOrWhiteSpace(menu.DishType))
            {
                ModelState.AddModelError("DishType", "Dish type is required.");
            }
            if (menu.Price <= 0)
            {
                ModelState.AddModelError("Price", "Price must be greater than zero.");
            }

            // Check for duplicate dish names
            if (!string.IsNullOrWhiteSpace(menu.Name))
            {
                var existingDish = await _context.Menu
                    .FirstOrDefaultAsync(m => m.Name.ToLower().Trim() == menu.Name.ToLower().Trim());
                if (existingDish != null)
                {
                    ModelState.AddModelError("Name", "A dish with this name already exists.");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (ImageFile != null && ImageFile.Length > 0)
                    {
                        string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
                        Directory.CreateDirectory(uploadsFolder); // ensures folder exists

                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + ImageFile.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await ImageFile.CopyToAsync(fileStream);
                        }

                        menu.ImagePath = "/images/" + uniqueFileName;
                    }

                    _context.Add(menu);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Dish '{menu.Name}' successfully created!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error creating dish: {ex.Message}");
                }
            }
            return View(menu);
        }


        // GET: Menus/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var menu = await _context.Menu.FindAsync(id);
            if (menu == null)
            {
                return NotFound();
            }
            return View(menu);
        }

        // POST: Menus/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MenuId,Name,Category,Price,ImagePath,DishType")] Menu menu, IFormFile ImageFile)
        {
            if (id != menu.MenuId)
            {
                return NotFound();
            }

            // Check for blank inputs
            if (string.IsNullOrWhiteSpace(menu.Name))
            {
                ModelState.AddModelError("Name", "Dish name is required.");
            }
            if (string.IsNullOrWhiteSpace(menu.Category))
            {
                ModelState.AddModelError("Category", "Category is required.");
            }
            if (string.IsNullOrWhiteSpace(menu.DishType))
            {
                ModelState.AddModelError("DishType", "Dish type is required.");
            }
            if (menu.Price <= 0)
            {
                ModelState.AddModelError("Price", "Price must be greater than zero.");
            }

            // Check for duplicate dish names (excluding current dish)
            if (!string.IsNullOrWhiteSpace(menu.Name))
            {
                var existingDish = await _context.Menu
                    .FirstOrDefaultAsync(m => m.Name.ToLower().Trim() == menu.Name.ToLower().Trim() && m.MenuId != menu.MenuId);
                if (existingDish != null)
                {
                    ModelState.AddModelError("Name", "A dish with this name already exists.");
                }
            }

            ModelState.Remove("ImageFile");
            if (ModelState.IsValid)
            {
                try
                {
                    if (ImageFile != null && ImageFile.Length > 0)
                    {
                        string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
                        Directory.CreateDirectory(uploadsFolder); // ensures folder exists

                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + ImageFile.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await ImageFile.CopyToAsync(fileStream);
                        }

                        menu.ImagePath = "/images/" + uniqueFileName;
                    }
                    _context.Update(menu);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Dish '{menu.Name}' successfully updated!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MenuExists(menu.MenuId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        ModelState.AddModelError("", "The dish was modified by another user. Please refresh and try again.");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error updating dish: {ex.Message}");
                }
            }
            return View(menu);
        }

        // GET: Menus/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var menu = await _context.Menu
                .FirstOrDefaultAsync(m => m.MenuId == id);
            if (menu == null)
            {
                return NotFound();
            }

            return View(menu);
        }

        // POST: Menus/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var menu = await _context.Menu.FindAsync(id);
                if (menu != null)
                {
                    var dishName = menu.Name;
                    _context.Menu.Remove(menu);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Dish '{dishName}' successfully deleted!";
                }
                else
                {
                    TempData["Error"] = "Dish not found.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting dish: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool MenuExists(int id)
        {
            return _context.Menu.Any(e => e.MenuId == id);
        }
    }
}

