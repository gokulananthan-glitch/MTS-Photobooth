using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Security.Cryptography;
using PhotoBooth.Models;
using static PhotoBooth.Services.ConfigService;

namespace PhotoBooth.Services
{
    public class RazorpayService
    {
        private readonly string _keyId;
        private readonly string _keySecret;
        private readonly string _currency;
        private readonly HttpClient _httpClient;

        public RazorpayService()
        {
            var config = GetConfig();
            _keyId = config.RazorpaySettings.KeyId;
            _keySecret = config.RazorpaySettings.KeySecret;
            _currency = config.RazorpaySettings.Currency;
            
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.razorpay.com/v1/")
            };
            
            // Set basic authentication header
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_keyId}:{_keySecret}"));
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
        }

        /// <summary>
        /// Creates a Razorpay order
        /// </summary>
        public async Task<RazorpayOrderResponse?> CreateOrderAsync(decimal amount, string orderId, string description = "PhotoBooth Payment")
        {
            try
            {
                var orderData = new
                {
                    amount = (int)(amount * 100), // Convert to paise (smallest currency unit)
                    currency = _currency,
                    receipt = orderId,
                    notes = new
                    {
                        description = description
                    }
                };

                var json = JsonSerializer.Serialize(orderData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("orders", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var orderResponse = JsonSerializer.Deserialize<RazorpayOrderResponse>(responseContent, options);
                    System.Diagnostics.Debug.WriteLine($"[Razorpay] Order created: {orderResponse?.Id}");
                    return orderResponse;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Razorpay] Failed to create order: {response.StatusCode} - {responseContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Razorpay] Error creating order: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Verifies payment signature
        /// </summary>
        public bool VerifyPaymentSignature(string orderId, string paymentId, string signature)
        {
            try
            {
                var payload = $"{orderId}|{paymentId}";
                var secret = Encoding.UTF8.GetBytes(_keySecret);
                
                using var hmac = new HMACSHA256(secret);
                var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var computedSignature = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                
                return computedSignature == signature.ToLower();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Razorpay] Error verifying signature: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets payment details
        /// </summary>
        public async Task<RazorpayPaymentResponse?> GetPaymentAsync(string paymentId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"payments/{paymentId}");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var paymentResponse = JsonSerializer.Deserialize<RazorpayPaymentResponse>(responseContent, options);
                    return paymentResponse;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Razorpay] Failed to get payment: {response.StatusCode} - {responseContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Razorpay] Error getting payment: {ex.Message}");
                return null;
            }
        }

        public string GetKeyId() => _keyId;
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

    public class RazorpayPaymentResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public int CreatedAt { get; set; }
    }
}

