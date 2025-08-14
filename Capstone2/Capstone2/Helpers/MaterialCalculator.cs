using System;
using System.Collections.Generic;

namespace Capstone2.Helpers
{
    public static class MaterialCalculator
    {
        public static int GetSuggestedQuantity(string materialName, int pax)
        {
            int tables = (int)Math.Ceiling(pax / 5.0);
            int lechonTrays = (int)Math.Ceiling(pax / 50.0);
            switch (materialName)
            {
                case "Dining Plate":
                case "Fork":
                case "Spoon":
                case "Knife":
                case "Chair":
                case "Teaspoon":
                case "High Ball Glass":
                case "Goblet/Wine Glass":
                case "Small Soup Bowl":
                case "Big Salad Bowl":
                case "Drinking Straw":
                case "Table napkins":
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
                    return lechonTrays;
                default:
                    return 0;
            }
        }
    }
}