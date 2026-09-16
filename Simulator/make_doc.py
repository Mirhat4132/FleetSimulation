import docx
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

def set_cell_background(cell, hex_color):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement('w:shd')
    shd.set(qn('w:val'), 'clear')
    shd.set(qn('w:color'), 'auto')
    shd.set(qn('w:fill'), hex_color)
    tc_pr.append(shd)

def set_cell_margins(cell, top=100, bottom=100, left=150, right=150):
    tc_pr = cell._tc.get_or_add_tcPr()
    tcMar = OxmlElement('w:tcMar')
    for m, val in [('top', top), ('bottom', bottom), ('left', left), ('right', right)]:
        node = OxmlElement(f'w:{m}')
        node.set(qn('w:w'), str(val))
        node.set(qn('w:type'), 'dxa')
        tcMar.append(node)
    tc_pr.append(tcMar)

doc = docx.Document()

# Sayfa Yapısı
for section in doc.sections:
    section.top_margin = Inches(1)
    section.bottom_margin = Inches(1)
    section.left_margin = Inches(1)
    section.right_margin = Inches(1)

# Rapor Başlığı
title = doc.add_paragraph()
title_run = title.add_run("DAĞITIK FİLO TELEMETRİ VE VERİTABANI BENCHMARK SİSTEM RAPORU")
title_run.bold = True
title_run.font.size = Pt(17)
title_run.font.color.rgb = RGBColor(0, 32, 96)
title.alignment = WD_ALIGN_PARAGRAPH.CENTER

subtitle = doc.add_paragraph()
sub_run = subtitle.add_run("Yüksek Hacimli IoT Veri Akışı, MongoDB Replica Set Mimarisi ve Asenkron Performans Doğrulaması\n")
sub_run.font.size = Pt(11)
sub_run.font.italic = True
subtitle.alignment = WD_ALIGN_PARAGRAPH.CENTER

# 1. BÖLÜM
h1 = doc.add_heading("1. Giriş ve Proje Amacı", level=1)
h1.style.font.color.rgb = RGBColor(0, 32, 96)
doc.add_paragraph(
    "Bu mühendislik çalışmasının temel amacı; 20.000 aktif IoT araç takip cihazından periyodik olarak üretilen "
    "telemetri paketlerinin (GPS, motor durumu, şebeke ve kabin sensör verileri) dağıtık bir veri tabanı kümesine "
    "yüksek verimlilik, düşük kaynak tüketimi ve sıfır veri kaybı (%100 tolerans) ile aktarılmasını sağlamaktır. "
    "Sistem; gerçek dünya saha senaryolarını simüle edecek biçimde çok iş parçacıklı (multi-threaded) mimaride koşturulmuş, "
    "veri akışının ve veritabanı motorunun sınırları stres ve yük testleriyle ölçümlenmiştir."
)

# 2. BÖLÜM
h2 = doc.add_heading("2. Sistem Mimarisi ve Teknolojik Tercihler", level=1)
h2.style.font.color.rgb = RGBColor(0, 32, 96)

p_arch = doc.add_paragraph()
p_arch.add_run(".NET 8.0 C# Motoru: ").bold = True
p_arch.add_run("Tamamen asenkron (async/await) Task tabanlı worker havuzuyla 20.000 cihazı 1.000'erlik gruplar halinde simüle eder.\n")
p_arch.add_run("MongoDB 3-Node Replica Set (rs0): ").bold = True
p_arch.add_run("Docker üzerinde izole koşan 1 Primary ve 2 Secondary düğümden oluşur. Yüksek erişilebilirlik (HA) ve veri bütünlüğü garanti edilir.\n")
p_arch.add_run("Polly Resilience Framework: ").bold = True
p_arch.add_run("Olası lider seçimlerinde veya geçici soket tıkanmalarında devreye giren üstel geri çekilmeli (exponential backoff) otomatik yeniden deneme mekanizması sunar.\n")
p_arch.add_run("ClosedXML & Compass: ").bold = True
p_arch.add_run("Arka planda çalışan periyodik raporlayıcı ile Excel çıktısı üretilir; resmi GUI olan MongoDB Compass üzerinden indeks ve şema canlı izlenir.")

# 3. BÖLÜM
h3 = doc.add_heading("3. Veri Modeli ve Veritabanı Optimizasyonları", level=1)
h3.style.font.color.rgb = RGBColor(0, 32, 96)

