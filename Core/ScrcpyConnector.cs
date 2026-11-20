using System.Net;
using System.Net.Sockets;

namespace PhoneMirror.Core;

/// <summary>Connexions ouvertes avec le serveur : vidéo, et son et contrôle s'ils sont activés.</summary>
public sealed record ScrcpyConnection(ScrcpyVideoStream Video, ScrcpyAudioStream? Audio, ScrcpyControlChannel? Control)
    : IDisposable
{
    public void Dispose()
    {
        Control?.Dispose();
        Audio?.Dispose();
        Video.Dispose();
    }
}

/// <summary>
/// Ouvre les connexions dans l'ordre attendu par le serveur scrcpy (DesktopConnection.java) :
/// vidéo d'abord (le serveur y envoie un octet factice), puis son, puis contrôle ; le nom de
/// l'appareil et le codec n'arrivent sur la connexion vidéo qu'une fois toutes acceptées.
/// </summary>
public static class ScrcpyConnector
{
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(2);

    public static async Task<ScrcpyConnection> ConnectAsync(
        int port, bool withAudio, bool withControl, Action<string> log, Func<bool> isServerRunning,
        TimeSpan timeout, CancellationToken ct)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        TcpClient video = await ConnectVideoAsync(port, isServerRunning, deadline, ct).ConfigureAwait(false);
        TcpClient? audio = null;
        TcpClient? control = null;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(Remaining(deadline));

            if (withAudio) audio = await ConnectNextAsync(port, timeoutCts.Token).ConfigureAwait(false);
            if (withControl) control = await ConnectNextAsync(port, timeoutCts.Token).ConfigureAwait(false);

            ScrcpyVideoStream stream = await ScrcpyVideoStream.HandshakeAsync(video, timeoutCts.Token).ConfigureAwait(false);
            return new ScrcpyConnection(
                stream,
                audio == null ? null : new ScrcpyAudioStream(audio),
                control == null ? null : new ScrcpyControlChannel(control, log));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            DisposeAll(video, audio, control);
            throw new TimeoutException("Le téléphone n'a pas terminé l'ouverture de la connexion à temps.");
        }
        catch
        {
            DisposeAll(video, audio, control);
            throw;
        }
    }

    /// <summary>Connexion suivante (son ou contrôle) : le serveur l'accepte dès la précédente établie.</summary>
    private static async Task<TcpClient> ConnectNextAsync(int port, CancellationToken ct)
    {
        TcpClient client = CreateClient();
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, port, ct).ConfigureAwait(false);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static void DisposeAll(params TcpClient?[] clients)
    {
        foreach (TcpClient? client in clients) client?.Dispose();
    }

    /// <summary>
    /// Se connecte en réessayant jusqu'à ce que le serveur écoute. Avec un tunnel adb « forward »,
    /// la connexion TCP réussit même si le serveur n'est pas encore prêt : seul l'octet factice
    /// envoyé par le serveur prouve qu'il est bien là.
    /// </summary>
    private static async Task<TcpClient> ConnectVideoAsync(
        int port, Func<bool> isServerRunning, DateTime deadline, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (!isServerRunning())
                throw new IOException("Le serveur s'est arrêté avant d'accepter la connexion.");

            TcpClient client = CreateClient();
            try
            {
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                attemptCts.CancelAfter(AttemptTimeout);

                await client.ConnectAsync(IPAddress.Loopback, port, attemptCts.Token).ConfigureAwait(false);
                var dummy = new byte[1];
                if (await client.GetStream().ReadAsync(dummy, attemptCts.Token).ConfigureAwait(false) == 1)
                    return client;
            }
            catch (Exception ex) when (ex is SocketException or IOException
                                       || (ex is OperationCanceledException && !ct.IsCancellationRequested))
            {
                // Serveur pas encore prêt : on réessaie.
            }
            catch
            {
                client.Dispose();
                throw;
            }

            client.Dispose();
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("Le téléphone n'a pas démarré la vidéo à temps.");

            await Task.Delay(100, ct).ConfigureAwait(false);
        }
    }

    private static TcpClient CreateClient() => new() { NoDelay = true, ReceiveBufferSize = 1024 * 1024 };

    private static TimeSpan Remaining(DateTime deadline)
    {
        TimeSpan remaining = deadline - DateTime.UtcNow;
        return remaining > TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1);
    }
}
