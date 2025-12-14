using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Domain.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ports;
using RabbitMQ.Client;
using Infrastructure.Messaging;

namespace Infrastructure.Heartbeat;

public sealed class HeartbeatSender : BackgroundService
{
    private readonly RabbitConnection _conn;
    private readonly ILogger<HeartbeatSender> _log;
    private readonly IConfiguration _cfg;
    private readonly IAppRoleProvider _roleProvider;

    private IModel? _ch;
    private string _exchange = "hb";
    private string _routingKey = "";
    private int _intervalMs;

    public HeartbeatSender(
        RabbitConnection conn,
        ILogger<HeartbeatSender> log,
        IConfiguration cfg,
        IAppRoleProvider roleProvider)
    {
        _conn = conn;
        _log = log;
        _cfg = cfg;
        _roleProvider = roleProvider;
    }

    protected override Task ExecuteAsync(CancellationToken ct)
    {
        _ch = _conn.CreateChannel();
        _ch.ExchangeDeclare(_exchange, ExchangeType.Topic, durable: false, autoDelete: false);

        var tenantId = _cfg["Tenant:Id"] ?? "T1";
        var role     = _roleProvider.Role;

        // Local stuurt: hb.local.T1
        // Cloud stuurt: hb.cloud.T1
        var roleKey = role == AppRole.Local ? "local" : "cloud";
        _routingKey = $"hb.{roleKey}.{tenantId}";

        _intervalMs = int.TryParse(_cfg["Heartbeat:IntervalMs"], out var i) ? i : 3000;

        _ = LoopAsync(ct);
        return Task.CompletedTask;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        var body = Encoding.UTF8.GetBytes("{}");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                _ch!.BasicPublish(
                    exchange: _exchange,
                    routingKey: _routingKey,
                    basicProperties: null,
                    body: body);

                // _log.LogDebug("[HB-SEND] {rk}", _routingKey);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[HB-SEND] failed");
            }

            try
            {
                await Task.Delay(_intervalMs, ct);
            }
            catch (TaskCanceledException)
            {
                // shutdown
            }
        }
    }

    public override void Dispose()
    {
        try { _ch?.Dispose(); } catch { }
        base.Dispose();
    }
}
