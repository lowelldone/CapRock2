using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;        // adjust namespace
using Capstone2.Models;      // where your Customer model lives

namespace Capstone2.Controllers
{
    public class PaidOrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PaidOrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: PaidOrders
        public async Task<IActionResult> Index(string statusFilter)
        {
            var paidOrders = _context.Customers
                .Include(c => c.Order)
                .Where(c => c.IsPaid);

            if (!string.IsNullOrEmpty(statusFilter))
            {
                paidOrders = paidOrders.Where(c => c.Order.Status == statusFilter);
            }

            return View(await paidOrders.ToListAsync());
        }

        // GET: PaidOrders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return BadRequest();

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Menu)
                .FirstOrDefaultAsync(o => o.CustomerID == id.Value);

            if (order == null)
                return NotFound();

            return View(order);
        }
    }
}
