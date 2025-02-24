# IoT Dashboard API

Bu proje, IoT cihazlarından gelen verileri gerçek zamanlı olarak görüntülemek için geliştirilmiş bir API'dir.

## 🚀 Özellikler

- Gerçek zamanlı veri akışı
- Apache Kafka entegrasyonu
- SignalR ile anlık veri iletimi
- RESTful API endpoints

## 🛠 Teknolojiler

- .NET 7.0
- Apache Kafka
- SignalR
- Entity Framework Core
- Swagger/OpenAPI

## 📋 Gereksinimler

- .NET 7.0 SDK
- Apache Kafka
- SQL Server (veya tercih ettiğiniz bir veritabanı)

## 🔧 Kurulum

1. Repoyu klonlayın:
```bash
git clone https://github.com/[kullanıcı-adınız]/IotDashboard-API.git
```

2. Proje dizinine gidin:
```bash
cd IotDashboard-API
```

3. Bağımlılıkları yükleyin:
```bash
dotnet restore
```

4. Uygulamayı çalıştırın:
```bash
dotnet run
```

## ⚙️ Yapılandırma

`appsettings.json` dosyasında aşağıdaki ayarları yapılandırın:

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Topic": "sensor-data",
    "GroupId": "dashboard-group"
  }
}
```

## 📝 API Endpoints

- `GET /api/sensor-data`: Tüm sensör verilerini getirir
- `POST /api/sensor-data`: Yeni sensör verisi ekler
- `GET /api/sensor-data/{id}`: Belirli bir sensör verisini getirir

## 🔌 WebSocket Bağlantısı

SignalR hub'ına bağlanmak için:
```
ws://[sunucu-adresi]/sensorHub
```

## 🤝 Katkıda Bulunma

1. Bu repoyu fork edin
2. Feature branch'i oluşturun (`git checkout -b feature/AmazingFeature`)
3. Değişikliklerinizi commit edin (`git commit -m 'Add some AmazingFeature'`)
4. Branch'inizi push edin (`git push origin feature/AmazingFeature`)
5. Pull Request oluşturun

## 📄 Lisans

Bu proje MIT lisansı altında lisanslanmıştır. Detaylar için [LICENSE](LICENSE) dosyasına bakın. 