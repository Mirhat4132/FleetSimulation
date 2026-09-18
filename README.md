# 🚀 Araç Takip Sistemi Telemetri Simülatörü

Bu proje, yüksek hacimli IoT (Nesnelerin İnterneti) cihazlarından gelen araç telemetri verilerini asenkron olarak üreten ve bu verileri yüksek erişilebilirlikli veritabanı kümelerine (MongoDB Replica Set & PostgreSQL) aktaran, .NET 8 tabanlı bir performans simülatörüdür.

## 🏗️ Mimari ve Öne Çıkan Özellikler

* **Asenkron ThreadPool Mimarisi:** Binlerce cihazı (ör: 5000 araç) simüle ederken ağır işletim sistemi thread'leri yerine .NET ThreadPool (`Task.Run` & `ConcurrentQueue`) kullanılarak CPU ve RAM tüketimi optimize edilmiştir.
* **Producer-Consumer Deseni:** Cihazlar veriyi üretip Thread-Safe bir kuyruğa bırakırken, arka plandaki bağımsız işçiler (consumers) bu verileri toplu (batch) olarak veritabanına yazar.
* **High Availability (Yüksek Erişilebilirlik):** MongoDB üzerinde 3 düğümlü (1 Primary, 2 Secondary) Replica Set mimarisi Docker üzerinden yapılandırılmıştır.
* **Polly Entegrasyonu:** Veritabanına toplu yazma sırasında oluşabilecek anlık ağ kopmalarına karşı `WaitAndRetryAsync` politikaları ile veri kaybı önlenmiştir.
* **Otomatik Excel Raporlama (ClosedXML):** Belirlenen aralıklarla sistem gecikmeleri (p50, p90, p99), veritabanı küme durumu ve araç telemetrileri profesyonel bir Excel (.xlsx) raporuna dökülür.

## 🛠️ Kullanılan Teknolojiler
* **Dil & Framework:** C#, .NET 8.0 Console App
* **Veritabanları:** MongoDB, PostgreSQL
* **Konteynerleştirme:** Docker & Docker Compose
* **Kütüphaneler:** MongoDB.Driver, Npgsql, ClosedXML, Polly

## ⚙️ Kurulum ve Çalıştırma

### 1. Ön Koşullar
Bilgisayarınızda **Docker Desktop** ve **.NET 8 SDK** kurulu olmalıdır.

### 2. Veritabanlarını Ayağa Kaldırma
Proje dizininde ( `docker-compose.yml` dosyasının bulunduğu yerde) terminali açın ve veritabanı kümelerini başlatın:

```bash
docker-compose up -d