namespace PhoneMirror.Core;

/// <summary>Image décodée au format BGRA 32 bits.</summary>
public sealed class VideoFrame
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int Stride => Width * 4;

    /// <summary>Tampon épinglé en mémoire, éventuellement plus grand que l'image.</summary>
    public byte[] Pixels { get; private set; } = [];

    internal void EnsureSize(int width, int height)
    {
        Width = width;
        Height = height;
        int needed = width * height * 4;
        if (Pixels.Length < needed)
            Pixels = GC.AllocateUninitializedArray<byte>(needed, pinned: true);
    }
}

/// <summary>
/// Triple tampon entre le thread de décodage et le thread d'affichage, sans copie.
/// L'affichage récupère toujours l'image la plus récente : si l'écran est plus lent
/// que le décodage, les images intermédiaires sont sautées au lieu de s'accumuler,
/// ce qui empêche la latence de grimper.
/// </summary>
public sealed class FrameExchange
{
    private readonly Lock _gate = new();
    private VideoFrame _back = new();   // en cours d'écriture (thread de décodage)
    private VideoFrame _ready = new();  // dernière image terminée
    private VideoFrame _front = new();  // en cours d'affichage (thread UI)
    private bool _hasNewFrame;

    /// <summary>Thread de décodage uniquement.</summary>
    public VideoFrame BeginWrite(int width, int height)
    {
        _back.EnsureSize(width, height);
        return _back;
    }

    /// <summary>Thread de décodage uniquement.</summary>
    public void CommitWrite()
    {
        lock (_gate)
        {
            (_back, _ready) = (_ready, _back);
            _hasNewFrame = true;
        }
    }

    /// <summary>
    /// Thread d'affichage uniquement. Renvoie la dernière image, ou null s'il n'y en a pas de nouvelle.
    /// L'image reste valide jusqu'au prochain appel.
    /// </summary>
    public VideoFrame? TryAcquireLatest()
    {
        lock (_gate)
        {
            if (!_hasNewFrame) return null;
            (_front, _ready) = (_ready, _front);
            _hasNewFrame = false;
            return _front;
        }
    }
}
