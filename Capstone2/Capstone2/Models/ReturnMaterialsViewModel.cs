using System.Collections.Generic;

namespace Capstone2.Models
{
    public class ReturnMaterialsViewModel
    {
        public int OrderId { get; set; }
        public int CustomerId { get; set; }
        public List<ReturnMaterialItem> Items { get; set; }
    }

    public class ReturnMaterialItem
    {
        public int MaterialId { get; set; }
        public string MaterialName { get; set; }
        public int PulledOut { get; set; }
        public int Returned { get; set; }
        public int Lost { get; set; }
        public int Damaged { get; set; }
        public decimal ChargePerItem { get; set; }
    }

    public class InventoryReportViewModel
    {
        public int OrderId { get; set; }
        public int CustomerId { get; set; }
        public List<InventoryReportItemViewModel> Items { get; set; }
    }

    public class InventoryReportItemViewModel
    {
        public int MaterialId { get; set; }
        public string MaterialName { get; set; }
        public int PulledOut { get; set; }
        public int Returned { get; set; }
        public int Lost { get; set; }
        public int Damaged { get; set; }
    }
}