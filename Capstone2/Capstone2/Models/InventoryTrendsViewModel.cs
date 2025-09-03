using System;
using System.Collections.Generic;

namespace Capstone2.Models
{
    public class InventoryTrendsViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string GroupBy { get; set; } = "month"; // day | week | month | quarter | year
        public List<ConsumptionPeriod> Periods { get; set; } = new List<ConsumptionPeriod>();
        public InventorySummary Summary { get; set; } = new InventorySummary();
    }



    public class ConsumptionPeriod
    {
        public DateTime PeriodStart { get; set; }
        public string Label { get; set; }
        public int Consumed { get; set; }
        public int Returned { get; set; }
        public int Lost { get; set; }
        public int Damaged { get; set; }
        public int ReturnMaterials => Math.Max(0, Consumed - (Lost + Damaged));
    }

    public class InventorySummary
    {
        public int TotalMaterials { get; set; }
    }
}