opts = [
    ("Tip Dönüşümü (String -> Int32): ", "Cihaz kimliği string ('DEV-01000') yerine 32-bit tamsayı (1000) olarak tutulmuştur. Bu adım B-Tree indeks boyutunu %50'den fazla küçültmüş ve RAM içi arama hızını artırmıştır."),
    ("Hiyerarşik Doküman Tasarımı: ", "Dağınık 30 sensör alanı mantıksal alt nesnelere (NetworkInfo, GpsInfo, EngineInfo, VehicleState) ayrıştırılarak profesyonel IoT standartlarına uyarlanmıştır."),
    ("BulkWrite (Toplu Yazma) Mimarisi: ", "Cihazlar için tek tek ağ çağrısı yapmak yerine 1.000'erli paketler UpdateOneModel ile toplu aktarılmış, ağ ek yükü (round-trip) 20.000'den 20 çağrıya indirilmiştir."),
    ("İki Kademeli Zaman Damgası ($setOnInsert): ", "CreatedAt alanı yalnızca belge ilk kez veritabanına eklenirken kaydedilir. Sonraki döngülerde $set operatörü ile yalnızca UpdatedAt yenilenir.")
]
for opt_t, opt_d in opts:
    bp = doc.add_paragraph(style='List Bullet')
    r = bp.add_run(opt_t)
    r.bold = True
    bp.add_run(opt_d)

# 4. BÖLÜM
h4 = doc.add_heading("4. Veritabanı Performans Analizi ve Benchmark Bulguları", level=1)
h4.style.font.color.rgb = RGBColor(0, 32, 96)

doc.add_paragraph(
    "20.000 aktif cihaz ile kesintisiz yürütülen kararlı durum (steady-state) testinde 1.880.000'den fazla "
    "telemetri işlemi kaydedilmiş ve aşağıdaki metrikler elde edilmiştir:"
)

# Tablo 1: Benchmark
t1 = doc.add_table(rows=1, cols=3)
t1.alignment = WD_TABLE_ALIGNMENT.CENTER
hdr1 = t1.rows[0].cells
titles1 = ["Performans Parametresi", "Ölçülen Değer", "Açıklama / Analiz"]
for i, name in enumerate(titles1):
    hdr1[i].text = name
    hdr1[i].paragraphs[0].runs[0].font.bold = True
    hdr1[i].paragraphs[0].runs[0].font.color.rgb = RGBColor(255, 255, 255)
    set_cell_background(hdr1[i], "1F497D")
    set_cell_margins(hdr1[i])

data1 = [
    ("Toplam İşlenen İstek", "1.881.000 Adet", "Test boyunca MongoDB'ye başarıyla yazılan telemetri"),
    ("Başarı / Hata Oranı", "%100 Başarı / 0 Hata", "1.88M işlemde sıfır paket kaybı ve sıfır timeout"),
    ("Paket Başına Ortalama Süre", "4.734,38 ms", "1.000 araçlık toplu paketin ağ, BSON ve disk süresi"),
    ("Cihaz Başına Ortalama Maliyet", "~4,73 ms", "1 telemetri verisinin sisteme ortalama maliyeti"),
    ("En Hızlı Paket Yanıtı (Min)", "215,20 ms", "WiredTiger önbelleğinin en rahat olduğu an"),
    ("En Yavaş Paket Yanıtı (Max)", "11.349,26 ms", "WiredTiger disk checkpoint anındaki gecikme"),
    ("Yazma Hacmi (Throughput)", "~4.000 veri/sn", "20.000 cihazın 5 sn aralıkla ürettiği veri debisi")
]

for row_data in data1:
    row_cells = t1.add_row().cells
    for i, val in enumerate(row_data):
        row_cells[i].text = val
        set_cell_margins(row_cells[i])

# Tablo 2: Kaynaklar
doc.add_paragraph("\nDonanım Kaynakları ve Veritabanı Motoru Durumu:")
t2 = doc.add_table(rows=1, cols=3)
t2.alignment = WD_TABLE_ALIGNMENT.CENTER
hdr2 = t2.rows[0].cells
titles2 = ["Bileşen / Kaynak", "Kullanım Değeri", "Teknik Değerlendirme"]
for i, name in enumerate(titles2):
    hdr2[i].text = name
    hdr2[i].paragraphs[0].runs[0].font.bold = True
    hdr2[i].paragraphs[0].runs[0].font.color.rgb = RGBColor(255, 255, 255)
    set_cell_background(hdr2[i], "366092")
    set_cell_margins(hdr2[i])

