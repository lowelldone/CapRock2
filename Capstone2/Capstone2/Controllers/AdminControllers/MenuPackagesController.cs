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
        public async Task<IActionResult> Create([Bind("MenuPackageId,MenuPackageName,NoOfMainDish,NoOfSideDish,NoOfDessert,NoOfRice,NoOfSoftDrinks")] MenuPackages menuPackages)
        {
            if (ModelState.IsValid)
            {
                _context.Add(menuPackages);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
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
        public async Task<IActionResult> Edit(int id, [Bind("MenuPackageId,MenuPackageName,NoOfMainDish,NoOfSideDish,NoOfDessert,NoOfRice,NoOfSoftDrinks")] MenuPackages menuPackages)
        {
            if (id != menuPackages.MenuPackageId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(menuPackages);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MenuPackagesExists(menuPackages.MenuPackageId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
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
            var menuPackages = await _context.MenuPackages.FindAsync(id);
            if (menuPackages != null)
            {
                _context.MenuPackages.Remove(menuPackages);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MenuPackagesExists(int id)
        {
            return _context.MenuPackages.Any(e => e.MenuPackageId == id);
        }
    }
}
