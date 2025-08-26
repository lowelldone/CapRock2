using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Capstone2.Data;
using Capstone2.Models;
using System.Text.Json; // Added for JsonSerializer
using Microsoft.AspNetCore.Http; // Added for session support

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
            ViewBag.MenuPackages = await _context.MenuPackages.ToListAsync();
            return View(await _context.Menu.ToListAsync());
        }

        // POST: ClientMenus/Index (when returning with OrderItemsJson)
        [HttpPost]
        public async Task<IActionResult> Index(string OrderItemsJson)
        {
            ViewBag.isAdmin = false;
            ViewBag.OrderItemsJson = OrderItemsJson;
            ViewBag.MenuPackages = await _context.MenuPackages.ToListAsync();
            return View(await _context.Menu.ToListAsync());
        }

        //Client Order
        public IActionResult OrderDetailConfirmation()
        {
            return View();
        }

        // Selected Package Menus
        public IActionResult SelectedPackageMenus()
        {
            ViewBag.isAdmin = false;
            return View();
        }

        // Package Form for customer information
        public IActionResult PackageForm()
        {
            ViewBag.isAdmin = false;
            return View();
        }

        // Store Package Order Data in Session
        [HttpPost]
        public IActionResult StorePackageOrderData([FromBody] object orderData)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"StorePackageOrderData: Received orderData type: {orderData?.GetType()}");
                System.Diagnostics.Debug.WriteLine($"StorePackageOrderData: Received orderData: {orderData}");

                var orderDataJson = JsonSerializer.Serialize(orderData);
                System.Diagnostics.Debug.WriteLine($"StorePackageOrderData: Serialized JSON length: {orderDataJson?.Length ?? 0}");

                HttpContext.Session.SetString("PackageOrderData", orderDataJson);
                System.Diagnostics.Debug.WriteLine($"StorePackageOrderData: Data stored in session successfully");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StorePackageOrderData: Error: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Clear Package Order Data from Session
        [HttpPost]
        public IActionResult ClearPackageOrderData()
        {
            try
            {
                HttpContext.Session.Remove("PackageOrderData");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Package Order Detail for confirmation
        public IActionResult PackageOrderDetail()
        {
            ViewBag.isAdmin = false;

            // Get the order data from session storage and populate TempData (like OrderDetails/Index)
            var orderData = HttpContext.Session.GetString("PackageOrderData");
            System.Diagnostics.Debug.WriteLine($"PackageOrderDetail: Session data found: {!string.IsNullOrEmpty(orderData)}");
            System.Diagnostics.Debug.WriteLine($"PackageOrderDetail: Session data length: {orderData?.Length ?? 0}");

            if (!string.IsNullOrEmpty(orderData))
            {
                TempData["PackageOrder"] = orderData;
                System.Diagnostics.Debug.WriteLine($"PackageOrderDetail: TempData set successfully");
                // Clear the session data after using it
                HttpContext.Session.Remove("PackageOrderData");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"PackageOrderDetail: No session data found");
            }

            return View();
        }

        // Package Order Confirmed
        [HttpPost]
        public async Task<IActionResult> PackageOrderConfirmed(string orderJson)
        {
            try
            {
                if (string.IsNullOrEmpty(orderJson))
                {
                    return Json(new { success = false, message = "No order data received." });
                }

                // Log the incoming JSON for debugging
                System.Diagnostics.Debug.WriteLine($"Incoming orderJson: {orderJson}");
                System.Diagnostics.Debug.WriteLine($"orderJson length: {orderJson?.Length ?? 0}");
                System.Diagnostics.Debug.WriteLine($"orderJson is null: {orderJson == null}");
                System.Diagnostics.Debug.WriteLine($"orderJson is empty: {string.IsNullOrEmpty(orderJson)}");

                // Deserialize the JSON to get the form data
                JsonElement orderData;
                try
                {
                    orderData = JsonSerializer.Deserialize<JsonElement>(orderJson);
                    System.Diagnostics.Debug.WriteLine($"Successfully deserialized orderJson to JsonElement with ValueKind: {orderData.ValueKind}");
                }
                catch (JsonException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"JsonException during deserialization: {ex.Message}");
                    return Json(new { success = false, message = "Invalid JSON format: " + ex.Message });
                }

                // Validate required fields exist
                var requiredFields = new[] { "Customer.Name", "Customer.ContactNo", "Customer.Address",
                                           "CateringDate", "Venue", "NoOfPax", "timeOfFoodServing",
                                           "Occasion", "Motif", "TotalPayment", "PackageData" };

                System.Diagnostics.Debug.WriteLine("Checking required fields...");
                foreach (var field in requiredFields)
                {
                    if (!orderData.TryGetProperty(field, out var fieldValue))
                    {
                        System.Diagnostics.Debug.WriteLine($"Missing required field: {field}");
                        return Json(new { success = false, message = $"Missing required field: {field}" });
                    }
                    System.Diagnostics.Debug.WriteLine($"Found field: {field}, Type: {fieldValue.ValueKind}, Value: {fieldValue}");
                }
                System.Diagnostics.Debug.WriteLine("All required fields found.");

                // Create Customer first
                var customer = new Customer
                {
                    Name = orderData.GetProperty("Customer.Name").GetString() ?? "Unknown Customer",
                    ContactNo = orderData.GetProperty("Customer.ContactNo").GetString() ?? "",
                    Address = orderData.GetProperty("Customer.Address").GetString() ?? ""
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync(); // Save to get CustomerID

                // Safe DateTime parsing
                DateTime cateringDate, timeOfServing;
                try
                {
                    var cateringDateStr = orderData.GetProperty("CateringDate").GetString();
                    var timeOfServingStr = orderData.GetProperty("timeOfFoodServing").GetString();

                    System.Diagnostics.Debug.WriteLine($"Parsing CateringDate: '{cateringDateStr}'");
                    System.Diagnostics.Debug.WriteLine($"Parsing timeOfFoodServing: '{timeOfServingStr}'");

                    cateringDate = DateTime.Parse(cateringDateStr);
                    timeOfServing = DateTime.Parse(timeOfServingStr);

                    System.Diagnostics.Debug.WriteLine($"Successfully parsed CateringDate: {cateringDate}");
                    System.Diagnostics.Debug.WriteLine($"Successfully parsed timeOfFoodServing: {timeOfServing}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing date fields: {ex.Message}");
                    return Json(new { success = false, message = $"Error parsing date fields: {ex.Message}" });
                }

                // Create Order
                var order = new Order
                {
                    CustomerID = customer.CustomerID,
                    OrderNumber = await GeneratePackageOrderNumber(),
                    OrderDate = DateTime.Now,
                    CateringDate = cateringDate,
                    Venue = orderData.GetProperty("Venue").GetString(),
                    NoOfPax = orderData.GetProperty("NoOfPax").ValueKind == JsonValueKind.Number ?
                               orderData.GetProperty("NoOfPax").GetInt32() :
                               int.Parse(orderData.GetProperty("NoOfPax").GetString()),
                    timeOfFoodServing = timeOfServing,
                    Occasion = orderData.GetProperty("Occasion").GetString(),
                    Motif = orderData.GetProperty("Motif").GetString(),
                    TotalPayment = orderData.GetProperty("TotalPayment").ValueKind == JsonValueKind.Number ?
                                   orderData.GetProperty("TotalPayment").GetDouble() :
                                   double.Parse(orderData.GetProperty("TotalPayment").GetString()),
                    Status = "Pending"
                };

                System.Diagnostics.Debug.WriteLine($"Created Order with ID: {order.OrderId}, CustomerID: {order.CustomerID}, OrderNumber: {order.OrderNumber}");

                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // Save to get OrderId
                System.Diagnostics.Debug.WriteLine($"Saved Order to database. Generated OrderId: {order.OrderId}");

                // Get package data from the form
                var packageDataJson = orderData.GetProperty("PackageData").GetString();
                if (string.IsNullOrEmpty(packageDataJson))
                {
                    return Json(new { success = false, message = "PackageData field is empty or null." });
                }
                System.Diagnostics.Debug.WriteLine($"PackageData JSON: {packageDataJson}");
                System.Diagnostics.Debug.WriteLine($"PackageData JSON length: {packageDataJson.Length}");

                JsonElement packageData;
                try
                {
                    packageData = JsonSerializer.Deserialize<JsonElement>(packageDataJson);
                    System.Diagnostics.Debug.WriteLine($"Successfully deserialized PackageData to JsonElement with ValueKind: {packageData.ValueKind}");
                }
                catch (JsonException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"JsonException during PackageData deserialization: {ex.Message}");
                    return Json(new { success = false, message = "Invalid package data JSON format: " + ex.Message });
                }

                // Validate package data
                if (packageData.ValueKind == JsonValueKind.Null || packageData.ValueKind == JsonValueKind.Undefined)
                {
                    return Json(new { success = false, message = "Package data is null or undefined." });
                }

                if (packageData.ValueKind != JsonValueKind.Object)
                {
                    return Json(new { success = false, message = $"Package data is not an object. Expected Object, got {packageData.ValueKind}." });
                }

                // Log the package data structure for debugging
                System.Diagnostics.Debug.WriteLine($"Package data ValueKind: {packageData.ValueKind}");
                var packageProperties = packageData.EnumerateObject().ToList();
                System.Diagnostics.Debug.WriteLine($"Package data has {packageProperties.Count} properties:");
                foreach (var prop in packageProperties)
                {
                    System.Diagnostics.Debug.WriteLine($"Property: {prop.Name}, Type: {prop.Value.ValueKind}, Value: {prop.Value}");
                }

                // Check if package data has any categories
                if (!packageProperties.Any())
                {
                    return Json(new { success = false, message = "Package data has no categories or properties." });
                }

                // Check if package data has at least one array with items
                bool hasAnyItems = false;
                foreach (var category in packageProperties)
                {
                    if (category.Value.ValueKind == JsonValueKind.Array && category.Value.GetArrayLength() > 0)
                    {
                        hasAnyItems = true;
                        System.Diagnostics.Debug.WriteLine($"Found category '{category.Name}' with {category.Value.GetArrayLength()} items");
                        break;
                    }
                }

                if (!hasAnyItems)
                {
                    return Json(new { success = false, message = "Package data has no items in any category." });
                }

                // Log the structure of each category for debugging
                foreach (var category in packageProperties)
                {
                    System.Diagnostics.Debug.WriteLine($"Category: {category.Name}, Type: {category.Value.ValueKind}, Length: {(category.Value.ValueKind == JsonValueKind.Array ? category.Value.GetArrayLength() : 0)}");
                    if (category.Value.ValueKind == JsonValueKind.Array)
                    {
                        for (int i = 0; i < category.Value.GetArrayLength(); i++)
                        {
                            var item = category.Value[i];
                            System.Diagnostics.Debug.WriteLine($"  Item {i}: Type={item.ValueKind}, Properties: {string.Join(", ", item.EnumerateObject().Select(p => $"{p.Name}:{p.Value.ValueKind}"))}");
                        }
                    }
                }

                if (!packageData.TryGetProperty("packageId", out var packageIdProp) ||
                    !packageData.TryGetProperty("packagePrice", out var packagePriceProp) ||
                    !packageData.TryGetProperty("totalPrice", out var totalPriceProp) ||
                    !packageData.TryGetProperty("paxQuantity", out var paxQuantityProp))
                {
                    return Json(new { success = false, message = "Invalid package data structure. Missing required properties." });
                }

                // Log the specific property types and values
                System.Diagnostics.Debug.WriteLine($"packageId: Type={packageIdProp.ValueKind}, Value={packageIdProp}");
                System.Diagnostics.Debug.WriteLine($"packagePrice: Type={packagePriceProp.ValueKind}, Value={packagePriceProp}");
                System.Diagnostics.Debug.WriteLine($"totalPrice: Type={totalPriceProp.ValueKind}, Value={totalPriceProp}");
                System.Diagnostics.Debug.WriteLine($"paxQuantity: Type={paxQuantityProp.ValueKind}, Value={paxQuantityProp}");

                // Safe type conversion with fallbacks
                int packageId;
                decimal packagePrice;
                decimal totalPrice;
                int paxQuantity;

                try
                {
                    System.Diagnostics.Debug.WriteLine("Converting package data types...");

                    packageId = packageIdProp.ValueKind == JsonValueKind.Number ? packageIdProp.GetInt32() : int.Parse(packageIdProp.GetString());
                    System.Diagnostics.Debug.WriteLine($"Converted packageId: {packageId}");

                    packagePrice = packagePriceProp.ValueKind == JsonValueKind.Number ? packagePriceProp.GetDecimal() : decimal.Parse(packagePriceProp.GetString());
                    System.Diagnostics.Debug.WriteLine($"Converted packagePrice: {packagePrice}");

                    totalPrice = totalPriceProp.ValueKind == JsonValueKind.Number ? totalPriceProp.GetDecimal() : decimal.Parse(totalPriceProp.GetString());
                    System.Diagnostics.Debug.WriteLine($"Converted totalPrice: {totalPrice}");

                    paxQuantity = paxQuantityProp.ValueKind == JsonValueKind.Number ? paxQuantityProp.GetInt32() : int.Parse(paxQuantityProp.GetString());
                    System.Diagnostics.Debug.WriteLine($"Converted paxQuantity: {paxQuantity}");

                    System.Diagnostics.Debug.WriteLine("All package data types converted successfully.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error converting package data types: {ex.Message}");
                    return Json(new { success = false, message = $"Error converting package data types: {ex.Message}. packageId: {packageIdProp}, packagePrice: {packagePriceProp}, totalPrice: {totalPriceProp}, paxQuantity: {paxQuantityProp}" });
                }

                // Create OrderDetails for package items
                var orderDetails = new List<OrderDetail>();

                // Add package items
                try
                {
                    bool hasItems = false;
                    foreach (var category in packageProperties)
                    {
                        if (category.Value.ValueKind == JsonValueKind.Array)
                        {
                            System.Diagnostics.Debug.WriteLine($"Processing category '{category.Name}' with {category.Value.GetArrayLength()} items");
                            foreach (var item in category.Value.EnumerateArray())
                            {
                                // Log the item structure for debugging
                                System.Diagnostics.Debug.WriteLine($"Processing item in {category.Name}: Type={item.ValueKind}, Properties: {string.Join(", ", item.EnumerateObject().Select(p => $"{p.Name}:{p.Value.ValueKind}"))}");

                                // Safe boolean conversion for isLechon
                                bool isLechon = false;
                                if (item.TryGetProperty("isLechon", out var isLechonProp))
                                {
                                    try
                                    {
                                        isLechon = isLechonProp.ValueKind == JsonValueKind.True ||
                                                  (isLechonProp.ValueKind == JsonValueKind.String && isLechonProp.GetString().ToLower() == "true") ||
                                                  (isLechonProp.ValueKind == JsonValueKind.Number && isLechonProp.GetInt32() == 1);
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error converting isLechon: {ex.Message}. Value: {isLechonProp}");
                                        isLechon = false; // Default to false on error
                                    }
                                }

                                if (!isLechon)
                                {
                                    if (item.TryGetProperty("id", out var idProp) && item.TryGetProperty("name", out var nameProp))
                                    {
                                        int menuId;
                                        try
                                        {
                                            menuId = idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt32() : int.Parse(idProp.GetString());
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Error converting MenuId: {ex.Message}. Value: {idProp}");
                                            continue; // Skip this item if we can't convert the ID
                                        }

                                        orderDetails.Add(new OrderDetail
                                        {
                                            OrderId = order.OrderId,
                                            MenuId = menuId,
                                            Name = nameProp.GetString() ?? "Unknown Item",
                                            Quantity = 1,
                                            Type = "Package Item",
                                            MenuPackageId = packageId,
                                            PackagePrice = packagePrice,
                                            PackageTotal = totalPrice
                                        });
                                        hasItems = true;
                                        System.Diagnostics.Debug.WriteLine($"Added item: MenuId={menuId}, Name={nameProp.GetString()}");
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Item missing required properties. Has id: {item.TryGetProperty("id", out _)}, Has name: {item.TryGetProperty("name", out _)}");
                                    }
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"Skipping lechon item in {category.Name}");
                                }
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Category '{category.Name}' is not an array (Type: {category.Value.ValueKind})");
                        }
                    }

                    if (!hasItems)
                    {
                        return Json(new { success = false, message = "No valid package items found to process." });
                    }
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Error processing package items: {ex.Message}" });
                }

                // Add free lechon for Package B
                if (paxQuantity >= 120)
                {
                    System.Diagnostics.Debug.WriteLine($"Adding free lechon for Package B (paxQuantity: {paxQuantity})");

                    // Find a valid menu item to use as a placeholder for the free lechon
                    var lechonMenu = await _context.Menu.FirstOrDefaultAsync(m => m.Name.Contains("Lechon") || m.Name.Contains("lechon"));
                    int lechonMenuId = lechonMenu?.MenuId ?? 1; // Use found lechon or default to 1

                    orderDetails.Add(new OrderDetail
                    {
                        OrderId = order.OrderId,
                        MenuId = lechonMenuId, // Use valid menu ID
                        Name = "1 Whole Lechon (Package B Bonus)",
                        Quantity = 1,
                        Type = "Package Bonus",
                        MenuPackageId = packageId,
                        PackagePrice = packagePrice,
                        PackageTotal = totalPrice,
                        IsFreeLechon = true
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"No free lechon for this package (paxQuantity: {paxQuantity})");
                }

                // Log the final order details for debugging
                System.Diagnostics.Debug.WriteLine($"Created {orderDetails.Count} order details:");
                foreach (var detail in orderDetails)
                {
                    System.Diagnostics.Debug.WriteLine($"  OrderDetail: MenuId={detail.MenuId}, Name={detail.Name}, Type={detail.Type}, MenuPackageId={detail.MenuPackageId}");
                }

                if (orderDetails.Any())
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Adding {orderDetails.Count} order details to database context...");
                        _context.OrderDetails.AddRange(orderDetails);
                        System.Diagnostics.Debug.WriteLine("Order details added to context, saving changes...");
                        await _context.SaveChangesAsync();
                        System.Diagnostics.Debug.WriteLine("Successfully saved order details to database.");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error saving order details to database: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                        // Get more detailed error information
                        var innerException = ex.InnerException;
                        while (innerException != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Inner exception: {innerException.Message}");
                            innerException = innerException.InnerException;
                        }

                        return Json(new { success = false, message = $"Error saving order details to database: {ex.Message}. Inner exception: {ex.InnerException?.Message}" });
                    }
                }
                else
                {
                    return Json(new { success = false, message = "No order details were created. Please check your package selection." });
                }

                System.Diagnostics.Debug.WriteLine("Package order processed successfully.");
                System.Diagnostics.Debug.WriteLine($"Returning success response: orderNumber={order.OrderNumber}, orderId={order.OrderId}");
                return Json(new { success = true, orderNumber = order.OrderNumber, orderId = order.OrderId });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in PackageOrderConfirmed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = "Error processing package order: " + ex.Message });
            }
        }

        private async Task<string> GeneratePackageOrderNumber()
        {
            var today = DateTime.Today;
            var dateString = today.ToString("yyyyMMdd");

            // Get the count of package orders for today
            var todayPackageOrderCount = await _context.Orders
                .Where(o => o.OrderDate.Date == today && o.OrderDetails.Any(od => od.Type == "Package Item"))
                .CountAsync();

            // Generate sequential number (starting from 1)
            var sequentialNumber = todayPackageOrderCount + 1;

            return $"PKG-{dateString}-{sequentialNumber:D3}";
        }
    }
}
