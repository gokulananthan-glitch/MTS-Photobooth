using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PhotoBooth.Models;

namespace PhotoBooth.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ApiService(string baseUrl, int timeoutSeconds = 30)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };
        }

        public async Task<MachineConfig?> GetMachineConfigAsync(string machineCode)
        {
            var startTime = DateTime.Now;
            string url = $"{_baseUrl}/api/machines/{machineCode}";
            
            Console.WriteLine($"[API CALL] GET {url}");
            Console.WriteLine($"[API CALL] Machine Code: {machineCode}");
            System.Diagnostics.Debug.WriteLine($"[API] Fetching machine config from: {url}");

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                
                Console.WriteLine($"[API RESPONSE] Status: {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine($"[API RESPONSE] Time: {elapsed:F2}ms");
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API ERROR] Response: {errorContent}");
                    System.Diagnostics.Debug.WriteLine($"[API] Error response: {response.StatusCode} - {errorContent}");
                    return null;
                }

                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[API RESPONSE] Body length: {json.Length} characters");
                System.Diagnostics.Debug.WriteLine($"[API] Response received: {json}");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var apiResponse = JsonSerializer.Deserialize<ApiResponse<MachineConfig>>(json, options);
                
                if (apiResponse?.Data != null)
                {
                    Console.WriteLine($"[API SUCCESS] Machine config loaded: {apiResponse.Data.MachineCode}");
                    System.Diagnostics.Debug.WriteLine($"[API] Machine config loaded: {apiResponse.Data.MachineCode}");
                    return apiResponse.Data;
                }

                Console.WriteLine($"[API WARNING] Response data is null");
                return null;
            }
            catch (HttpRequestException ex)
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Console.WriteLine($"[API EXCEPTION] HttpRequestException after {elapsed:F2}ms: {ex.Message}");
                Console.WriteLine($"[API EXCEPTION] StackTrace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"[API] HttpRequestException fetching machine config: {ex.Message}");
                return null;
            }
            catch (TaskCanceledException ex)
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Console.WriteLine($"[API EXCEPTION] Timeout after {elapsed:F2}ms: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[API] Timeout fetching machine config: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Console.WriteLine($"[API EXCEPTION] Error after {elapsed:F2}ms: {ex.Message}");
                Console.WriteLine($"[API EXCEPTION] Type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[API] Error fetching machine config: {ex.Message}");
                return null;
            }
        }

        public async Task<List<FrameTemplate>?> GetFrameTemplatesAsync(string machineCode)
        {
            var startTime = DateTime.Now;
            string url = $"{_baseUrl}/api/machine-frames/all-frames/{machineCode}?status=active";
            
            Console.WriteLine($"[API CALL] GET {url}");
            Console.WriteLine($"[API CALL] Machine Code: {machineCode}");
            System.Diagnostics.Debug.WriteLine($"[API] Fetching frame templates from: {url}");

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                
                Console.WriteLine($"[API RESPONSE] Status: {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine($"[API RESPONSE] Time: {elapsed:F2}ms");
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[API ERROR] Response: {errorContent}");
                    System.Diagnostics.Debug.WriteLine($"[API] Error response: {response.StatusCode} - {errorContent}");
                    return null;
                }

                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[API RESPONSE] Body length: {json.Length} characters");
                System.Diagnostics.Debug.WriteLine($"[API] Frames response received");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var apiResponse = JsonSerializer.Deserialize<FrameTemplatesResponse>(json, options);
                
                if (apiResponse?.Data != null)
                {
                    Console.WriteLine($"[API SUCCESS] {apiResponse.Data.Count} frame templates loaded");
                    System.Diagnostics.Debug.WriteLine($"[API] {apiResponse.Data.Count} frame templates loaded");
                    return apiResponse.Data;
                }

                Console.WriteLine($"[API WARNING] Response data is null");
                return null;
            }
            catch (HttpRequestException ex)
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Console.WriteLine($"[API EXCEPTION] HttpRequestException after {elapsed:F2}ms: {ex.Message}");
                Console.WriteLine($"[API EXCEPTION] StackTrace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"[API] HttpRequestException fetching frame templates: {ex.Message}");
                return null;
            }
            catch (TaskCanceledException ex)
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Console.WriteLine($"[API EXCEPTION] Timeout after {elapsed:F2}ms: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[API] Timeout fetching frame templates: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Console.WriteLine($"[API EXCEPTION] Error after {elapsed:F2}ms: {ex.Message}");
                Console.WriteLine($"[API EXCEPTION] Type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[API] Error fetching frame templates: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> UploadOfflineFrameAsync(string filePath, string frameId, string machineCode, string siteCode, DateTime createdAt, string? eventId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[API] File not found: {filePath}");
                    return false;
                }

                string url = $"{_baseUrl}/api/customers-frame/offline-frame";
                System.Diagnostics.Debug.WriteLine($"[API] Uploading offline frame: {frameId}");

                using var formData = new MultipartFormDataContent();
                
                // Add file
                var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                formData.Add(fileContent, "file", $"{frameId}.jpg");

                // Add form fields
                if (!string.IsNullOrEmpty(frameId))
                    formData.Add(new StringContent(frameId), "frame_id");
                
                if (!string.IsNullOrEmpty(machineCode))
                    formData.Add(new StringContent(machineCode), "machine_code");
                
                if (!string.IsNullOrEmpty(siteCode))
                    formData.Add(new StringContent(siteCode), "site_code");
                
                if (!string.IsNullOrEmpty(eventId))
                    formData.Add(new StringContent(eventId), "event_id");
                
                formData.Add(new StringContent(createdAt.ToString("O")), "createdAt");

                // Create timeout cancellation token (60 seconds)
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

                HttpResponseMessage response = await _httpClient.PostAsync(url, formData, timeoutCts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[API] Offline frame uploaded successfully: {frameId}");
                    return true;
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] Upload failed for {frameId}: {response.StatusCode} - {errorContent}");
                    return false;
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Upload timeout for {frameId}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Error uploading offline frame {frameId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CreateCompletedSaleTransactionAsync(TransactionData transaction, string? paymentMethod = null, CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"{_baseUrl}/api/sale-transaction/create-completed-sale-transaction";
                System.Diagnostics.Debug.WriteLine($"[API] Creating completed sale transaction: {transaction.OrderId}");

                // Format frame value - ensure it starts with "grid" if it doesn't
                string formattedFrame = transaction.Frame;
                if (!string.IsNullOrEmpty(formattedFrame))
                {
                    // If it's just a number, add "grid" prefix
                    if (int.TryParse(formattedFrame, out int frameNum))
                    {
                        formattedFrame = $"grid{frameNum}";
                    }
                    // If it doesn't start with "grid", add it
                    else if (!formattedFrame.StartsWith("grid", StringComparison.OrdinalIgnoreCase))
                    {
                        formattedFrame = $"grid{formattedFrame}";
                    }
                }

                // Prepare payload matching the React Native format
                var payload = new
                {
                    machine_code = transaction.MachineCode ?? "",
                    site_code = transaction.SiteCode ?? "",
                    frame = formattedFrame,
                    amount = transaction.Amount,
                    payment_method = paymentMethod ?? transaction.PaymentMode ?? "OFFLINE",
                    total_copies = transaction.TotalCopies,
                    total_amount = transaction.TotalAmount,
                    order_id = transaction.OrderId
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Create timeout cancellation token (60 seconds)
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

                HttpResponseMessage response = await _httpClient.PostAsync(url, content, timeoutCts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[API] Transaction created successfully: {transaction.OrderId}");
                    return true;
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] Transaction creation failed for {transaction.OrderId}: {response.StatusCode} - {errorContent}");
                    return false;
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Transaction creation timeout for {transaction.OrderId}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Error creating transaction {transaction.OrderId}: {ex.Message}");
                return false;
            }
        }

        public async Task<TransactionData?> CreatePaymentReceivedTransactionAsync(string orderId, string paymentMethod, CancellationToken cancellationToken = default)
        {
            try
            {
                // This endpoint should match your backend API
                // Based on React Native pattern, it might be something like:
                // POST /api/sale-transaction/payment-received
                string url = $"{_baseUrl}/api/sale-transaction/payment-received";
                System.Diagnostics.Debug.WriteLine($"[API] Creating payment received transaction: OrderId={orderId}, PaymentMethod={paymentMethod}");

                var payload = new
                {
                    order_id = orderId,
                    payment_method = paymentMethod
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Create timeout cancellation token (60 seconds)
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

                HttpResponseMessage response = await _httpClient.PostAsync(url, content, timeoutCts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    string responseJson = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] Payment received transaction created successfully: {responseJson}");

                    // Parse response
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };

                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<TransactionData>>(responseJson, options);
                    
                    if (apiResponse?.Data != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[API] Payment received transaction data: OrderId={apiResponse.Data.OrderId}");
                        return apiResponse.Data;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[API] Payment received response data is null");
                        return null;
                    }
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] Payment received transaction creation failed: {response.StatusCode} - {errorContent}");
                    return null;
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Payment received transaction creation timeout");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Error creating payment received transaction: {ex.Message}");
                return null;
            }
        }

        public async Task<RazorpayOrderResponse?> CreateRazorpayOrderAsync(double amount, string currency = "INR", CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"{_baseUrl}/api/payment/create-order";
                System.Diagnostics.Debug.WriteLine($"[API] Creating Razorpay order: amount={amount}, currency={currency}");

                var payload = new
                {
                    amount = (int)(amount * 100), // Razorpay expects amount in smallest currency unit (paise)
                    currency = currency
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Create timeout cancellation token (60 seconds)
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

                HttpResponseMessage response = await _httpClient.PostAsync(url, content, timeoutCts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    string responseJson = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] Razorpay order created successfully: {responseJson}");

                    // Parse response
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<RazorpayOrderResponse>>(responseJson, options);
                    
                    if (apiResponse?.Data != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[API] Razorpay order ID: {apiResponse.Data.Id}");
                        return apiResponse.Data;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[API] Razorpay order response data is null");
                        return null;
                    }
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] Razorpay order creation failed: {response.StatusCode} - {errorContent}");
                    return null;
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Razorpay order creation timeout");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Error creating Razorpay order: {ex.Message}");
                return null;
            }
        }

        public async Task<TransactionData?> CreateProcessingSaleTransactionAsync(string machineCode, string siteCode, string frame, double amount, string paymentMethod, int totalCopies, double totalAmount, string orderId, CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"{_baseUrl}/api/sale-transaction/create-processing-sale-transaction";
                System.Diagnostics.Debug.WriteLine($"[API] Creating processing sale transaction: orderId={orderId}, paymentMethod={paymentMethod}");

                var payload = new
                {
                    machine_code = machineCode,
                    site_code = siteCode,
                    frame = frame,
                    amount = amount,
                    payment_method = paymentMethod,
                    total_copies = totalCopies,
                    total_amount = totalAmount,
                    order_id = orderId
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

                HttpResponseMessage response = await _httpClient.PostAsync(url, content, timeoutCts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    string responseJson = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] Processing transaction created successfully: {responseJson}");

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };

                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<TransactionData>>(responseJson, options);
                    
                    if (apiResponse?.Data != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[API] Processing transaction data: OrderId={apiResponse.Data.OrderId}");
                        return apiResponse.Data;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[API] Processing transaction response data is null");
                        return null;
                    }
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] Processing transaction creation failed: {response.StatusCode} - {errorContent}");
                    return null;
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Processing transaction creation timeout");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Error creating processing transaction: {ex.Message}");
                return null;
            }
        }

        public async Task<TransactionData?> CreatePaymentReceivedSaleTransactionAsync(string machineCode, string siteCode, string frame, double amount, string paymentMethod, int totalCopies, double totalAmount, string orderId, string? razorPayOrderId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"{_baseUrl}/api/sale-transaction/create-payment-received-sale-transaction";
                System.Diagnostics.Debug.WriteLine($"[API] Creating payment received sale transaction: orderId={orderId}, paymentMethod={paymentMethod}");

                var payload = new
                {
                    machine_code = machineCode,
                    site_code = siteCode,
                    frame = frame,
                    amount = amount,
                    payment_method = paymentMethod,
                    total_copies = totalCopies,
                    total_amount = totalAmount,
                    order_id = orderId,
                    razor_pay_order_id = razorPayOrderId
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

                HttpResponseMessage response = await _httpClient.PostAsync(url, content, timeoutCts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    string responseJson = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] Payment received transaction created successfully: {responseJson}");

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };

                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<TransactionData>>(responseJson, options);
                    
                    if (apiResponse?.Data != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[API] Payment received transaction data: OrderId={apiResponse.Data.OrderId}");
                        return apiResponse.Data;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[API] Payment received transaction response data is null");
                        return null;
                    }
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] Payment received transaction creation failed: {response.StatusCode} - {errorContent}");
                    return null;
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Payment received transaction creation timeout");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Error creating payment received transaction: {ex.Message}");
                return null;
            }
        }

        public async Task<TransactionData?> CreateCancelledSaleTransactionAsync(string machineCode, string siteCode, string frame, double amount, string paymentMethod, int totalCopies, double totalAmount, string orderId, CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"{_baseUrl}/api/sale-transaction/create-cancelled-sale-transaction";
                System.Diagnostics.Debug.WriteLine($"[API] Creating cancelled sale transaction: orderId={orderId}, paymentMethod={paymentMethod}");

                var payload = new
                {
                    machine_code = machineCode,
                    site_code = siteCode,
                    frame = frame,
                    amount = amount,
                    payment_method = paymentMethod,
                    total_copies = totalCopies,
                    total_amount = totalAmount,
                    order_id = orderId
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

                HttpResponseMessage response = await _httpClient.PostAsync(url, content, timeoutCts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    string responseJson = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] Cancelled transaction created successfully: {responseJson}");

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };

                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<TransactionData>>(responseJson, options);
                    
                    if (apiResponse?.Data != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[API] Cancelled transaction data: OrderId={apiResponse.Data.OrderId}");
                        return apiResponse.Data;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[API] Cancelled transaction response data is null");
                        return null;
                    }
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] Cancelled transaction creation failed: {response.StatusCode} - {errorContent}");
                    return null;
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Cancelled transaction creation timeout");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Error creating cancelled transaction: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> VerifyOtpAsync(string machineCode, string otp, CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"{_baseUrl}/api/machines/verify-otp/{machineCode}";
                System.Diagnostics.Debug.WriteLine($"[API] Verifying OTP: machineCode={machineCode}");

                var payload = new
                {
                    otp = otp
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

                HttpResponseMessage response = await _httpClient.PostAsync(url, content, timeoutCts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    string responseJson = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] OTP verification response: {responseJson}");

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<bool>>(responseJson, options);
                    
                    return apiResponse?.Data == true;
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] OTP verification failed: {response.StatusCode} - {errorContent}");
                    return false;
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine($"[API] OTP verification timeout");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Error verifying OTP: {ex.Message}");
                return false;
            }
        }

        public async Task<TransactionData?> CreatePendingSaleTransactionAsync(int frameCount, double amount, string machineCode, string siteCode, CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"{_baseUrl}/api/sale-transaction/create-pending-sale-transaction";
                System.Diagnostics.Debug.WriteLine($"[API] Creating pending sale transaction: frameCount={frameCount}, amount={amount}");

                // Prepare payload matching the React Native format
                var payload = new
                {
                    machine_code = machineCode,
                    site_code = siteCode,
                    frame = $"grid{frameCount}",
                    amount = amount
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Create timeout cancellation token (60 seconds)
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

                HttpResponseMessage response = await _httpClient.PostAsync(url, content, timeoutCts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    string responseJson = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] Pending transaction created successfully: {responseJson}");

                    // Parse response
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };

                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<TransactionData>>(responseJson, options);
                    
                    if (apiResponse?.Data != null)
                    {
                        // Log all fields to debug
                        System.Diagnostics.Debug.WriteLine($"[API] Transaction data received: OrderId='{apiResponse.Data.OrderId}', MachineCode='{apiResponse.Data.MachineCode}', SiteCode='{apiResponse.Data.SiteCode}', PaymentMode='{apiResponse.Data.PaymentMode}'");
                        
                        // Validate OrderId is not empty
                        if (string.IsNullOrEmpty(apiResponse.Data.OrderId))
                        {
                            System.Diagnostics.Debug.WriteLine($"[API] WARNING: OrderId is empty in API response. Full response: {responseJson}");
                            // Try to extract order_id directly from response if it's in a different format
                            try
                            {
                                using (var doc = JsonDocument.Parse(responseJson))
                                {
                                    var root = doc.RootElement;
                                    if (root.TryGetProperty("data", out var dataElement))
                                    {
                                        if (dataElement.TryGetProperty("order_id", out var orderIdElement))
                                        {
                                            apiResponse.Data.OrderId = orderIdElement.GetString() ?? "";
                                            System.Diagnostics.Debug.WriteLine($"[API] Extracted OrderId from order_id field: '{apiResponse.Data.OrderId}'");
                                        }
                                    }
                                }
                            }
                            catch (Exception parseEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[API] Error parsing response for OrderId: {parseEx.Message}");
                            }
                        }
                        
                        return apiResponse.Data;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[API] Response data is null. Full response: {responseJson}");
                        return null;
                    }
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] Pending transaction creation failed: {response.StatusCode} - {errorContent}");
                    return null;
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Pending transaction creation timeout");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Error creating pending transaction: {ex.Message}");
                return null;
            }
        }
    }
}

