namespace PhoneMirror.Core;

/// <summary>
/// Une session de recopie : serveur sur le téléphone, connexion, puis décodage
/// sur un thread dédié qui publie les images dans <see cref="Frames"/>.
/// </summary>
public sealed class MirrorSession(MirrorOptions options) : IAsyncDisposable
{
    private ScrcpyServer? _server;
    private ScrcpyVideoStream? _stream;
    private ScrcpyAudioStream? _audioStream;
    private AudioPipeline? _audio;
    private Thread? _videoThread;
    private long _decodedFrames;
    private long _decodeErrors;
    private volatile bool _stopping;
    private int _disposed;

    public FrameExchange Frames { get; } = new();
    public string DeviceName { get; private set; } = "";

    /// <summary>Canal de contrôle (souris, clavier, presse-papiers) ; null si désactivé ou pas encore connecté.</summary>
    public ScrcpyControlChannel? Control { get; private set; }
    /// <summary>Écran virtuel du mode « une seule application » ; null sinon ou pas encore créé.</summary>
    public int? VirtualDisplayId => _server?.VirtualDisplayId;

    /// <summary>Nom affiché de l'application lancée seule, s'il est connu.</summary>
    public string? AppLabel => _server?.StartedAppLabel;

    public long DecodedFrames => Interlocked.Read(ref _decodedFrames);
    public long DecodeErrors => Interlocked.Read(ref _decodeErrors);

    public event Action<string>? Log;

    /// <summary>Déclenché depuis le thread vidéo quand la session s'arrête d'elle-même (câble débranché, etc.).</summary>
    public event Action<Exception>? Faulted;

    public async Task StartAsync(CancellationToken ct)
    {
        FfmpegLoader.EnsureLoaded(options.FfmpegDirectory);
        WriteLog($"FFmpeg {FfmpegLoader.Version}");

        var server = new ScrcpyServer(options, WriteLog);
        _server = server;
        await server.StartAsync(ct).ConfigureAwait(false);
        await AbortIfStoppedAsync().ConfigureAwait(false);

        try
        {
            ScrcpyConnection connection = await ScrcpyConnector.ConnectAsync(
                options.LocalPort, options.EnableAudio, options.EnableControl, WriteLog, () => !server.HasExited,
                TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
            _stream = connection.Video;
            _audioStream = connection.Audio;
            Control = connection.Control;
        }
        catch (Exception ex) when (server.HasExited && ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "Le serveur s'est arrêté sur le téléphone :\n" + server.RecentOutput, ex);
        }

        await AbortIfStoppedAsync().ConfigureAwait(false);
        DeviceName = _stream.DeviceName;
        WriteLog($"Connecté à {DeviceName}");

        _videoThread = new Thread(VideoLoop)
        {
            IsBackground = true,
            Name = "PhoneMirror video",
            Priority = ThreadPriority.AboveNormal,
        };
        _videoThread.Start();

        if (_audioStream != null) _audio = new AudioPipeline(_audioStream, WriteLog);
    }

    private void VideoLoop()
    {
        Exception? failure = null;
        try
        {
            using var decoder = new H264Decoder();
            ScrcpyVideoStream stream = _stream!;
            byte[] pendingConfig = [];

            while (!_stopping)
            {
                StreamPacket packet = stream.ReadPacket();
                switch (packet.Kind)
                {
                    case PacketKind.Session:
                        WriteLog($"Session vidéo : {packet.Width}×{packet.Height}");
                        break;

                    case PacketKind.Config:
                        // SPS/PPS : transmis au décodeur avec l'image suivante, comme le fait scrcpy.
                        pendingConfig = stream.GetPayload(packet).ToArray();
                        break;

                    case PacketKind.Frame:
                        int frames = decoder.Decode(pendingConfig, stream.GetPayload(packet), Frames);
                        pendingConfig = [];
                        Interlocked.Add(ref _decodedFrames, frames);
                        Interlocked.Exchange(ref _decodeErrors, decoder.ErrorCount);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            if (!_stopping) failure = ex;
        }

        if (failure != null)
        {
            WriteLog("Arrêt de la vidéo : " + failure);
            Exception reported = failure is IOException
                ? new IOException("La connexion avec le téléphone a été interrompue.", failure)
                : failure;
            Faulted?.Invoke(reported);
        }
    }

    /// <summary>
    /// Si la session a été fermée pendant le démarrage, libère ce qui a été créé
    /// entre-temps (sinon le serveur resterait lancé sur le téléphone).
    /// </summary>
    private async Task AbortIfStoppedAsync()
    {
        if (!_stopping) return;
        Control?.Dispose();
        _audioStream?.Dispose();
        _stream?.Dispose();
        if (_server != null) await _server.DisposeAsync().ConfigureAwait(false);
        throw new OperationCanceledException("Session fermée pendant le démarrage.");
    }

    private void WriteLog(string message) => Log?.Invoke(message);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _stopping = true;
        Control?.Dispose();
        _audio?.Dispose();
        _audioStream?.Dispose();
        _stream?.Dispose(); // débloque la lecture en cours sur le thread vidéo

        if (_videoThread != null && _videoThread != Thread.CurrentThread)
            _videoThread.Join(TimeSpan.FromSeconds(2));

        if (_server != null)
            await _server.DisposeAsync().ConfigureAwait(false);
    }
}
