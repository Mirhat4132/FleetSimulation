using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Simulator
{
    public class MongoTelemetryRepository : ITelemetryRepository
    {

        public async Task BulkUpsertTelemetryAsync(List<DeviceTelemetry> batch)     
{
    if (batch == null || batch.Count == 0) return;

    var writes = new List<WriteModel<DeviceTelemetry>>();

    foreach (var telemetry in batch)
    {
        var filter = Builders<DeviceTelemetry>.Filter.Eq(x => x.DeviceId, telemetry.DeviceId);
        var upsert = new ReplaceOneModel<DeviceTelemetry>(filter, telemetry) { IsUpsert = true };
        writes.Add(upsert);
    }

    // IsOrdered = false: Bir hata çıksa bile diğerlerini durdurmaz, çok daha hızlı yazar
    await _collection.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false });
}

        private readonly MongoClient _client;
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<DeviceTelemetry> _collection;
        private readonly string _collectionName;

        public MongoTelemetryRepository(string connectionString, string dbName = "FleetDb", string collectionName = "Telemetries", int maxPoolSize = 500)
        {
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.MaxConnectionPoolSize = maxPoolSize;
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
            settings.WaitQueueTimeout = TimeSpan.FromSeconds(10);

            _client = new MongoClient(settings);
            _database = _client.GetDatabase(dbName);
            _collectionName = collectionName;
            _collection = _database.GetCollection<DeviceTelemetry>(collectionName);
        }

        public async Task InitializeAsync()
        {
            try
            {
                var indexKeys = Builders<DeviceTelemetry>.IndexKeys.Ascending(x => x.DeviceId);
                var indexOptions = new CreateIndexOptions { Unique = true, Name = "ux_device_id" };
                await _collection.Indexes.CreateOneAsync(new CreateIndexModel<DeviceTelemetry>(indexKeys, indexOptions));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BİLGİ] MongoDB indeks kontrolü: {ex.Message}");
            }
        }

        

        public async Task<List<DeviceTelemetry>> GetTelemetriesAsync(int startId, int endId)
        {
            var filter = Builders<DeviceTelemetry>.Filter.Gte(x => x.DeviceId, startId) &
                         Builders<DeviceTelemetry>.Filter.Lte(x => x.DeviceId, endId);

            return await _collection.Find(filter)
                .SortBy(x => x.DeviceId)
                .ToListAsync();
        }

        public async Task ResetDatabaseAsync()
        {
            await _database.DropCollectionAsync(_collectionName);
        }

        // --- CANLI REPLICA SET LİDER VE GECİKME TAKİBİ ---
        public async Task PrintReplicaSetLagAsync()
        {
            try
            {
                var adminDb = _client.GetDatabase("admin");
                var command = new BsonDocument("replSetGetStatus", 1);
                var status = await adminDb.RunCommandAsync<BsonDocument>(command);

                var members = status["members"].AsBsonArray;

                // O an kimin lider (PRIMARY) olduğunu dinamik olarak bul
                var primary = members
                    .Select(m => m.AsBsonDocument)
                    .FirstOrDefault(m => m.GetValue("stateStr", "").AsString == "PRIMARY");

                if (primary == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n[REPLICA SET] Aktif bir PRIMARY düğüm bulunamadı! (Lider seçimi sürüyor veya küme kilitli)\n");
                    Console.ResetColor();
                    return;
                }

                string primaryName = primary.GetValue("name", "Bilinmiyor").AsString;
                DateTime primaryOptime = primary.GetValue("optimeDate", DateTime.UtcNow).ToUniversalTime();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n┌─── [REPLICA LAG & DÜĞÜM DURUMU] ──────────────────────────┐");
                Console.WriteLine($"│ GÜNCEL LİDER (PRIMARY): {primaryName,-33} │");
                Console.WriteLine($"├───────────────────────────────────────────────────────────┤");

                foreach (var memberValue in members)
                {
                    var m = memberValue.AsBsonDocument;
                    string name = m.GetValue("name", "").AsString;
                    string stateStr = m.GetValue("stateStr", "").AsString;
                    double health = m.GetValue("health", 0).ToDouble();

                    // Liderin kendisini gecikme listesinde gösterme
                    if (stateStr == "PRIMARY") continue;

                    // Düğüm kapatılmışsa veya ulaşılamıyorsa
                    if (health == 0 || stateStr.Contains("not reachable"))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"│ {name,-18} : [ÖLÜ / ERİŞİLEMEZ]                      │");
                        Console.ForegroundColor = ConsoleColor.Cyan;
                    }
                    // Canlı ikincil düğüm ise milisaniyelik farkı hesapla
                    else if (stateStr == "SECONDARY")
                    {
                        DateTime secOptime = m.GetValue("optimeDate", primaryOptime).ToUniversalTime();
                        double lagMs = Math.Max(0, (primaryOptime - secOptime).TotalMilliseconds);
                        Console.WriteLine($"│ {name,-18} : Gecikme: {lagMs,5:F0} ms (SECONDARY)           │");
                    }
                    else
                    {
                        Console.WriteLine($"│ {name,-18} : Durum: {stateStr,-15}          │");
                    }
                }

                Console.WriteLine($"└───────────────────────────────────────────────────────────┘\n");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"[UYARI] Replica Set durumu okunamadı: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}