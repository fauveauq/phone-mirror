using NAudio.Wave;

namespace PhoneMirror.Core;

/// <summary>
/// Joue le son sur la sortie audio par défaut de Windows (WASAPI partagé), en suivant
/// automatiquement les changements de sortie (casque branché…).
/// Le retard est plafonné : si le tampon dépasse <see cref="MaxBuffered"/> (téléphone et PC
/// qui ne vont pas exactement à la même vitesse, à-coups du réseau…), il est vidé.
/// Une courte coupure vaut mieux qu'un son de plus en plus décalé de l'image.
/// </summary>
public sealed class AudioPlayer : IAudioSink, IDisposable
{
    private const int OutputLatencyMs = 40;
    private static readonly TimeSpan MaxBuffered = TimeSpan.FromMilliseconds(150);

    private readonly BufferedWaveProvider _buffer;
    private readonly WasapiPlayer _output;

    /// <summary>Nombre de fois où le tampon a été vidé pour rattraper le retard.</summary>
    public long Resyncs { get; private set; }

    /// <summary>Description de la sortie obtenue, pour le journal.</summary>
    public string Description =>
        $"{(string.IsNullOrEmpty(_output.DeviceFriendlyName) ? "sortie par défaut de Windows" : _output.DeviceFriendlyName)}, "
        + $"latence {_output.LatencyMilliseconds} ms";

    /// <summary>À appeler depuis un thread dédié : l'ouverture de la sortie audio est bloquante.</summary>
    public AudioPlayer(int sampleRate, int channels, Action<string> log)
    {
        _buffer = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels), TimeSpan.FromSeconds(1))
        {
            DiscardOnBufferOverflow = true,
            ReadFully = true, // silence plutôt qu'un arrêt quand le tampon est vide
        };

        try
        {
            // Le suivi de la sortie par défaut est incompatible avec le mode faible latence IAudioClient3.
            _output = Open(_buffer, followDefaultDevice: true);
        }
        catch (Exception ex)
        {
            log("Suivi automatique de la sortie audio indisponible, sortie fixe utilisée : " + ex.Message);
            _output = Open(_buffer, followDefaultDevice: false);
        }
    }

    private static WasapiPlayer Open(IWaveProvider source, bool followDefaultDevice)
    {
        WasapiPlayerBuilder builder = new WasapiPlayerBuilder()
            .WithSharedMode()
            .WithEventSync()
            .WithLatency(OutputLatencyMs)
            .WithMmcssThreadPriority("Pro Audio");
        if (followDefaultDevice) builder = builder.WithDefaultDeviceStreamRouting();

        WasapiPlayer player = builder.BuildAsync().GetAwaiter().GetResult();
        try
        {
            player.Init(source);
            player.Play();
            return player;
        }
        catch
        {
            player.Dispose();
            throw;
        }
    }

    public void Write(ReadOnlySpan<byte> samples)
    {
        if (_buffer.BufferedDuration > MaxBuffered)
        {
            _buffer.ClearBuffer();
            Resyncs++;
        }
        _buffer.AddSamples(samples);
    }

    public void Dispose()
    {
        _output.Stop();
        _output.Dispose();
    }
}
