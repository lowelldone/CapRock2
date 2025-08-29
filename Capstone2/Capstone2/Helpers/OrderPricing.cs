using System;
using System.Linq;
using Capstone2.Data;
using Capstone2.Models;

namespace Capstone2.Helpers
{
     public static class OrderPricing
     {
         public class Result
         {
             public double BaseTotal { get; set; }
             public double RushFee { get; set; }
             public double Total { get; set; }
         }

         public static Result Compute(Order order, ApplicationDbContext context)
         {
                 if (order == null) throw new ArgumentNullException(nameof(order));
    
                double baseTotal = 0d;
    
                bool isPackageOrder = order.OrderDetails != null && order.OrderDetails.Any(od => od.MenuPackageId != null);
                 if (isPackageOrder)
                     {
                         // Determine package price (prefer live MenuPackage, fallback to stored PackagePrice, then DB lookup)
                        var pkgMeta = order.OrderDetails.FirstOrDefault(od => od.MenuPackageId != null);
                        double packagePrice = 0d;
                        if (pkgMeta != null)
                             {
                                 if (pkgMeta.MenuPackage != null)
                                     {
                                        packagePrice = pkgMeta.MenuPackage.Price;
                                     }
                                 else if (pkgMeta.PackagePrice.HasValue)
                                     {
                                        packagePrice = (double)pkgMeta.PackagePrice.Value;
                                     }
                                 else if (pkgMeta.MenuPackageId.HasValue)
                                     {
                                        var mp = context.MenuPackages.FirstOrDefault(m => m.MenuPackageId == pkgMeta.MenuPackageId.Value);
                                         if (mp != null) packagePrice = mp.Price;
                                     }
                             }
        
                        double extras = 0d;
                         foreach (var od in order.OrderDetails)
                             {
                                 if (!od.IsFreeLechon && string.Equals(od.Type, "Package Extra", StringComparison.OrdinalIgnoreCase))
                                     {
                                         if (od.Menu != null)
                                             {
                                                extras += od.Menu.Price;
                                             }
                                         else
                                             {
                                                var m = context.Menu.FirstOrDefault(x => x.MenuId == od.MenuId);
                                                 if (m != null) extras += m.Price;
                                             }
                                     }
                             }
        
                        baseTotal = (packagePrice * order.NoOfPax) + extras;
                     }
                 else if (order.OrderDetails != null)
                     {
                         foreach (var od in order.OrderDetails)
                             {
                                var unit = od.Menu != null ? od.Menu.Price : (context.Menu.FirstOrDefault(x => x.MenuId == od.MenuId)?.Price ?? 0d);
                                baseTotal += unit * od.Quantity;
                             }
                     }
    
                bool isRush = order.OrderDate.Date == order.CateringDate.Date;
                double rushFee = isRush ? baseTotal * 0.10 : 0d;
    
                 return new Result
                 {
                    BaseTotal = baseTotal,
                    RushFee = rushFee,
                    Total = baseTotal + rushFee
             };
         }
     }
 }