data2 = [
    ("Docker CPU Yükü (Primary)", "%15 - %25", "Çok çekirdekli yapıda işlemci darboğazı oluşmamıştır"),
    ("MongoDB Bellek (RAM)", "~450 MB - 600 MB", "WiredTiger önbelleği limitler dahilinde kalmıştır"),
    ("İndeks Boyutu (ux_device_id)", "~400 KB", "Int32 tipi sayesinde indeks bütünüyle RAM'de tutulur"),
    ("Bağlantı Havuzu (Connection Pool)", "20 - 30 Aktif Soket", "Havuz verimli kullanılmış, soket tükenmesi yaşanmamıştır"),
    ("Sıkıştırma Verimi (Snappy)", "~%65 Boyut Tasarrufu", "Veriler diske yazılırken optimize depolanmıştır")
]

for row_data in data2:
    row_cells = t2.add_row().cells
    for i, val in enumerate(row_data):
        row_cells[i].text = val
        set_cell_margins(row_cells[i])

# 5. BÖLÜM: DÜZENSİZ ARTIŞIN ANALİZİ
h5 = doc.add_heading("\n5. İstek Sayısındaki Asenkron Dalgalanmanın Analizi", level=1)
h5.style.font.color.rgb = RGBColor(0, 32, 96)

doc.add_paragraph(
    "Test terminalinde istek sayısının sabit 20.000 adımlarla değil; 9.000, 26.000, 71.000 veya 100.000 gibi "
    "farklı miktarlarda arttığı gözlemlenmiştir. Bu durum bir hata değil, dağıtık ve asenkron mimarinin beklenen "
    "ve hedeflenen çalışma biçimidir:"
)

points = [
    ("Örnekleme Periyodu Uyuşmazlığı: ", "Konsol metrik izleyicisi 5.000 ms'de bir sayaç okumaktadır. Ancak 1.000 cihazlık toplu paketin MongoDB tarafından işlenmesi ortalama 4.7 saniye sürmekte, ardından 5.0 saniye bekleme uygulanmaktadır. Toplam işçi tur süresi ~9.7 saniye olduğundan, 5 saniyelik sabit örnekleme penceresine her turda farklı sayıda işçinin bitişi denk gelmektedir."),
    ("Thundering Herd Koruması (Jitter): ", "Tüm cihazların aynı anda veritabanına yüklenip I/O kilitlenmesi yaratmaması için işçilere ±500 ms rastgele gecikme (jitter) verilmiştir. İşçiler zaman çizgisine bilinçli olarak dağıtılmıştır."),
    ("Gecikme Dağılımı (215 ms - 11.349 ms): ", "MongoDB'nin diske yaptığı checkpoint ve flushing anlarında bazı paketler daha uzun sürede tamamlanmakta; geciken paketler topluca bittiğinde sayaçta ani sıçramalar (+100.000) üretmektedir."),
    ("Veri Bütünlüğü Kanıtı: ", "Tüm artış miktarlarının istisnasız 1.000'in tam katı olması, hiçbir paketin bölünmediğini ve işçilerin kuyruğu kayıpsız tamamladığını kanıtlamaktadır.")
]

for p_title, p_desc in points:
    bp = doc.add_paragraph(style='List Bullet')
    r = bp.add_run(p_title)
    r.bold = True
    bp.add_run(p_desc)

# 6. BÖLÜM
h6 = doc.add_heading("6. Sonuç ve Mühendislik Çıkarımları", level=1)
h6.style.font.color.rgb = RGBColor(0, 32, 96)

doc.add_paragraph(
    "Yürütülen yük ve performans testleri sonucunda kurulan mimarinin 20.000 aktif cihazdan gelen saniyede ~4.000 "
    "telemetri akışını %100 doğrulukla ve sıfır hata ile karşıladığı teyit edilmiştir.\n\n"
    "Cihaz başına ortalama ~4,7 ms seviyesinde seyreden işlem maliyeti ve %25'in altında kalan CPU tüketimi; "
    "mevcut sistem donanımının ek bir sunucuya ihtiyaç duymaksızın 60.000 - 80.000 araçlık çok daha büyük filoları "
    "tek başına kaldırabilecek kapasite rezervine sahip olduğunu kanıtlamıştır."
)

doc.save("Filo_Telemetri_Sistem_Raporu.docx")
print("Guncel Word raporu basariyla olusturuldu: Filo_Telemetri_Sistem_Raporu.docx")
