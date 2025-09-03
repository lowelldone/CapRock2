using System;
using System.Collections.Generic;

namespace Capstone2.Models
{
    public class SalesPeriodItem
    {
        public DateTime PeriodStart { get; set; }
        public string Label { get; set; } = string.Empty;
        public int NumberOfOrders { get; set; }
        public int NumberOfTransactions { get; set; }
        public double GrandTotal { get; set; }
    }

    public class SalesReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string GroupBy { get; set; } = "day"; // day | week | month
        public List<SalesPeriodItem> Periods { get; set; } = new List<SalesPeriodItem>();
        public int TotalOrders { get; set; }
        public int TotalTransactions { get; set; }
        public double GrandTotal { get; set; }
    }

    public class TrendsPeriodItem
    {
        public DateTime PeriodStart { get; set; }
        public string Label { get; set; } = string.Empty;
        public int OrdersCount { get; set; }
        public int PaxTotal { get; set; }
        public double RevenueTotal { get; set; }
    }

    public class TrendsReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string GroupBy { get; set; } = "day";
        public List<TrendsPeriodItem> Periods { get; set; } = new List<TrendsPeriodItem>();
        public int PendingCount { get; set; }
        public int AcceptedCount { get; set; }
        public int OngoingCount { get; set; }
        public int CompletedCount { get; set; }
        public int CancelledCount { get; set; }
        public double AcceptanceRate => (PendingCount + AcceptedCount) == 0 ? 0 : (double)AcceptedCount / (PendingCount + AcceptedCount);
        public double CompletionRate => (AcceptedCount == 0) ? 0 : (double)CompletedCount / AcceptedCount;
    }

    public class PreferencesItem
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public double Revenue { get; set; }
    }

    public class AveragePaxItem
    {
        public string Name { get; set; } = string.Empty;
        public double AveragePax { get; set; }
    }

    public class RepeatCustomerItem
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int OrdersCount { get; set; }
    }

    public class PreferencesReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<PreferencesItem> TopMenusByQuantity { get; set; } = new List<PreferencesItem>();
        public List<AveragePaxItem> AveragePaxByOccasion { get; set; } = new List<AveragePaxItem>();
        public List<AveragePaxItem> AveragePaxByVenue { get; set; } = new List<AveragePaxItem>();
        public List<RepeatCustomerItem> RepeatCustomers { get; set; } = new List<RepeatCustomerItem>();
    }
}


