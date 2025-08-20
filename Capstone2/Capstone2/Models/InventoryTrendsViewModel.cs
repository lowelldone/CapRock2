using System;
using System.Collections.Generic;

namespace Capstone2.Models
{
    public class InventoryTrendsViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string GroupBy { get; set; } = "month"; // day | week | month | quarter | year
        public List<MaterialConsumptionTrend> MaterialTrends { get; set; } = new List<MaterialConsumptionTrend>();
        public List<ConsumptionPeriod> Periods { get; set; } = new List<ConsumptionPeriod>();
        public InventorySummary Summary { get; set; } = new InventorySummary();
    }

    public class MaterialConsumptionTrend
    {
        public int MaterialId { get; set; }
        public string MaterialName { get; set; }
        public bool IsConsumable { get; set; }
        public int TotalConsumption { get; set; }
        public int TotalLoss { get; set; }
        public int TotalDamage { get; set; }
        public int OrderCount { get; set; }
        public double AverageConsumptionPerOrder { get; set; }
        public double LossRate => TotalConsumption > 0 ? (double)TotalLoss / TotalConsumption * 100 : 0;
        public double DamageRate => TotalConsumption > 0 ? (double)TotalDamage / TotalConsumption * 100 : 0;
        public List<ConsumptionPeriod> ConsumptionByPeriod { get; set; } = new List<ConsumptionPeriod>();
    }

    public class ConsumptionPeriod
    {
        public DateTime PeriodStart { get; set; }
        public string Label { get; set; }
        public int OrderCount { get; set; }
        public int Consumed { get; set; }
        public int Returned { get; set; }
        public int Lost { get; set; }
        public int Damaged { get; set; }
        public int NetConsumption => Consumed - Returned;
        public double AverageConsumptionPerOrder => OrderCount > 0 ? (double)Consumed / OrderCount : 0;
    }

    public class InventorySummary
    {
        public int TotalMaterials { get; set; }
        public int ConsumableMaterials { get; set; }
        public int NonConsumableMaterials { get; set; }
        public int TotalOrders { get; set; }
        public int TotalConsumption { get; set; }
        public int TotalLoss { get; set; }
        public int TotalDamage { get; set; }
        public double OverallLossRate => TotalConsumption > 0 ? (double)TotalLoss / TotalConsumption * 100 : 0;
        public double OverallDamageRate => TotalConsumption > 0 ? (double)TotalDamage / TotalConsumption * 100 : 0;
        public double AverageMaterialsPerOrder => TotalOrders > 0 ? (double)TotalConsumption / TotalOrders : 0;
    }
}
