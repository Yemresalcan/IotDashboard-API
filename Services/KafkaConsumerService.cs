using Confluent.Kafka;
using IoTDashboard.API.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace IoTDashboard.API.Services;

public class KafkaConsumerService : BackgroundService
{
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, (double min, double max, double avg, int count)> _sensorStats;
    private readonly double _anomalyThreshold = 2.0; // standart sapmanın kaç katı anomali sayılacak
    private readonly Queue<SensorDataModel> _messageQueue = new Queue<SensorDataModel>();
    private readonly object _queueLock = new object();
    private readonly int _batchSize = 5;
    private readonly Timer _processTimer;

    public KafkaConsumerService(
        IHubContext<DashboardHub> hubContext,
        IConfiguration configuration)
    {
        _hubContext = hubContext;
        _configuration = configuration;
        _sensorStats = new Dictionary<string, (double min, double max, double avg, int count)>();
        _processTimer = new Timer(ProcessQueuedMessages, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
    }

    private class SensorDataModel
    {
        public string machine_id { get; set; } = string.Empty;
        public string timestamp { get; set; } = string.Empty;
        public Dictionary<string, SensorValue> sensors { get; set; } = new();
    }

    private class SensorValue
    {
        public double value { get; set; }
        public string unit { get; set; } = string.Empty;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = _configuration["Kafka:BootstrapServers"];
        var groupId = _configuration["Kafka:GroupId"];
        var topic = _configuration["Kafka:Topic"];

        if (string.IsNullOrEmpty(bootstrapServers) || string.IsNullOrEmpty(groupId) || string.IsNullOrEmpty(topic))
        {
            Console.WriteLine("❌ Kafka yapılandırması eksik!");
            Console.WriteLine($"BootstrapServers: {bootstrapServers}");
            Console.WriteLine($"GroupId: {groupId}");
            Console.WriteLine($"Topic: {topic}");
            return;
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = true,
            MaxPollIntervalMs = 300000,
            SessionTimeoutMs = 45000,
            HeartbeatIntervalMs = 10000,
            EnableAutoOffsetStore = false,
            MaxPartitionFetchBytes = 1 * 1024 * 1024,
            FetchWaitMaxMs = 500,
            SocketTimeoutMs = 60000,
            SocketKeepaliveEnable = true,
            EnablePartitionEof = true,
            AllowAutoCreateTopics = true
        };

        try
        {
            // Kafka topic'in var olup olmadığını kontrol et
            var adminConfig = new AdminClientConfig { BootstrapServers = config.BootstrapServers };
            using var adminClient = new AdminClientBuilder(adminConfig).Build();
            
            try
            {
                var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
                var topicExists = metadata.Topics.Any(t => t.Topic == _configuration["Kafka:Topic"]);
                
                if (!topicExists)
                {
                    Console.WriteLine($"⚠️ Topic bulunamadı: {_configuration["Kafka:Topic"]}");
                    Console.WriteLine("🔄 Topic otomatik oluşturulacak...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Kafka metadata kontrolü başarısız: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Kafka admin client hatası: {ex.Message}");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var consumer = new ConsumerBuilder<Ignore, string>(config)
                    .SetErrorHandler((_, e) => Console.WriteLine($"⚠️ Kafka hatası: {e.Reason}"))
                    .SetStatisticsHandler((_, json) => Console.WriteLine($"📊 Kafka istatistikleri: {json}"))
                    .SetPartitionsAssignedHandler((c, partitions) =>
                    {
                        Console.WriteLine($"✅ Partisyonlar atandı: {string.Join(", ", partitions.Select(p => p.Partition.Value))}");
                    })
                    .SetPartitionsRevokedHandler((c, partitions) =>
                    {
                        Console.WriteLine($"⚠️ Partisyonlar geri alındı: {string.Join(", ", partitions.Select(p => p.Partition.Value))}");
                    })
                    .Build();

                try
                {
                    consumer.Subscribe(_configuration["Kafka:Topic"]);
                    Console.WriteLine($"✅ Topic'e abone olundu: {_configuration["Kafka:Topic"]}");

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(500));
                            if (consumeResult != null)
                            {
                                await ProcessMessage(consumeResult.Message.Value);
                                try
                                {
                                    consumer.StoreOffset(consumeResult);
                                    consumer.Commit(consumeResult);
                                }
                                catch (KafkaException ke)
                                {
                                    Console.WriteLine($"⚠️ Offset kaydetme hatası: {ke.Message}");
                                }
                            }
                        }
                        catch (ConsumeException ex)
                        {
                            Console.WriteLine($"❌ Veri okuma hatası: {ex.Error.Reason}");
                            if (ex.Error.IsFatal)
                            {
                                throw;
                            }
                            await Task.Delay(1000, stoppingToken);
                        }
                    }
                }
                finally
                {
                    try
                    {
                        consumer.Close();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Consumer kapatma hatası: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("⚠️ İşlem iptal edildi");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Beklenmeyen hata: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async void ProcessQueuedMessages(object? state)
    {
        List<SensorDataModel> batch = new List<SensorDataModel>();
        
        lock (_queueLock)
        {
            while (_messageQueue.Count > 0 && batch.Count < _batchSize)
            {
                batch.Add(_messageQueue.Dequeue());
            }
        }

        if (batch.Count > 0)
        {
            try
            {
                foreach (var data in batch)
                {
                    var formattedData = new
                    {
                        machineId = data.machine_id,
                        timestamp = data.timestamp,
                        sensors = data.sensors
                    };

                    await _hubContext.Clients.All.SendAsync("ReceiveSensorData", formattedData);

                    foreach (var sensor in data.sensors)
                    {
                        var value = sensor.Value.value;
                        UpdateSensorStats(sensor.Key, value);
                        if (IsAnomaly(sensor.Key, value))
                        {
                            await SendAnomalyAlert(sensor.Key, value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Toplu işleme hatası: {ex.Message}");
            }
        }
    }

    private async Task ProcessMessage(string message)
    {
        try
        {
            var data = JsonSerializer.Deserialize<SensorDataModel>(message);
            if (data == null)
            {
                Console.WriteLine("❌ Mesaj deserialize edilemedi!");
                return;
            }

            lock (_queueLock)
            {
                _messageQueue.Enqueue(data);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Mesaj işleme hatası: {ex.Message}");
        }
    }

    private void UpdateSensorStats(string sensorId, double value)
    {
        if (!_sensorStats.ContainsKey(sensorId))
        {
            _sensorStats[sensorId] = (value, value, value, 1);
            return;
        }

        var stats = _sensorStats[sensorId];
        var newCount = stats.count + 1;
        var newAvg = ((stats.avg * stats.count) + value) / newCount;
        var newMin = Math.Min(stats.min, value);
        var newMax = Math.Max(stats.max, value);

        _sensorStats[sensorId] = (newMin, newMax, newAvg, newCount);
    }

    private bool IsAnomaly(string sensorId, double value)
    {
        if (!_sensorStats.ContainsKey(sensorId))
            return false;

        var stats = _sensorStats[sensorId];
        var range = stats.max - stats.min;
        var threshold = range * _anomalyThreshold;
        
        return Math.Abs(value - stats.avg) > threshold;
    }

    private async Task SendAnomalyAlert(string sensorId, double value)
    {
        var alert = new
        {
            sensorId = sensorId,
            value = value,
            timestamp = DateTime.Now.ToString("O"),
            message = $"Anormal değer tespit edildi: {sensorId} = {value}"
        };

        await _hubContext.Clients.All.SendAsync("ReceiveAnomalyAlert", alert);
    }

    private string GetSensorUnit(string sensorId)
    {
        return sensorId.ToLower() switch
        {
            var s when s.Contains("sicaklik") => "°C",
            var s when s.Contains("basinc") => "bar",
            var s when s.Contains("nem") => "%",
            var s when s.Contains("hiz") => "RPM",
            _ => "birim"
        };
    }
}