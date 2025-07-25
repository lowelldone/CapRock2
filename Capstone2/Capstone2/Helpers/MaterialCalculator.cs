using System;
using System.Collections.Generic;

namespace Capstone2.Helpers
{
    public static class MaterialCalculator
    {
        public static Dictionary<string, int> CalculateMaterials(int pax)
        {
            int tables = (int)Math.Ceiling(pax / 5.0);
            int lechonTrays = (int)Math.Ceiling(pax / 50.0);

            return new Dictionary<string, int>
            {
                ["Dining Plate"] = pax,
                ["VIP Plate"] = 0,
                ["Fork"] = pax,
                ["Spoon"] = pax,
                ["Knife"] = pax,
                ["Chair"] = pax,
                ["VIP Spoon"] = 0,
                ["VIP Fork"] = 0,
                ["Serving Spoon"] = 0,
                ["Serving Fork"] = 0,
                ["Teaspoon"] = pax,
                ["High Ball Glass"] = pax,
                ["Goblet/Wine Glass"] = pax,
                ["Small Soup Bowl"] = pax,
                ["Big Salad Bowl"] = pax,
                ["Drinking Straw"] = pax,
                ["Tissue"] = pax * 2,
                ["Toothpicks"] = pax,
                ["Table napkins"] = tables,
                ["Pitcher"] = tables,
                ["Round Bar Tray or Oval Tray"] = tables,
                ["Tray Stand"] = tables,
                ["Tongs"] = tables * 2,
                ["Lechon Tray"] = lechonTrays
            };
        }
    }
}