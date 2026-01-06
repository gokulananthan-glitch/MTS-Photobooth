using System;
using System.IO;
using System.Text.Json;
using PhotoBooth.Models;

namespace PhotoBooth.Services
{
    public class ConfigService
    {
        private static AppConfig? _config;
        private static readonly string ConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "appsettings.json");

        public static AppConfig GetConfig()
        {
            if (_config != null)
                return _config;

            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    _config = JsonSerializer.Deserialize<AppConfig>(json, options);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error loading config: {ex.Message}");
            }

            // Return default config if file doesn't exist or failed to load
            return _config ??= new AppConfig();
        }

        public static void SaveConfig(AppConfig config)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(ConfigPath, json);
                _config = config;
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Config saved to: {ConfigPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error saving config: {ex.Message}");
            }
        }
    }
}

