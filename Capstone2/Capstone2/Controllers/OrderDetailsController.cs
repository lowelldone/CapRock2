using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Capstone2.Controllers
{
    public class OrderDetailsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrderDetailsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            Order order = JsonSerializer.Deserialize<Order>(TempData["Order"] as string);
            return View(order);
        }

        public async Task<IActionResult> OrderConfirmed(string orderJson)
        {
            Order order = JsonSerializer.Deserialize<Order>(orderJson);
            order.OrderDetails.ForEach(x => x.Menu = null);

            // Check pax limits for the catering date
            var existingOrdersForDate = await _context.Orders
                .Where(o => o.CateringDate.Date == order.CateringDate.Date && o.Status != "Cancelled")
                .ToListAsync();

            int totalPaxForDate = existingOrdersForDate.Sum(o => o.NoOfPax);
            int newOrderPax = order.NoOfPax;

            //// Check if this is a large order (701-1500 pax)
            //if (newOrderPax >= 701 && newOrderPax <= 1500)
            //{
            //    // Large orders can only be the only order for that day
            //    if (existingOrdersForDate.Any())
            //    {
            //        return Json(new
            //        {
            //            success = false,
            //            message = "Large orders (701-1500 pax) cannot be scheduled on the same day as other orders. Please choose a different date."
            //        });
            //    }
            //}

            // Generate unique order number
            order.OrderNumber = await GenerateOrderNumber();

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            return Json(new { success = true, orderNumber = order.OrderNumber, orderId = order.OrderId });
        }

        private async Task<string> GenerateOrderNumber()
        {
            var today = DateTime.Today;
            var dateString = today.ToString("yyyyMMdd");

            // Get the count of orders for today
            var todayOrderCount = await _context.Orders
                .Where(o => o.OrderDate.Date == today)
                .CountAsync();

            // Generate sequential number (starting from 1)
            var sequentialNumber = todayOrderCount + 1;

            return $"ORD-{dateString}-{sequentialNumber:D3}";
        }
        [HttpGet]
        public async Task<IActionResult> Edit(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Menu)
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound();

            ViewBag.Menus = await _context.Menu.ToListAsync();
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int orderId, List<OrderDetail> orderDetails)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Menu)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return NotFound();

            // Remove all existing details and add new ones
            _context.OrderDetails.RemoveRange(order.OrderDetails);
            await _context.SaveChangesAsync();

            double total = 0;
            if (orderDetails != null)
            {
                foreach (var detail in orderDetails)
                {
                    if (detail != null && detail.MenuId > 0)
                    {
                        // Get the menu price from the database
                        var menu = await _context.Menu.FindAsync(detail.MenuId);
                        if (menu != null)
                        {
                            total += menu.Price * detail.Quantity;
                        }
                        _context.OrderDetails.Add(new OrderDetail
                        {
                            MenuId = detail.MenuId,
                            Name = detail.Name,
                            Quantity = detail.Quantity,
                            OrderId = orderId
                        });
                    }
                }
            }
            // Apply rush order fee if OrderDate and CateringDate are the same day
            var isRush = order.OrderDate.Date == order.CateringDate.Date;
            order.TotalPayment = isRush ? total + (total * 0.10) : total;
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            return RedirectToAction("ViewOrder", "Customers", new { id = order.CustomerID });
        }

        [HttpGet]
        public async Task<IActionResult> GenerateInvoice(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Menu)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound();

            string Peso(double v) => $"₱{v:N2}";

            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Row(row =>
                    {
                        row.RelativeColumn().Stack(stack =>
                        {
                            stack.Item().Text("GRUSH Catering").FontSize(16).SemiBold();
                            stack.Item().Text("Official Invoice").FontColor(Colors.Blue.Medium);
                        });

                        row.ConstantColumn(260).AlignRight().Stack(stack =>
                        {
                            stack.Item().Text(text =>
                            {
                                text.Span("Order No: ").SemiBold();
                                text.Span(string.IsNullOrWhiteSpace(order.OrderNumber) ? $"ORD-{order.OrderDate:yyyyMMdd}-{order.OrderId:D3}" : order.OrderNumber);
                            });
                            stack.Item().Text(text =>
                            {
                                text.Span("Order Date: ").SemiBold();
                                text.Span(order.OrderDate.ToString("dd/MM/yyyy"));
                            });
                            stack.Item().Text(text =>
                            {
                                text.Span("Catering Date: ").SemiBold();
                                text.Span(order.CateringDate.ToString("dd/MM/yyyy"));
                            });
                        });
                    });

                    page.Content().Stack(stack =>
                    {
                        stack.Spacing(10);

                        // Billed To
                        stack.Item().Text("Billed To").SemiBold();
                        stack.Item().Text(order.Customer.Name);
                        if (!string.IsNullOrWhiteSpace(order.Customer.ContactNo))
                            stack.Item().Text(order.Customer.ContactNo).FontColor(Colors.Blue.Medium);
                        if (!string.IsNullOrWhiteSpace(order.Customer.Address))
                            stack.Item().Text(order.Customer.Address);

                        stack.Item().PaddingTop(10).Element(container =>
                        {
                            container.Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(5);   // Menu
                                    columns.RelativeColumn(2);   // Qty
                                    columns.RelativeColumn(3);   // Unit Price
                                    columns.RelativeColumn(3);   // Subtotal
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellHeader).Text("Menu");
                                    header.Cell().Element(CellHeader).AlignRight().Text("Quantity");
                                    header.Cell().Element(CellHeader).AlignRight().Text("Unit Price");
                                    header.Cell().Element(CellHeader).AlignRight().Text("Subtotal");

                                    static IContainer CellHeader(IContainer container) =>
                                        container.DefaultTextStyle(x => x.SemiBold())
                                            .Background(Colors.Grey.Lighten3)
                                            .PaddingVertical(6)
                                            .PaddingHorizontal(8);
                                });

                                foreach (var item in order.OrderDetails)
                                {
                                    var unit = item.Menu?.Price ?? 0;
                                    var subtotal = unit * item.Quantity;
                                    table.Cell().Element(Cell).Text(item.Name);
                                    table.Cell().Element(Cell).AlignRight().Text(item.Quantity.ToString());
                                    table.Cell().Element(Cell).AlignRight().Text(Peso(unit));
                                    table.Cell().Element(Cell).AlignRight().Text(Peso(subtotal));
                                }

                                static IContainer Cell(IContainer container) =>
                                    container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3)
                                             .PaddingVertical(6).PaddingHorizontal(8);
                            });
                        });

                        // Totals
                        stack.Item().PaddingTop(10).AlignRight().Stack(totals =>
                        {
                            totals.Spacing(4);

                            if (order.IsRushOrder)
                            {
                                totals.Item().Text(text =>
                                {
                                    text.Span("Base Amount: ").SemiBold();
                                    text.Span(Peso(order.BaseAmount));
                                });
                                totals.Item().Text(text =>
                                {
                                    text.Span("Rush Order Fee (10%): ").SemiBold().FontColor(Colors.Red.Medium);
                                    text.Span(Peso(order.RushOrderFee)).FontColor(Colors.Red.Medium);
                                });
                            }

                            totals.Item().Text(text =>
                            {
                                text.Span("Total Payment: ").SemiBold().FontSize(13);
                                text.Span(Peso(order.TotalPayment)).SemiBold().FontSize(13);
                            });

                            totals.Item().Text(text =>
                            {
                                text.Span("50% Downpayment Required: ").SemiBold().FontColor(Colors.Blue.Medium);
                                text.Span(Peso(order.TotalPayment * 0.5)).FontColor(Colors.Blue.Medium);
                            });
                        });
                    });

                    page.Footer().AlignCenter().Text("Thank you for your business!");
                });
            });

            var pdf = document.GeneratePdf();
            var fileName = $"{order.Customer.Name} - Order Invoice.pdf";
            return File(pdf, "application/pdf", fileName);
        }
    }
}
