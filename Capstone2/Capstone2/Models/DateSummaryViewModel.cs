using System;
using System.Collections.Generic;

namespace Capstone2.Models
{
    public class DateSummaryViewModel
    {
        public DateTime Date { get; set; }
        public int TotalPax { get; set; }
        public bool HasLargeOrder { get; set; }
        public int OrderCount { get; set; }
    }

    public class DateSummaryPageViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<DateSummaryViewModel> DateSummary { get; set; } = new List<DateSummaryViewModel>();
        public List<DateSummaryViewModel> AllDateSummary { get; set; } = new List<DateSummaryViewModel>();
    }
}