using FFmpeg.AutoGen.Abstractions;

namespace PhoneMirror.Core;

/// <summary>
/// Décode le H.264 avec FFmpeg et convertit chaque image en BGRA.
/// Réglé pour la latence : aucune image n'est retenue dans le décodeur.
/// </summary>
public sealed unsafe class H264Decoder : IDisposable
{
    private readonly int _errorAgain = ffmpeg.AVERROR(ffmpeg.EAGAIN);
    private AVCodecContext* _context;
    private AVPacket* _packet;
    private AVFrame* _frame;
    private SwsContext* _scaler;

    /// <summary>Paquets ou images rejetés par le décodeur (flux corrompu, etc.).</summary>
    public long ErrorCount { get; private set; }

    public H264Decoder()
    {
        try
        {
            AVCodec* codec = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_H264);
            if (codec == null) throw new InvalidOperationException("FFmpeg ne contient pas de décodeur H.264.");

            _context = ffmpeg.avcodec_alloc_context3(codec);
            if (_context == null) throw new InvalidOperationException("Allocation du décodeur impossible.");

            // Le multithreading « par image » ajoute une image de retard par thread : on l'évite.
            _context->flags |= ffmpeg.AV_CODEC_FLAG_LOW_DELAY;
            _context->thread_count = 1;

            FfmpegLoader.Check(ffmpeg.avcodec_open2(_context, codec, null), "l'ouverture du décodeur");

            _packet = ffmpeg.av_packet_alloc();
            _frame = ffmpeg.av_frame_alloc();
            if (_packet == null || _frame == null) throw new InvalidOperationException("Allocation FFmpeg impossible.");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Décode un paquet (précédé de la configuration du codec si elle vient d'arriver)
    /// et publie les images obtenues. Renvoie le nombre d'images produites.
    /// </summary>
    public int Decode(ReadOnlySpan<byte> config, ReadOnlySpan<byte> data, FrameExchange output)
    {
        int size = config.Length + data.Length;
        FfmpegLoader.Check(ffmpeg.av_new_packet(_packet, size), "l'allocation d'un paquet");

        var destination = new Span<byte>(_packet->data, size);
        config.CopyTo(destination);
        data.CopyTo(destination[config.Length..]);

        int result = ffmpeg.avcodec_send_packet(_context, _packet);
        ffmpeg.av_packet_unref(_packet);
        if (result < 0)
        {
            // Paquet illisible : le décodeur se resynchronise sur l'image clé suivante.
            ErrorCount++;
            return 0;
        }

        int frames = 0;
        while (true)
        {
            result = ffmpeg.avcodec_receive_frame(_context, _frame);
            if (result == _errorAgain || result == ffmpeg.AVERROR_EOF) break;
            if (result < 0)
            {
                ErrorCount++;
                break;
            }

            try
            {
                ConvertTo(output);
                frames++;
            }
            finally
            {
                ffmpeg.av_frame_unref(_frame);
            }
        }

        return frames;
    }

    private void ConvertTo(FrameExchange output)
    {
        int width = _frame->width;
        int height = _frame->height;

        _scaler = ffmpeg.sws_getCachedContext(_scaler,
            width, height, (AVPixelFormat)_frame->format,
            width, height, AVPixelFormat.AV_PIX_FMT_BGRA,
            (int)SwsFlags.SWS_FAST_BILINEAR, null, null, null);
        if (_scaler == null) throw new InvalidOperationException("Conversion des couleurs impossible.");

        VideoFrame target = output.BeginWrite(width, height);
        fixed (byte* pixels = target.Pixels)
        {
            var destination = new byte*[] { pixels, null, null, null };
            var stride = new[] { target.Stride, 0, 0, 0 };
            ffmpeg.sws_scale(_scaler, _frame->data, _frame->linesize, 0, height, destination, stride);
        }

        output.CommitWrite();
    }

    public void Dispose()
    {
        if (_scaler != null)
        {
            ffmpeg.sws_freeContext(_scaler);
            _scaler = null;
        }

        if (_frame != null)
        {
            AVFrame* frame = _frame;
            ffmpeg.av_frame_free(&frame);
            _frame = null;
        }

        if (_packet != null)
        {
            AVPacket* packet = _packet;
            ffmpeg.av_packet_free(&packet);
            _packet = null;
        }

        if (_context != null)
        {
            AVCodecContext* context = _context;
            ffmpeg.avcodec_free_context(&context);
            _context = null;
        }
    }
}
