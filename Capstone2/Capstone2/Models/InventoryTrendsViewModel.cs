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

        // Separate tracking for consumable vs non-consumable materials
        public int ConsumableConsumed { get; set; }
        public int ConsumableLost { get; set; }
        public int ConsumableDamaged { get; set; }
        public int NonConsumableConsumed { get; set; }
        public int NonConsumableLost { get; set; }
        public int NonConsumableDamaged { get; set; }
        public int NonConsumableReturned { get; set; }

        // Return Materials should only include non-consumable materials
        public int ReturnMaterials => Math.Max(0, NonConsumableConsumed - (NonConsumableLost + NonConsumableDamaged));
    }

    public class InventorySummary
    {
        public int TotalMaterials { get; set; }
    }
}
