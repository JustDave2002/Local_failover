using Api.Pipeline.Middleware;
using Domain.Policies;
using Domain.Types;
using Infrastructure.Fence;
using Infrastructure.Heartbeat;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Messaging;
using Ports;

var builder = WebApplication.CreateBuilder(args);
Console.WriteLine("ENV= " + builder.Environment.EnvironmentName);

// Role bepalen uit environment
var env = builder.Environment.EnvironmentName?.ToLowerInvariant();
var role = env == "cloud" ? AppRole.Cloud : AppRole.Local;

// DI
builder.Services.AddSingleton<IAppRoleProvider>(_ => new AppRoleProvider(role));
builder.Services.AddSingleton<IDomainPolicy, DomainPolicy>();
builder.Services.AddSingleton<IFenceStateProvider, FenceStateProvider>();
builder.Services.AddControllers();
// Controllers ook beschikbaar maken voor DI (aangeroepen door CommandConsumer)
builder.Services.AddScoped<Api.Controllers.SalesOrdersController>();
builder.Services.AddScoped<Api.Controllers.StockMovementController>();

// Rabbit
Console.WriteLine("Rabbit host=" + builder.Configuration["Rabbit:HostName"]);
Console.WriteLine("Rabbit port=" + builder.Configuration["Rabbit:Port"]);
builder.Services.AddSingleton<IEventPublisher, RabbitEventPublisher>();
builder.Services.AddSingleton<RabbitConnection>();
builder.Services.AddSingleton<ICommandBus, RabbitCommandBus>();
if (role == AppRole.Cloud) {
    builder.Services.AddHostedService<CommandConsumer>();
}
if (role == AppRole.Local)  {
    builder.Services.AddHostedService<EventConsumer>();
    builder.Services.AddHostedService<OutboxPublisherHostedService>();
}

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.TimestampFormat = "[HH:mm:ss] ";
    o.SingleLine = true;
    o.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
});
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http", LogLevel.Warning);

// Swagger endpoint testing voor PoC
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS policy (dev only)
builder.Services.AddCors(o => o.AddPolicy("DevAll", p =>
{
    p.AllowAnyOrigin()      
     .AllowAnyHeader()
     .AllowAnyMethod();
}));

//Sqlite on VM, SqlServer LocalDB op windows
builder.Services.AddDbContext<ErpDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("Db");
    if (role == AppRole.Cloud) 
    {
        opt.UseSqlite(cs);
    }
    else 
    {
        opt.UseSqlServer(cs);
    }
});

// Heartbeat over RabbitMQ (geldig voor zowel Local als Cloud)
builder.Services.AddHostedService<Infrastructure.Heartbeat.HeartbeatSender>();
builder.Services.AddHostedService<Infrastructure.Heartbeat.HeartbeatReceiver>();

var app = builder.Build();

// Pipeline
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("DevAll");  
app.UseWriteGuard();      // blokkeer writes centraal o.b.v. policy
app.MapControllers();

await app.RunAsync();