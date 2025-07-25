using System.Collections.Generic;

namespace Capstone2.Models
{
    public class PullOutMaterialsViewModel
    {
        public int CustomerId { get; set; }
        public int Pax { get; set; }
        public Dictionary<string, int> Materials { get; set; }
    }
}