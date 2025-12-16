using Api.Pipeline.Middleware;
using Domain.Policies;
using Domain.Types;
using Infrastructure.Fence;
using Infrastructure.Heartbeat;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Messaging;
using Ports;
using Application.Sync;
using Infrastructure.Messaging.Handlers;



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

builder.Services.AddScoped<ICommandDispatcher, CommandDispatcher>();

builder.Services.AddScoped<ISyncGateway, SyncGateway>();

builder.Services.AddScoped<Ports.ISyncApplyHandler, Infrastructure.Messaging.Handlers.SalesOrderPostHandler>();
builder.Services.AddScoped<Ports.ISyncApplyHandler, Infrastructure.Messaging.Handlers.StockMovementPostHandler>();

builder.Services.AddScoped<IOutboxWriter, DbOutboxWriter>();
builder.Services.AddHostedService<CommandConsumer>();
builder.Services.AddHostedService<OutboxPublisherHostedService>();


// Rabbit
Console.WriteLine("Rabbit host=" + builder.Configuration["Rabbit:HostName"]);
Console.WriteLine("Rabbit port=" + builder.Configuration["Rabbit:Port"]);
builder.Services.AddSingleton<RabbitConnection>();
builder.Services.AddSingleton<ICommandBus, RabbitCommandBus>();
if (role == AppRole.Cloud) {
    
}
if (role == AppRole.Local)  {
    // builder.Services.AddHostedService<EventConsumer>();
    
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
        // opt.UseSqlite(cs);
        opt.UseSqlServer(cs);

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