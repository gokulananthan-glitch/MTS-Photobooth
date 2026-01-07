using System;
using System.Text.Json.Serialization;

namespace PhotoBooth.Models
{
    public class TransactionData
    {
        public int Id { get; set; }
        
        [JsonPropertyName("order_id")]
        public string OrderId { get; set; } = string.Empty;
        
        [JsonPropertyName("machine_code")]
        public string MachineCode { get; set; } = string.Empty;
        
        [JsonPropertyName("site_code")]
        public string SiteCode { get; set; } = string.Empty;
        
        public string Frame { get; set; } = string.Empty;
        
        public double Amount { get; set; }
        
        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }
        
        [JsonPropertyName("sale_date")]
        public DateTime? SaleDate { get; set; }
        
        [JsonPropertyName("payment_mode")]
        public string PaymentMode { get; set; } = string.Empty;
        
        [JsonPropertyName("payment_method")]
        public string? PaymentMethod { get; set; }
        
        [JsonPropertyName("total_copies")]
        public int TotalCopies { get; set; }
        
        [JsonPropertyName("total_amount")]
        public double TotalAmount { get; set; }
        
        [JsonPropertyName("event_id")]
        public string? EventId { get; set; }
        
        [JsonPropertyName("on_event")]
        public string? OnEvent { get; set; }
    }
}

