using System;

namespace PhotoBooth.Models
{
    public class AppConfig
    {
        public ApiSettings ApiSettings { get; set; } = new();
        public PrintSettings PrintSettings { get; set; } = new();
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
}

