using System;
using System.Threading;
using System.Threading.Tasks;
using Domain.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ports;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Infrastructure.Messaging;

namespace Infrastructure.Heartbeat;

public sealed class HeartbeatReceiver : BackgroundService
{
    private readonly RabbitConnection _conn;
    private readonly ILogger<HeartbeatReceiver> _log;
    private readonly IConfiguration _cfg;
    private readonly IAppRoleProvider _roleProvider;
    private readonly IFenceStateProvider _fence;

    private IModel? _ch;
    private string _exchange = "hb";
    private string _queueName = "";

    private DateTime _lastBeatUtc = DateTime.MinValue;
    private int _graceMs;

    public HeartbeatReceiver(
        RabbitConnection conn,
        ILogger<HeartbeatReceiver> log,
        IConfiguration cfg,
        IAppRoleProvider roleProvider,
        IFenceStateProvider fence)
    {
        _conn = conn;
        _log = log;
        _cfg = cfg;
        _roleProvider = roleProvider;
        _fence = fence;
    }

    protected override Task ExecuteAsync(CancellationToken ct)
    {
        _ch = _conn.CreateChannel();
        _ch.ExchangeDeclare(_exchange, ExchangeType.Topic, durable: false, autoDelete: false);

        var tenantId = _cfg["Tenant:Id"] ?? "T1";
        var role     = _roleProvider.Role;

        // Local luistert naar cloud-heartbeats
        // Cloud luistert naar local-heartbeats
        var otherRoleKey = role == AppRole.Local ? "cloud" : "local";

        _queueName = $"q.hb.{otherRoleKey}.{tenantId}";
        var rk = $"hb.{otherRoleKey}.{tenantId}";

        _ch.QueueDeclare(queue: _queueName, durable: false, exclusive: false, autoDelete: false);
        _ch.QueueBind(_queueName, _exchange, rk);

        var consumer = new AsyncEventingBasicConsumer(_ch);
        consumer.Received += OnReceivedAsync;
        _ch.BasicConsume(queue: _queueName, autoAck: true, consumer: consumer);

        _graceMs = int.TryParse(_cfg["Heartbeat:GraceMs"], out var g) ? g : 7000;

        _log.LogInformation("[HB-RECV] Listening on {queue} (rk={rk}, grace={grace}ms)", _queueName, rk, _graceMs);

        _ = MonitorLoopAsync(tenantId, ct);
        return Task.CompletedTask;
    }

    private Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        _lastBeatUtc = DateTime.UtcNow;

        if (_fence.GetFenceMode("T1") != FenceMode.Online)
        {
            _fence.SetFenceMode(FenceMode.Online);
        }

        // _log.LogDebug("[HB-RECV] beat from other side at {ts}", _lastBeatUtc);
        return Task.CompletedTask;
    }

    private async Task MonitorLoopAsync(string tenantId, CancellationToken ct)
    {
        var checkMs = Math.Max(_graceMs / 3, 500);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(checkMs, ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            var last = _lastBeatUtc;
            if (last == DateTime.MinValue) continue;

            var delta = DateTime.UtcNow - last;
            if (delta > TimeSpan.FromMilliseconds(_graceMs))
            {
                if (_fence.GetFenceMode(tenantId) != FenceMode.Fenced)
                {
                    _fence.SetFenceMode(FenceMode.Fenced);
                }
            }
        }
    }

    public override void Dispose()
    {
        try { _ch?.Dispose(); } catch { }
        base.Dispose();
    }
}
