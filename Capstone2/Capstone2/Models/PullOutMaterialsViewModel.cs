using System.Collections.Generic;

namespace Capstone2.Models
{
    public class PullOutMaterialsViewModel
    {
        public int CustomerId { get; set; }
        public int Pax { get; set; }
        public List<PullOutMaterialItemViewModel> Materials { get; set; }
    }

    public class PullOutMaterialItemViewModel
    {
        public int MaterialId { get; set; }
        public string Name { get; set; }
        public int CurrentQuantity { get; set; } // Inventory
        public int PullOutQuantity { get; set; } // Editable by user
    }
}