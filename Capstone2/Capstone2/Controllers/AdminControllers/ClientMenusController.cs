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
    public class ClientMenusController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClientMenusController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ClientMenus
        public async Task<IActionResult> Index()
        {
            ViewBag.isAdmin = false;
            return View(await _context.Menu.ToListAsync());
        }

        //Client Order
        public IActionResult OrderDetailConfirmation()
        {
            return View();
        }
    }
}
