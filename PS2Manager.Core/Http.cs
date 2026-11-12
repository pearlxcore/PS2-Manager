using System.Net.Http.Headers;

namespace PS2Manager.Core;

/// <summary>One shared HttpClient for the whole app.</summary>
public static class Http
{
    public static HttpClient Client { get; } = Create();

    private static HttpClient Create()
    {
        var c = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 8,
        })
        {
            Timeout = TimeSpan.FromMinutes(3),
        };
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PS2Manager", "2.0"));
        return c;
    }
}
