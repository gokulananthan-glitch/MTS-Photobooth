using System;

namespace PhotoBooth.Models
{
    public class AppConfig
    {
        public ApiSettings ApiSettings { get; set; } = new();
        public PrintSettings PrintSettings { get; set; } = new();
        public RazorpaySettings RazorpaySettings { get; set; } = new();
    }

    public class ApiSettings
    {
        public string BaseUrl { get; set; } = "https://your-api-url.com";
        public string MachineCode { get; set; } = "M100";
        public string SiteCode { get; set; } = "9000";
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class PrintSettings
    {
        public string HotPrintingFolder { get; set; } = @"C:\HotPrinting";
    }

    public class RazorpaySettings
    {
        public string KeyId { get; set; } = string.Empty;
        public string KeySecret { get; set; } = string.Empty;
        public string Currency { get; set; } = "INR";
    }

    public class RazorpayOrderResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Receipt { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int CreatedAt { get; set; }
    }
}

