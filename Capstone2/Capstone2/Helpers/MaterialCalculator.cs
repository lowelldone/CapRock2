using System;
using System.Collections.Generic;

namespace Capstone2.Helpers
{
    public static class MaterialCalculator
    {
        public static int GetSuggestedQuantity(string materialName, int pax)
        {
            return GetSuggestedQuantity(materialName, pax, 0);
        }
        public static int GetSuggestedQuantity(string materialName, int pax, int lechonCount)
        {
            int tables = (int)Math.Ceiling(pax / 5.0);
            int lechonTraysByPax = (int)Math.Ceiling(pax / 50.0);
            switch (materialName)
            {
                case "Dining Plate":
                case "Fork":
                case "Spoon":
                case "Chair":
                case "High Ball Glass":
                case "Goblet/Wine Glass":
                case "Drinking Straw":
                case "Table napkins":
                //case "Knife":
                //case "Teaspoon":
                //case "Small Soup Bowl":
                //case "Big Salad Bowl":
                    return pax;
                case "Tissue":
                    return pax * 2;
                case "Toothpicks":
                case "Tables":
                case "Pitcher":
                case "Round Bar Tray or Oval Tray":
                case "Tray Stand":
                    return tables;
                case "Tongs":
                    return tables * 2;
                case "Lechon Tray":
                    // Use the exact count of lechon ordered for this specific order when provided; fallback to pax-based heuristic
                    return lechonCount > 0 ? lechonCount : lechonTraysByPax;
                default:
                    return 0;
            }
        }
    }
}