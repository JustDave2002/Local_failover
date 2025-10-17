using Ports;
using System.Net.Http;

namespace Infrastructure.Heartbeat;
public sealed class HttpHeartbeatProbe : IHeartbeatProbe
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    public HttpHeartbeatProbe(IHttpClientFactory http, IConfiguration cfg)
    { _http = http; _cfg = cfg; }

    public async Task<bool> CheckAsync(CancellationToken ct)
    {
      var baseUrl = _cfg["Heartbeat:RemoteUrl"]; // e.g. http://localhost:5000
      if (string.IsNullOrWhiteSpace(baseUrl)) return false;
      try {
        var r = await _http.CreateClient().GetAsync($"{baseUrl}/status", ct);
        return r.IsSuccessStatusCode;
      } catch { return false; }
    }
}
