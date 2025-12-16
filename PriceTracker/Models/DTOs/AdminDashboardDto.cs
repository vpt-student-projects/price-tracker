namespace PriceTracker.Models.DTOs
{
    public class AdminDashboardDto
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalProducts { get; set; }
        public int ActiveProducts { get; set; }
        public int TotalPriceRecords { get; set; }
        public int TodayUpdates { get; set; }
        public DateTime ServerTime { get; set; }
        public string Uptime { get; set; } = string.Empty;
    }
}