using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ClosedXML.Excel;
using Polly;
using Polly.Retry;

namespace Simulator
{
    class Program
    {
        private static long _totalRequests = 0;
        private static long _successfulRequests = 0;
        private static long _failedRequests = 0;
        private static readonly ConcurrentBag<double> _latencyRecordsMs = new ConcurrentBag<double>();

        //5000 thread'in ürettiği verileri toplayacağımız thread-safe ortak kuyruk
        private static readonly ConcurrentQueue<DeviceTelemetry> _telemetryQueue = new ConcurrentQueue<DeviceTelemetry>();

        // Retry politikası (Toplu yazma sırasında geçici ağ kopmaları için)
        private static readonly AsyncRetryPolicy _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(150 * attempt),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    if (retryCount >= 2)
                    {
                        Console.WriteLine($"[DARBOĞAZ] Toplu yazma bağlantısı zorlanıyor ({retryCount}/3)...");
                    }
                });

        static async Task Main(string[] args)
        {
            Console.WriteLine("==========================================================");
            Console.WriteLine("ARAÇ TAKİP SİSTEMİ TELEMETRİ SİMÜLATÖRÜ");
            Console.WriteLine("==========================================================\n");

            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            string provider = config["DatabaseSettings:Provider"] ?? "MongoDB";
            int defaultStartId = int.Parse(config["SimulationSettings:DefaultStartId"] ?? "1000");
            int defaultEndId = int.Parse(config["SimulationSettings:DefaultEndId"] ?? "3499");
            int intervalSeconds = int.Parse(config["SimulationSettings:UpdateIntervalSeconds"] ?? "5");

            Console.WriteLine($"[AKTİF VERİTABANI SAĞLAYICISI]: {provider.ToUpper()}");

            // --- REPOSITORY FACTORY ---
            ITelemetryRepository repository;
            if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                string pgConn = config["DatabaseSettings:PostgreSQL:ConnectionString"]
                    ?? "Host=localhost;Port=5432;Database=FleetDb;Username=postgres;Password=secret;Maximum Pool Size=500;";
                repository = new PostgresTelemetryRepository(pgConn);
            }
            else
            {
                string mongoConn = config["DatabaseSettings:Mongo:ConnectionString"]
                    ?? "mongodb://mongo1:27017,mongo2:27018,mongo3:27019/?replicaSet=rs0&serverSelectionTimeoutMS=5000";
                string dbName = config["DatabaseSettings:Mongo:DatabaseName"] ?? "FleetDb";
                string colName = config["DatabaseSettings:Mongo:CollectionName"] ?? "Telemetries";
                int maxPool = int.Parse(config["DatabaseSettings:Mongo:MaxPoolSize"] ?? "500");

                repository = new MongoTelemetryRepository(mongoConn, dbName, colName, maxPool);
            }

            Console.Write($"Başlangıç Cihaz ID [Varsayılan: {defaultStartId}]: ");
            string inputStart = Console.ReadLine()?.Trim() ?? string.Empty;
            int startId = int.TryParse(inputStart, out int pStart) && pStart > 0 ? pStart : defaultStartId;

            Console.Write($"Bitiş Cihaz ID [Varsayılan: {defaultEndId}]: ");
            string inputEnd = Console.ReadLine()?.Trim() ?? string.Empty;
            int endId = int.TryParse(inputEnd, out int pEnd) && pEnd >= startId ? pEnd : defaultEndId;

            int totalDeviceCount = (endId - startId) + 1;

            Console.Write("\nBaşlamadan Önce Veritabanını Sıfırlamak İster Misiniz? ( 1 = Evet || 2 = Hayır ) : ");
            string resetChoice = Console.ReadLine()?.Trim() ?? "2";

            string excelFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Benchmark_Filo_Raporu.xlsx");

            if (resetChoice == "1")
            {
                try
                {
                    Console.WriteLine("\n[İŞLEM] Veriler temizleniyor...");
                    await repository.ResetDatabaseAsync();
                    if (File.Exists(excelFilePath)) File.Delete(excelFilePath);
                    Console.WriteLine("[BİLGİ] Sıfırlama tamamlandı.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UYARI] Sıfırlama atlandı: {ex.Message}");
                }
            }

            // Tablo veya indeks kontrolü
            Console.WriteLine("[BAĞLANTI] Veritabanı şeması ve indeksler hazırlanıyor...");
            await repository.InitializeAsync();

            var allDeviceIds = Enumerable.Range(startId, totalDeviceCount).ToList();

            // [AYNEN KORUNDU]: 5000 cihaz için işletim sisteminden fiziksel thread tahsisi
            Console.WriteLine($"\n[BAŞLATILIYOR] İşletim sisteminden {totalDeviceCount} adet FİZİKSEL THREAD tahsis ediliyor...");

            var threads = new List<Thread>(totalDeviceCount);
            try
            {
                for (int i = 0; i < allDeviceIds.Count; i++)
                {
                    int deviceId = allDeviceIds[i];

                    var t = new Thread(() => DeviceWorkerLoop(deviceId, intervalSeconds), 256 * 1024)
                    {
                        IsBackground = true,
                        Name = $"DevThread_{deviceId}"
                    };

                    t.Start();
                    threads.Add(t);

                    if ((i + 1) % 1000 == 0)
                    {
                        Console.WriteLine($"-> {i + 1} adet OS Thread oluşturuldu...");
                    }
                }

                Console.WriteLine($"\n[BAŞARILI] Tam {threads.Count} adet fiziksel OS Thread ayağa kalktı!\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[HATA] Thread tahsisi limitlere takıldı: {ex.Message}");
                Console.ResetColor();
                return;
            }

           for (int i = 0; i < 3; i++)
            {
                _ = Task.Run(async () => await StartBulkWriterAsync(repository));
            }
            // MongoDB Replica Set gecikme takipçisi
            if (repository is MongoTelemetryRepository mongoRepo)
            {
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        await Task.Delay(10000);
                        await mongoRepo.PrintReplicaSetLagAsync();
                    }
                });
            }

            //Excel raporunu ana döngüden ayırdık, her 15 SANİYEDE BİR arka planda çalışacak
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(15000); // 15 saniye bekleme
                    await ExportBenchmarkAndTelemetryToExcelAsync(repository, startId, endId, excelFilePath);
                }
            });

            int round = 1;
            while (true)
            {
                await Task.Delay(intervalSeconds * 1000);

                long currentTotal = Interlocked.Read(ref _totalRequests);
                long currentSuccess = Interlocked.Read(ref _successfulRequests);
                long currentFailed = Interlocked.Read(ref _failedRequests);

                var snapshot = _latencyRecordsMs.ToArray();
                var recentSlice = snapshot.TakeLast(Math.Min(totalDeviceCount, snapshot.Length)).ToList();
                double avgLatency = recentSlice.Count > 0 ? recentSlice.Average() : 0;
                double minLatency = recentSlice.Count > 0 ? recentSlice.Min() : 0;
                double maxLatency = recentSlice.Count > 0 ? recentSlice.Max() : 0;

                Console.WriteLine($"[METRİK #{round}] Sağlayıcı: {provider} | Kuyrukta Bekleyen: {_telemetryQueue.Count} | İstek: {currentTotal} | Başarılı: {currentSuccess} | Hatalı: {currentFailed} | Ort: {avgLatency:F2} ms | Min: {minLatency:F2} ms | Max: {maxLatency:F2} ms");

                round++;
            }
        }

        // [DEĞİŞTİ]: Fiziksel thread'ler veritabanına gitmez, sadece veriyi üretip kuyruğa atar
        private static void DeviceWorkerLoop(int deviceId, int intervalSeconds)
        {
            var random = new Random(deviceId);

            // Başlangıç yığılmasını engelleyen hafif faz kaydırma (jitter)
            Thread.Sleep(random.Next(0, intervalSeconds * 1000));

            while (true)
            {
                var telemetry = GenerateRandomData(deviceId, random);

                // Veritabanı çağrısı kalktı; doğrudan ConcurrentQueue'ya fırlatıyoruz (milisaniyenin altında sürer)
                _telemetryQueue.Enqueue(telemetry);

                Thread.Sleep(intervalSeconds * 1000);
            }
        }

        //Kuyruktaki verileri 500-1000'erli paketler halinde alıp veritabanına BulkWrite yapar
        private static async Task StartBulkWriterAsync(ITelemetryRepository repository)
        {
            while (true)
            {
                var batch = new List<DeviceTelemetry>();

                // Kuyrukta birikenlerden en fazla 500 tanesini tek bir pakete topla
                while (batch.Count < 2000 && _telemetryQueue.TryDequeue(out var item))
                {
                    batch.Add(item);
                }

                if (batch.Count > 0)
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        await _retryPolicy.ExecuteAsync(async () =>
                        {
                            await repository.BulkUpsertTelemetryAsync(batch);
                        });

                        sw.Stop();

                        // Paket başına düşen ortalama gecikmeyi metrik havuzuna ekliyoruz
                        double latencyPerRecord = sw.Elapsed.TotalMilliseconds / batch.Count;
                        for (int i = 0; i < batch.Count; i++)
                        {
                            _latencyRecordsMs.Add(latencyPerRecord);
                        }

                        Interlocked.Add(ref _successfulRequests, batch.Count);
                    }
                    catch
                    {
                        sw.Stop();
                        Interlocked.Add(ref _failedRequests, batch.Count);
                    }
                    finally
                    {
                        Interlocked.Add(ref _totalRequests, batch.Count);
                    }
                }
                else
                {
                    // Kuyruk henüz boşsa CPU'yu gereksiz tüketmemek için ufak bir bekleme
                    await Task.Delay(20);
                }
            }
        }

        public static async Task ExportBenchmarkAndTelemetryToExcelAsync(ITelemetryRepository repository, int startId, int endId, string filePath)
        {
            try
            {
                var snapshot = _latencyRecordsMs.ToArray();
                if (snapshot.Length == 0) return;

                Array.Sort(snapshot);

                double minMs = snapshot[0];
                double maxMs = snapshot[^1];
                double avgMs = snapshot.Average();
                double p50Ms = GetPercentile(snapshot, 0.50);
                double p90Ms = GetPercentile(snapshot, 0.90);
                double p95Ms = GetPercentile(snapshot, 0.95);
                double p99Ms = GetPercentile(snapshot, 0.99);

                var devices = await repository.GetTelemetriesAsync(startId, endId);

                using (var workbook = new XLWorkbook())
                {
                    var benchSheet = workbook.Worksheets.Add("Benchmark Analizi");
                    benchSheet.Cell(1, 1).Value = "Performans Metriği";
                    benchSheet.Cell(1, 2).Value = "Milisaniye (ms)";
                    benchSheet.Cell(1, 3).Value = "Mikrosaniye (us)";

                    var headerRange = benchSheet.Range("A1:C1");
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.Navy;
                    headerRange.Style.Font.FontColor = XLColor.White;

                    int r = 2;
                    AddBenchmarkRow(benchSheet, r++, "Ortalama Yanıt Süresi (Average)", avgMs);
                    AddBenchmarkRow(benchSheet, r++, "En Hızlı Yanıt (Min)", minMs);
                    AddBenchmarkRow(benchSheet, r++, "En Yavaş Yanıt (Max)", maxMs);
                    AddBenchmarkRow(benchSheet, r++, "Medyan Süre (p50)", p50Ms);
                    AddBenchmarkRow(benchSheet, r++, "90. Yüzdelik Dilim (p90)", p90Ms);
                    AddBenchmarkRow(benchSheet, r++, "95. Yüzdelik Dilim (p95)", p95Ms);
                    AddBenchmarkRow(benchSheet, r++, "99. Yüzdelik Dilim (p99)", p99Ms);

                    benchSheet.Cell(r + 1, 1).Value = "Test Edilen ID Aralığı";
                    benchSheet.Cell(r + 1, 2).Value = $"{startId} - {endId} ({endId - startId + 1} Cihaz)";
                    benchSheet.Cell(r + 2, 1).Value = "Toplam Başarılı Yazma";
                    benchSheet.Cell(r + 2, 2).Value = Interlocked.Read(ref _successfulRequests);
                    benchSheet.Cell(r + 3, 1).Value = "Toplam Hatalı İstek";
                    benchSheet.Cell(r + 3, 2).Value = Interlocked.Read(ref _failedRequests);
                    benchSheet.Columns().AdjustToContents();

                    var telemetrySheet = workbook.Worksheets.Add("Filo Telemetrisi");
                    string[] headers = new string[]
                    {
                        "Cihaz ID", "IMEI", "Durum", "Firmware",
                        "Enlem (Lat)", "Boylam (Lng)", "Rakım (m)", "Hız (km/h)", "Yön (°)", "GPS Doğruluk", "Uydu Sayısı",
                        "IP Adresi", "Sinyal Gücü (dBm)", "Bağlantı Türü", "Operatör",
                        "Motor Devri (RPM)", "Motor Sıcaklığı (°C)", "Benzin Seviyesi (%)", "Kilometre", 
                        "Akü Voltajı (V)", "Motor Çalışıyor mu?", "Lastik Basıncı (Bar)",
                        "Kabin Sıcaklığı (°C)", "Kapı Açık mı?", "Bagaj Açık mı?", "Emniyet Kemeri",
                        "Sert Fren", "Sert Hızlanma", "Kayıt Tarihi", "Son Güncelleme"
                    };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = telemetrySheet.Cell(1, i + 1);
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.AirForceBlue;
                        cell.Style.Font.FontColor = XLColor.White;
                    }

                    int row = 2;
                    foreach (var dev in devices)
                    {
                        telemetrySheet.Cell(row, 1).Value = dev.DeviceId;
                        telemetrySheet.Cell(row, 2).Value = dev.Network.ImeiNumber;
                        telemetrySheet.Cell(row, 3).Value = dev.DeviceStatus;
                        telemetrySheet.Cell(row, 4).Value = dev.FirmwareVersion;
                        telemetrySheet.Cell(row, 5).Value = dev.Gps.Latitude;
                        telemetrySheet.Cell(row, 6).Value = dev.Gps.Longitude;
                        telemetrySheet.Cell(row, 7).Value = dev.Gps.Altitude;
                        telemetrySheet.Cell(row, 8).Value = dev.Gps.SpeedKmh;
                        telemetrySheet.Cell(row, 9).Value = dev.Gps.HeadingDegrees;
                        telemetrySheet.Cell(row, 10).Value = dev.Gps.GpsAccuracy;
                        telemetrySheet.Cell(row, 11).Value = dev.Gps.SatelliteCount;
                        telemetrySheet.Cell(row, 12).Value = dev.Network.IpAddress;
                        telemetrySheet.Cell(row, 13).Value = dev.Network.SignalStrengthDbm;
                        telemetrySheet.Cell(row, 14).Value = dev.Network.ConnectionType;
                        telemetrySheet.Cell(row, 15).Value = dev.Network.NetworkProvider;
                        telemetrySheet.Cell(row, 16).Value = dev.Engine.EngineRpm;
                        telemetrySheet.Cell(row, 17).Value = dev.Engine.EngineTemperatureC;
                        telemetrySheet.Cell(row, 18).Value = dev.Engine.FuelLevelPercent;
                        telemetrySheet.Cell(row, 19).Value = dev.Engine.OdometerKm;
                        telemetrySheet.Cell(row, 20).Value = dev.Engine.BatteryVoltage;
                        telemetrySheet.Cell(row, 21).Value = dev.State.IsEngineRunning ? "Evet" : "Hayır";
                        telemetrySheet.Cell(row, 22).Value = dev.Engine.TirePressureBar;
                        telemetrySheet.Cell(row, 23).Value = dev.State.CabinTemperatureC;
                        telemetrySheet.Cell(row, 24).Value = dev.State.IsDoorOpen ? "Açık" : "Kapalı";
                        telemetrySheet.Cell(row, 25).Value = dev.State.IsTrunkOpen ? "Açık" : "Kapalı";
                        telemetrySheet.Cell(row, 26).Value = dev.State.IsSeatbeltFastened ? "Takılı" : "Takılı Değil";
                        telemetrySheet.Cell(row, 27).Value = dev.State.HarshBrakingEvent ? "VAR" : "Yok";
                        telemetrySheet.Cell(row, 28).Value = dev.State.HarshAccelerationEvent ? "VAR" : "Yok";
                        telemetrySheet.Cell(row, 29).Value = GetTurkeyTime(dev.CreatedAt).ToString("dd.MM.yyyy HH:mm:ss");
                        telemetrySheet.Cell(row, 30).Value = GetTurkeyTime(dev.UpdatedAt).ToString("dd.MM.yyyy HH:mm:ss");
                        row++;
                    }

                    telemetrySheet.Columns().AdjustToContents();
                    workbook.SaveAs(filePath);
                    Console.WriteLine($"\n[RAPOR] Excel güncellendi: {Path.GetFileName(filePath)} ({devices.Count} kayıt)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[UYARI] Excel raporu oluşturulamadı: {ex.Message}");
            }
        }

        private static void AddBenchmarkRow(IXLWorksheet sheet, int row, string metricName, double msValue)
        {
            sheet.Cell(row, 1).Value = metricName;
            sheet.Cell(row, 2).Value = Math.Round(msValue, 4);
            sheet.Cell(row, 3).Value = Math.Round(msValue * 1000.0, 2);
        }

        private static double GetPercentile(double[] sortedData, double percentile)
        {
            int idx = (int)Math.Ceiling(percentile * sortedData.Length) - 1;
            return sortedData[Math.Clamp(idx, 0, sortedData.Length - 1)];
        }

        static DeviceTelemetry GenerateRandomData(int deviceId, Random rnd)
        {
            var now = DateTime.UtcNow;
            return new DeviceTelemetry
            {
                DeviceId = deviceId,
                DeviceStatus = "Active",
                FirmwareVersion = "v2.1.4",
                Network = new NetworkInfo
                {
                    ImeiNumber = $"3598710{rnd.Next(1000000, 9999999)}",
                    IpAddress = $"10.0.{rnd.Next(1, 255)}.{rnd.Next(1, 255)}",
                    SignalStrengthDbm = rnd.Next(-100, -50),
                    ConnectionType = "4G",
                    NetworkProvider = "GlobalNet"
                },
                Gps = new GpsInfo
                {
                    Latitude = 41.0 + (rnd.NextDouble() * 0.1),
                    Longitude = 28.9 + (rnd.NextDouble() * 0.1),
                    Altitude = rnd.Next(10, 500),
                    SpeedKmh = rnd.Next(0, 120),
                    HeadingDegrees = rnd.Next(0, 360),
                    GpsAccuracy = Math.Round(rnd.NextDouble() * 5, 2),
                    SatelliteCount = rnd.Next(4, 12)
                },
                Engine = new EngineInfo
                {
                    EngineRpm = rnd.Next(800, 4500),
                    EngineTemperatureC = rnd.Next(70, 110),
                    FuelLevelPercent = rnd.Next(10, 100),
                    OdometerKm = rnd.Next(10000, 150000),
                    BatteryVoltage = Math.Round(12.0 + (rnd.NextDouble() * 2.5), 1),
                    TirePressureBar = Math.Round(2.0 + rnd.NextDouble(), 2)
                },
                State = new VehicleState
                {
                    IsEngineRunning = true,
                    CabinTemperatureC = rnd.Next(18, 28),
                    IsDoorOpen = false,
                    IsTrunkOpen = false,
                    IsSeatbeltFastened = true,
                    HarshBrakingEvent = rnd.Next(0, 100) > 95,
                    HarshAccelerationEvent = rnd.Next(0, 100) > 95
                },
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        private static DateTime GetTurkeyTime(DateTime utcDate)
        {
            var utc = DateTime.SpecifyKind(utcDate, DateTimeKind.Utc);
            try
            {
                var tzId = OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul";
                var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                return TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            }
            catch
            {
                return utc.AddHours(3);
            }
        }
    }
}