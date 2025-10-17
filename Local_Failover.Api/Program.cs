using Api.Pipeline.Middleware;
using Domain.Policies;
using Domain.Types;
using Infrastructure.Fence;
using Infrastructure.Heartbeat;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Ports;

var builder = WebApplication.CreateBuilder(args);

// Role bepalen uit environment
var env = builder.Environment.EnvironmentName?.ToLowerInvariant();
var role = env == "cloud" ? AppRole.Cloud : AppRole.Local;

// DI
builder.Services.AddSingleton<IAppRoleProvider>(_ => new AppRoleProvider(role));
builder.Services.AddSingleton<IDomainPolicy, DomainPolicy>();
builder.Services.AddSingleton<IFenceStateProvider, FenceStateProvider>();
builder.Services.AddControllers();
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

builder.Services.AddDbContext<ErpDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("Db");
    opt.UseSqlServer(cs);
});

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IHeartbeatProbe, HttpHeartbeatProbe>();
builder.Services.AddHostedService<HeartbeatHostedService>();

var app = builder.Build();

// Pipeline
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("DevAll");  
app.UseWriteGuard();      // blokkeer writes centraal o.b.v. policy
app.MapControllers();

app.Run();
