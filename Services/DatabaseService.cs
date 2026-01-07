using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PhotoBooth.Models;

namespace PhotoBooth.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;
        private const string ConnectionString = "Data Source=photobooth.db";

        public DatabaseService()
        {
            _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "photobooth.db");
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            // Enable foreign key constraints
            var pragmaCommand = connection.CreateCommand();
            pragmaCommand.CommandText = "PRAGMA foreign_keys = ON";
            pragmaCommand.ExecuteNonQuery();

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS MachineConfig (
                    Id TEXT PRIMARY KEY,
                    MachineCode TEXT NOT NULL,
                    PaymentType TEXT,
                    SiteCode TEXT,
                    Active TEXT,
                    CreatedAt TEXT,
                    ModifiedAt TEXT,
                    Timer INTEGER,
                    PaymentMode TEXT,
                    ImageTimer TEXT,
                    MachineOtp TEXT,
                    OfflineMode INTEGER,
                    OnEvent TEXT,
                    EventId TEXT,
                    SavedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS SupportedFrames (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    MachineConfigId TEXT NOT NULL,
                    Type TEXT NOT NULL,
                    Amount TEXT NOT NULL,
                    FOREIGN KEY (MachineConfigId) REFERENCES MachineConfig(Id)
                );

                CREATE TABLE IF NOT EXISTS FrameTemplates (
                    Id TEXT PRIMARY KEY,
                    MachineCode TEXT NOT NULL,
                    Frame TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    Image TEXT NOT NULL,
                    SiteCode TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    SavedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS AppSettings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS OfflineFrames (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    frame_id TEXT NOT NULL UNIQUE,
                    machine_code TEXT NOT NULL,
                    site_code TEXT NOT NULL,
                    filePath TEXT NOT NULL,
                    createdAt TEXT NOT NULL,
                    event_id TEXT
                );

                CREATE TABLE IF NOT EXISTS TransactionData (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    order_id TEXT NOT NULL UNIQUE,
                    machine_code TEXT NOT NULL,
                    site_code TEXT NOT NULL,
                    frame TEXT NOT NULL,
                    amount REAL NOT NULL,
                    created_at TEXT NOT NULL,
                    sale_date TEXT NOT NULL,
                    payment_mode TEXT NOT NULL,
                    total_copies INTEGER NOT NULL,
                    total_amount REAL NOT NULL,
                    event_id TEXT,
                    on_event TEXT
                );
            ";
            
            // Add SavedAt column if it doesn't exist (for existing databases)
            try
            {
                var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE MachineConfig ADD COLUMN SavedAt TEXT";
                alterCommand.ExecuteNonQuery();
            }
            catch
            {
                // Column already exists, ignore
            }

            // Add event_id column to OfflineFrames if it doesn't exist (for existing databases)
            try
            {
                var alterCommand2 = connection.CreateCommand();
                alterCommand2.CommandText = "ALTER TABLE OfflineFrames ADD COLUMN event_id TEXT";
                alterCommand2.ExecuteNonQuery();
            }
            catch
            {
                // Column already exists, ignore
            }

            command.ExecuteNonQuery();
        }

        public async Task SaveMachineConfigAsync(MachineConfig config)
        {
            await Task.Run(() =>
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Enable foreign key constraints for this transaction
                    var pragmaCommand = connection.CreateCommand();
                    pragmaCommand.Transaction = transaction;
                    pragmaCommand.CommandText = "PRAGMA foreign_keys = ON";
                    pragmaCommand.ExecuteNonQuery();

                    // Clean up any orphaned SupportedFrames (ones that reference non-existent MachineConfig)
                    var cleanupOrphanedCommand = connection.CreateCommand();
                    cleanupOrphanedCommand.Transaction = transaction;
                    cleanupOrphanedCommand.CommandText = @"
                        DELETE FROM SupportedFrames 
                        WHERE MachineConfigId NOT IN (SELECT Id FROM MachineConfig)
                    ";
                    int orphanedDeleted = cleanupOrphanedCommand.ExecuteNonQuery();
                    if (orphanedDeleted > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] Cleaned up {orphanedDeleted} orphaned SupportedFrames");
                    }

                    // Delete ALL SupportedFrames associated with this MachineCode
                    // Use a subquery to find all MachineConfigIds for this MachineCode
                    var deleteFramesCommand = connection.CreateCommand();
                    deleteFramesCommand.Transaction = transaction;
                    deleteFramesCommand.CommandText = @"
                        DELETE FROM SupportedFrames 
                        WHERE MachineConfigId IN (
                            SELECT Id FROM MachineConfig WHERE MachineCode = $machineCode
                        )
                    ";
                    deleteFramesCommand.Parameters.AddWithValue("$machineCode", config.MachineCode);
                    int deletedFrames = deleteFramesCommand.ExecuteNonQuery();
                    System.Diagnostics.Debug.WriteLine($"[DB] Deleted {deletedFrames} SupportedFrames for MachineCode: {config.MachineCode}");

                    // Now delete existing config (safe because all SupportedFrames are deleted)
                    var deleteCommand = connection.CreateCommand();
                    deleteCommand.Transaction = transaction;
                    deleteCommand.CommandText = "DELETE FROM MachineConfig WHERE MachineCode = $machineCode";
                    deleteCommand.Parameters.AddWithValue("$machineCode", config.MachineCode);
                    deleteCommand.ExecuteNonQuery();

                    // Insert new config with current timestamp
                    var savedAt = DateTime.UtcNow;
                    var insertCommand = connection.CreateCommand();
                    insertCommand.Transaction = transaction;
                    insertCommand.CommandText = @"
                        INSERT INTO MachineConfig 
                        (Id, MachineCode, PaymentType, SiteCode, Active, CreatedAt, ModifiedAt, 
                         Timer, PaymentMode, ImageTimer, MachineOtp, OfflineMode, OnEvent, EventId, SavedAt)
                        VALUES 
                        ($id, $machineCode, $paymentType, $siteCode, $active, $createdAt, $modifiedAt,
                         $timer, $paymentMode, $imageTimer, $machineOtp, $offlineMode, $onEvent, $eventId, $savedAt)
                    ";

                    insertCommand.Parameters.AddWithValue("$id", config.Id);
                    insertCommand.Parameters.AddWithValue("$machineCode", config.MachineCode);
                    insertCommand.Parameters.AddWithValue("$paymentType", config.PaymentType);
                    insertCommand.Parameters.AddWithValue("$siteCode", config.SiteCode);
                    insertCommand.Parameters.AddWithValue("$active", config.Active);
                    insertCommand.Parameters.AddWithValue("$createdAt", config.CreatedAt.ToString("O"));
                    insertCommand.Parameters.AddWithValue("$modifiedAt", config.ModifiedAt.ToString("O"));
                    insertCommand.Parameters.AddWithValue("$timer", config.Timer);
                    insertCommand.Parameters.AddWithValue("$paymentMode", config.PaymentMode ?? (object)DBNull.Value);
                    insertCommand.Parameters.AddWithValue("$imageTimer", config.ImageTimer);
                    insertCommand.Parameters.AddWithValue("$machineOtp", config.MachineOtp);
                    insertCommand.Parameters.AddWithValue("$offlineMode", config.OfflineMode ? 1 : 0);
                    insertCommand.Parameters.AddWithValue("$onEvent", config.OnEvent);
                    insertCommand.Parameters.AddWithValue("$eventId", config.EventId ?? (object)DBNull.Value);
                    insertCommand.Parameters.AddWithValue("$savedAt", savedAt.ToString("O"));

                    insertCommand.ExecuteNonQuery();

                    // Insert new supported frames
                    var frameCommand = connection.CreateCommand();
                    frameCommand.Transaction = transaction;
                    frameCommand.CommandText = @"
                        INSERT INTO SupportedFrames (MachineConfigId, Type, Amount)
                        VALUES ($machineConfigId, $type, $amount)
                    ";

                    foreach (var frame in config.SupportedFrames)
                    {
                        frameCommand.Parameters.Clear();
                        frameCommand.Parameters.AddWithValue("$machineConfigId", config.Id);
                        frameCommand.Parameters.AddWithValue("$type", frame.Type);
                        frameCommand.Parameters.AddWithValue("$amount", frame.Amount);
                        frameCommand.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    System.Diagnostics.Debug.WriteLine($"[DB] Machine config saved: {config.MachineCode}");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    System.Diagnostics.Debug.WriteLine($"[DB] Error saving machine config: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[DB] Stack trace: {ex.StackTrace}");
                    throw;
                }
            });
        }

        public async Task<MachineConfig?> GetMachineConfigAsync()
        {
            return await Task.Run(() =>
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, MachineCode, PaymentType, SiteCode, Active, CreatedAt, ModifiedAt,
                           Timer, PaymentMode, ImageTimer, MachineOtp, OfflineMode, OnEvent, EventId, SavedAt
                    FROM MachineConfig
                    LIMIT 1
                ";

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    var config = new MachineConfig
                    {
                        Id = reader.GetString(0),
                        MachineCode = reader.GetString(1),
                        PaymentType = reader.GetString(2),
                        SiteCode = reader.GetString(3),
                        Active = reader.GetString(4),
                        CreatedAt = DateTime.Parse(reader.GetString(5)),
                        ModifiedAt = DateTime.Parse(reader.GetString(6)),
                        Timer = reader.GetInt32(7),
                        PaymentMode = reader.IsDBNull(8) ? null : reader.GetString(8),
                        ImageTimer = reader.GetString(9),
                        MachineOtp = reader.GetString(10),
                        OfflineMode = reader.GetInt32(11) == 1,
                        OnEvent = reader.GetString(12),
                        EventId = reader.IsDBNull(13) ? null : reader.GetString(13)
                    };

                    // Load supported frames
                    var frameCommand = connection.CreateCommand();
                    frameCommand.CommandText = "SELECT Type, Amount FROM SupportedFrames WHERE MachineConfigId = $id";
                    frameCommand.Parameters.AddWithValue("$id", config.Id);

                    using var frameReader = frameCommand.ExecuteReader();
                    while (frameReader.Read())
                    {
                        config.SupportedFrames.Add(new SupportedFrame
                        {
                            Type = frameReader.GetString(0),
                            Amount = frameReader.GetString(1)
                        });
                    }

                    return config;
                }

                return null;
            });
        }

        public async Task<DateTime?> GetSavedTimestampAsync()
        {
            return await Task.Run<DateTime?>(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={_dbPath}");
                    connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT SavedAt FROM MachineConfig LIMIT 1";

                    using var reader = command.ExecuteReader();
                    if (reader.Read() && !reader.IsDBNull(0))
                    {
                        var savedAtString = reader.GetString(0);
                        if (!string.IsNullOrEmpty(savedAtString))
                        {
                            return DateTime.Parse(savedAtString);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DB] Error getting saved timestamp: {ex.Message}");
                }

                return (DateTime?)null;
            });
        }

        public async Task SaveFrameTemplatesAsync(List<FrameTemplate> frames)
        {
            await Task.Run(() =>
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();

                using var transaction = connection.BeginTransaction();

                try
                {
                    // Delete existing frames first
                    var deleteCommand = connection.CreateCommand();
                    deleteCommand.Transaction = transaction;
                    deleteCommand.CommandText = "DELETE FROM FrameTemplates";
                    int deletedRows = deleteCommand.ExecuteNonQuery();
                    System.Diagnostics.Debug.WriteLine($"[DB] Deleted {deletedRows} existing frame templates");

                    // Insert new frames with current timestamp using INSERT OR REPLACE
                    var savedAt = DateTime.UtcNow;
                    var insertCommand = connection.CreateCommand();
                    insertCommand.Transaction = transaction;
                    insertCommand.CommandText = @"
                        INSERT OR REPLACE INTO FrameTemplates 
                        (Id, MachineCode, Frame, Status, Image, SiteCode, CreatedAt, UpdatedAt, SavedAt)
                        VALUES 
                        ($id, $machineCode, $frame, $status, $image, $siteCode, $createdAt, $updatedAt, $savedAt)
                    ";

                    foreach (var frame in frames)
                    {
                        insertCommand.Parameters.Clear();
                        insertCommand.Parameters.AddWithValue("$id", frame.Id);
                        insertCommand.Parameters.AddWithValue("$machineCode", frame.MachineCode);
                        insertCommand.Parameters.AddWithValue("$frame", frame.Frame);
                        insertCommand.Parameters.AddWithValue("$status", frame.Status);
                        insertCommand.Parameters.AddWithValue("$image", frame.Image);
                        insertCommand.Parameters.AddWithValue("$siteCode", frame.SiteCode);
                        insertCommand.Parameters.AddWithValue("$createdAt", frame.CreatedAt.ToString("O"));
                        insertCommand.Parameters.AddWithValue("$updatedAt", frame.UpdatedAt.ToString("O"));
                        insertCommand.Parameters.AddWithValue("$savedAt", savedAt.ToString("O"));
                        insertCommand.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    System.Diagnostics.Debug.WriteLine($"[DB] {frames.Count} frame templates saved successfully");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    System.Diagnostics.Debug.WriteLine($"[DB] Error saving frame templates: {ex.Message}");
                    throw;
                }
            });
        }

        public async Task<List<FrameTemplate>> GetFrameTemplatesAsync()
        {
            return await Task.Run(() =>
            {
                var frames = new List<FrameTemplate>();

                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, MachineCode, Frame, Status, Image, SiteCode, CreatedAt, UpdatedAt
                    FROM FrameTemplates
                    WHERE Status = 'active'
                ";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    frames.Add(new FrameTemplate
                    {
                        Id = reader.GetString(0),
                        MachineCode = reader.GetString(1),
                        Frame = reader.GetString(2),
                        Status = reader.GetString(3),
                        Image = reader.GetString(4),
                        SiteCode = reader.GetString(5),
                        CreatedAt = DateTime.Parse(reader.GetString(6)),
                        UpdatedAt = DateTime.Parse(reader.GetString(7))
                    });
                }

                return frames;
            });
        }

        public async Task<DateTime?> GetFramesSavedTimestampAsync()
        {
            return await Task.Run<DateTime?>(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={_dbPath}");
                    connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT SavedAt FROM FrameTemplates LIMIT 1";

                    using var reader = command.ExecuteReader();
                    if (reader.Read() && !reader.IsDBNull(0))
                    {
                        var savedAtString = reader.GetString(0);
                        if (!string.IsNullOrEmpty(savedAtString))
                        {
                            return DateTime.Parse(savedAtString);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DB] Error getting frames saved timestamp: {ex.Message}");
                }

                return (DateTime?)null;
            });
        }

        public async Task SaveAppSettingAsync(string key, string value)
        {
            await Task.Run(() =>
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();

                // Create AppSettings table if it doesn't exist
                var createTableCommand = connection.CreateCommand();
                createTableCommand.CommandText = @"
                    CREATE TABLE IF NOT EXISTS AppSettings (
                        Key TEXT PRIMARY KEY,
                        Value TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL
                    )";
                createTableCommand.ExecuteNonQuery();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO AppSettings (Key, Value, UpdatedAt)
                    VALUES ($key, $value, $updatedAt)
                ";

                command.Parameters.AddWithValue("$key", key);
                command.Parameters.AddWithValue("$value", value);
                command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O"));

                command.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine($"[DB] App setting saved: {key} = {value}");
            });
        }

        public async Task<string?> GetAppSettingAsync(string key)
        {
            return await Task.Run(() =>
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();

                // Create AppSettings table if it doesn't exist
                var createTableCommand = connection.CreateCommand();
                createTableCommand.CommandText = @"
                    CREATE TABLE IF NOT EXISTS AppSettings (
                        Key TEXT PRIMARY KEY,
                        Value TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL
                    )";
                createTableCommand.ExecuteNonQuery();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT Value FROM AppSettings WHERE Key = $key";
                command.Parameters.AddWithValue("$key", key);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return reader.GetString(0);
                }

                return null;
            });
        }

        public async Task SaveOfflineFrameAsync(string frameId, string machineCode, string siteCode, string filePath, string? eventId, DateTime createdAt)
        {
            await Task.Run(() =>
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO OfflineFrames (frame_id, machine_code, site_code, filePath, event_id, createdAt)
                    VALUES ($frameId, $machineCode, $siteCode, $filePath, $eventId, $createdAt)
                ";

                command.Parameters.AddWithValue("$frameId", frameId);
                command.Parameters.AddWithValue("$machineCode", machineCode);
                command.Parameters.AddWithValue("$siteCode", siteCode);
                command.Parameters.AddWithValue("$filePath", filePath);
                command.Parameters.AddWithValue("$eventId", (object?)eventId ?? DBNull.Value);
                command.Parameters.AddWithValue("$createdAt", createdAt.ToString("O"));

                command.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine($"[DB] Offline frame saved: {frameId}, eventId: {eventId ?? "null"}");
            });
        }

        public async Task SaveTransactionDataAsync(string orderId, string machineCode, string siteCode, string frame, 
            double amount, DateTime createdAt, DateTime saleDate, string paymentMode, int totalCopies, 
            double totalAmount, string? eventId, string? onEvent)
        {
            await Task.Run(() =>
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO TransactionData 
                    (order_id, machine_code, site_code, frame, amount, created_at, sale_date, 
                     payment_mode, total_copies, total_amount, event_id, on_event)
                    VALUES 
                    ($orderId, $machineCode, $siteCode, $frame, $amount, $createdAt, $saleDate,
                     $paymentMode, $totalCopies, $totalAmount, $eventId, $onEvent)
                ";

                command.Parameters.AddWithValue("$orderId", orderId);
                command.Parameters.AddWithValue("$machineCode", machineCode);
                command.Parameters.AddWithValue("$siteCode", siteCode);
                command.Parameters.AddWithValue("$frame", frame);
                command.Parameters.AddWithValue("$amount", amount);
                command.Parameters.AddWithValue("$createdAt", createdAt.ToString("O"));
                command.Parameters.AddWithValue("$saleDate", saleDate.ToString("O"));
                command.Parameters.AddWithValue("$paymentMode", paymentMode);
                command.Parameters.AddWithValue("$totalCopies", totalCopies);
                command.Parameters.AddWithValue("$totalAmount", totalAmount);
                command.Parameters.AddWithValue("$eventId", eventId ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$onEvent", onEvent ?? (object)DBNull.Value);

                command.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine($"[DB] Transaction data saved: OrderId={orderId}, MachineCode='{machineCode}', SiteCode='{siteCode}', Frame='{frame}', Amount={amount}, TotalCopies={totalCopies}");
            });
        }

        public async Task<List<OfflineFrame>> GetAllOfflineFramesAsync()
        {
            return await Task.Run(() =>
            {
                var frames = new List<OfflineFrame>();

                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, frame_id, machine_code, site_code, filePath, event_id, createdAt
                    FROM OfflineFrames
                    ORDER BY createdAt ASC
                ";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    frames.Add(new OfflineFrame
                    {
                        Id = reader.GetInt32(0),
                        FrameId = reader.GetString(1),
                        MachineCode = reader.GetString(2),
                        SiteCode = reader.GetString(3),
                        FilePath = reader.GetString(4),
                        EventId = reader.IsDBNull(5) ? null : reader.GetString(5),
                        CreatedAt = DateTime.Parse(reader.GetString(6))
                    });
                }

                return frames;
            });
        }

        public async Task DeleteOfflineFrameAsync(string frameId)
        {
            await Task.Run(() =>
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM OfflineFrames WHERE frame_id = $frameId";
                command.Parameters.AddWithValue("$frameId", frameId);

                command.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine($"[DB] Offline frame deleted: {frameId}");
            });
        }

        public async Task<List<TransactionData>> GetAllTransactionDataAsync()
        {
            return await Task.Run(() =>
            {
                var transactions = new List<TransactionData>();

                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT order_id, machine_code, site_code, frame, amount, created_at, sale_date, 
                           payment_mode, total_copies, total_amount, event_id, on_event
                    FROM TransactionData
                    ORDER BY created_at ASC
                ";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var orderId = reader.GetString(0);
                    var machineCode = reader.GetString(1);
                    var siteCode = reader.GetString(2);
                    
                    System.Diagnostics.Debug.WriteLine($"[DB] Reading transaction: OrderId={orderId}, MachineCode='{machineCode}', SiteCode='{siteCode}'");
                    
                    transactions.Add(new TransactionData
                    {
                        OrderId = orderId,
                        MachineCode = machineCode,
                        SiteCode = siteCode,
                        Frame = reader.GetString(3),
                        Amount = reader.GetDouble(4),
                        CreatedAt = DateTime.Parse(reader.GetString(5)),
                        SaleDate = DateTime.Parse(reader.GetString(6)),
                        PaymentMode = reader.GetString(7),
                        TotalCopies = reader.GetInt32(8),
                        TotalAmount = reader.GetDouble(9),
                        EventId = reader.IsDBNull(10) ? null : reader.GetString(10),
                        OnEvent = reader.IsDBNull(11) ? null : reader.GetString(11)
                    });
                }

                System.Diagnostics.Debug.WriteLine($"[DB] Retrieved {transactions.Count} transaction(s) from database");
                return transactions;
            });
        }

        public async Task DeleteTransactionDataAsync(string orderId)
        {
            await Task.Run(() =>
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM TransactionData WHERE order_id = $orderId";
                command.Parameters.AddWithValue("$orderId", orderId);

                command.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine($"[DB] Transaction data deleted: {orderId}");
            });
        }
    }
}

