namespace PhoneMirror.Core;

/// <summary>
/// Son du téléphone : lecture du flux, décodage Opus et lecture sur le PC, sur un thread dédié.
/// Une panne du son (pas de sortie audio sur le PC, téléphone trop ancien…) est journalisée
/// mais n'interrompt jamais la vidéo.
/// </summary>
public sealed class AudioPipeline : IDisposable
{
    private readonly ScrcpyAudioStream _stream;
    private readonly Action<string> _log;
    private readonly Thread _thread;
    private volatile bool _stopping;

    public AudioPipeline(ScrcpyAudioStream stream, Action<string> log)
    {
        _stream = stream;
        _log = log;
        _thread = new Thread(Run) { IsBackground = true, Name = "PhoneMirror audio", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    private void Run()
    {
        try
        {
            switch (_stream.ReadHeader())
            {
                case AudioAvailability.Disabled:
                    _log("Son indisponible : il faut Android 11 ou plus récent, et certaines applications l'interdisent.");
                    return;
                case AudioAvailability.Error:
                    _log("Le téléphone n'a pas pu démarrer la capture du son.");
                    return;
            }

            using var decoder = new OpusDecoder();
            using AudioPlayer? player = TryCreatePlayer();
            if (player != null) _log($"Son activé : {player.Description}.");

            while (!_stopping)
            {
                StreamPacket packet = _stream.ReadPacket();
                // Sans sortie audio, on continue de lire pour ne pas bloquer le téléphone.
                // Le paquet de configuration (OpusHead) n'est pas nécessaire en 48 kHz stéréo.
                if (player == null || packet.Kind != PacketKind.Frame) continue;
                decoder.Decode(_stream.GetPayload(packet), player);
            }
        }
        catch (Exception ex)
        {
            if (!_stopping) _log("Arrêt du son : " + ex.Message);
        }
    }

    private AudioPlayer? TryCreatePlayer()
    {
        try
        {
            return new AudioPlayer(OpusDecoder.SampleRate, OpusDecoder.Channels, _log);
        }
        catch (Exception ex)
        {
            _log("Aucune sortie audio utilisable sur le PC : " + ex.Message);
            return null;
        }
    }

    public void Dispose()
    {
        _stopping = true;
        _stream.Dispose(); // débloque la lecture en cours
        if (_thread != Thread.CurrentThread) _thread.Join(TimeSpan.FromSeconds(2));
    }
}
