using System;
using System.Collections.Generic;

namespace Capstone2.Models
{
    public class MaterialPullOut
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public DateTime Date { get; set; }
        public List<MaterialPullOutItem> Items { get; set; }
    }

    public class MaterialPullOutItem
    {
        public int Id { get; set; }
        public int MaterialPullOutId { get; set; }
        public string MaterialName { get; set; }
        public int Quantity { get; set; }
    }
}