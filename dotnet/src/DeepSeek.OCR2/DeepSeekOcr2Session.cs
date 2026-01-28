using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DeepSeek.OCR2;

public sealed class DeepSeekOcr2Session : IAsyncDisposable, IDisposable
{
    private bool _disposed;

    private DeepSeekOcr2Session(DeepSeekOcr2LocalServer server, HttpClient httpClient, DeepSeekOcr2Client client)
    {
        Server = server ?? throw new ArgumentNullException(nameof(server));
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public DeepSeekOcr2LocalServer Server { get; }
    public HttpClient HttpClient { get; }
    public DeepSeekOcr2Client Client { get; }

    public static async Task<DeepSeekOcr2Session> CreateAsync(
        DeepSeekOcr2LocalServerOptions? serverOptions = null,
        CancellationToken cancellationToken = default)
    {
        var server = await DeepSeekOcr2LocalServer.StartAsync(serverOptions, cancellationToken).ConfigureAwait(false);
        var http = new HttpClient { BaseAddress = server.BaseUri };
        var client = new DeepSeekOcr2Client(http);
        return new DeepSeekOcr2Session(server, http, client);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            HttpClient.Dispose();
        }
        finally
        {
            Server.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            HttpClient.Dispose();
        }
        finally
        {
            await Server.DisposeAsync().ConfigureAwait(false);
        }
    }
}

