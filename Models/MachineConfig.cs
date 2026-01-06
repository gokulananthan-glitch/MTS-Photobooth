using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PhotoBooth.Models
{
    public class MachineConfig
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;
        public string MachineCode { get; set; } = string.Empty;
        public string PaymentType { get; set; } = string.Empty;
        public string SiteCode { get; set; } = string.Empty;
        public string Active { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        
        [JsonPropertyName("supported_frames")]
        public List<SupportedFrame> SupportedFrames { get; set; } = new();
        public int Timer { get; set; }
        public string? PaymentMode { get; set; }
        public string ImageTimer { get; set; } = "5";
        public string MachineOtp { get; set; } = string.Empty;
        public bool OfflineMode { get; set; }
        public string OnEvent { get; set; } = "false";
        public string? EventId { get; set; }
    }

    public class SupportedFrame
    {
        public string Type { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
    }

    public class ApiResponse<T>
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
    }
}

