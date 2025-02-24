using IoTDashboard.API.Hubs;
using IoTDashboard.API.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Serilog yapılandırması
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/iot-dashboard-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Yapılandırma servisini ekle
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Temel servisler
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

// API Controller'ları ekle
builder.Services.AddControllers();

// Swagger/OpenAPI yapılandırması
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "IoT Dashboard API",
        Version = "v1",
        Description = "IoT cihazlarından gelen verileri gerçek zamanlı görüntülemek için API",
        Contact = new OpenApiContact
        {
            Name = "IoT Dashboard Team",
            Email = "contact@iotdashboard.com"
        }
    });
});

// Health Check yapılandırması
builder.Services.AddHealthChecks()
    .AddKafka(builder.Configuration["Kafka:BootstrapServers"])
    .AddSignalR();

// SignalR yapılandırması
builder.Services.AddSignalR(options => {
    options.EnableDetailedErrors = true;
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 102400; // 100 KB
    options.KeepAliveInterval = TimeSpan.FromSeconds(10);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// CORS politikası
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.WithOrigins(allowedOrigins ?? Array.Empty<string>())
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials()
               .WithExposedHeaders("Content-Disposition");
    });
});

// Test veri üretici servisi
//builder.Services.AddHostedService<TestDataGenerator>();

// Kafka consumer servisi
builder.Services.AddHostedService<KafkaConsumerService>();

var app = builder.Build();

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "IoT Dashboard API V1");
        c.RoutePrefix = "api-docs";
    });
}

// Middleware pipeline
app.UseRouting();
app.UseCors();

// WebSocket desteği
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30),
    ReceiveBufferSize = 4 * 1024 // 4 KB buffer
};
app.UseWebSockets(webSocketOptions);

// Health check endpoint'leri
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

// API Controller'ları ve Hub
app.MapControllers();
app.MapHub<DashboardHub>("/hub");

// Hoşgeldin sayfası
app.MapGet("/", () => Results.Redirect("/api-docs"));

Log.Information("=================================================");
Log.Information("🚀 API Başlatıldı");
Log.Information("📡 API URL: http://localhost:5005");
Log.Information("📚 Docs URL: http://localhost:5005/api-docs");
Log.Information("📡 Hub URL: http://localhost:5005/hub");
Log.Information("=================================================");

// Kestrel ayarları
app.Urls.Add("http://localhost:5005");
await app.RunAsync();