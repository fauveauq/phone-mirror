using System.Net;
using System.Net.Sockets;

namespace PhoneMirror.Tests;

/// <summary>Deux extrémités TCP reliées en local : « Client » (côté PC) et « Server » (côté téléphone simulé).</summary>
internal sealed class LoopbackPair : IDisposable
{
    public TcpClient Client { get; }
    public TcpClient Server { get; }
    public NetworkStream ServerStream => Server.GetStream();

    private LoopbackPair(TcpClient client, TcpClient server)
    {
        Client = client;
        Server = server;
    }

    public static async Task<LoopbackPair> CreateAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var client = new TcpClient { NoDelay = true };
            Task<TcpClient> accept = listener.AcceptTcpClientAsync();
            await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            return new LoopbackPair(client, await accept);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>Lit exactement <paramref name="count"/> octets côté serveur, avec un délai maximal.</summary>
    public async Task<byte[]> ReadFromServerAsync(int count)
    {
        var buffer = new byte[count];
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await ServerStream.ReadExactlyAsync(buffer, cts.Token);
        return buffer;
    }

    public void Dispose()
    {
        Client.Dispose();
        Server.Dispose();
    }
}
