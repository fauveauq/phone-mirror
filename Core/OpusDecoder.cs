using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;

namespace PhoneMirror.Core;

/// <summary>Reçoit des échantillons audio décodés (float 32 bits entrelacés).</summary>
public interface IAudioSink
{
    void Write(ReadOnlySpan<byte> samples);
}

/// <summary>
/// Décode l'Opus du téléphone (48 kHz stéréo, comme le configure scrcpy) avec FFmpeg,
/// et le convertit en float 32 bits entrelacé. Aucun bloc n'est retenu dans le décodeur.
/// </summary>
public sealed unsafe class OpusDecoder : IDisposable
{
    public const int SampleRate = 48000;
    public const int Channels = 2;

    private readonly int _errorAgain = ffmpeg.AVERROR(ffmpeg.EAGAIN);
    private AVCodecContext* _context;
    private AVPacket* _packet;
    private AVFrame* _frame;
    private float[] _interleaved = new float[4096];

    public long ErrorCount { get; private set; }

    public OpusDecoder()
    {
        try
        {
            AVCodec* codec = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_OPUS);
            if (codec == null) throw new InvalidOperationException("FFmpeg ne contient pas de décodeur Opus.");

            _context = ffmpeg.avcodec_alloc_context3(codec);
            if (_context == null) throw new InvalidOperationException("Allocation du décodeur audio impossible.");

            _context->sample_rate = SampleRate;
            ffmpeg.av_channel_layout_default(&_context->ch_layout, Channels);
            FfmpegLoader.Check(ffmpeg.avcodec_open2(_context, codec, null), "l'ouverture du décodeur audio");

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

    /// <summary>Décode un paquet Opus et transmet les échantillons obtenus.</summary>
    public void Decode(ReadOnlySpan<byte> data, IAudioSink sink)
    {
        FfmpegLoader.Check(ffmpeg.av_new_packet(_packet, data.Length), "l'allocation d'un paquet audio");
        data.CopyTo(new Span<byte>(_packet->data, data.Length));

        int result = ffmpeg.avcodec_send_packet(_context, _packet);
        ffmpeg.av_packet_unref(_packet);
        if (result < 0)
        {
            ErrorCount++;
            return;
        }

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
                sink.Write(MemoryMarshal.AsBytes(Interleave().AsSpan()));
            }
            finally
            {
                ffmpeg.av_frame_unref(_frame);
            }
        }
    }

    /// <summary>Convertit le bloc décodé (souvent en float « planaire », un tableau par canal) en float entrelacé.</summary>
    private ArraySegment<float> Interleave()
    {
        int samples = _frame->nb_samples;
        int channels = _frame->ch_layout.nb_channels;
        int total = samples * channels;
        if (_interleaved.Length < total) _interleaved = new float[total];

        var format = (AVSampleFormat)_frame->format;
        if (format == AVSampleFormat.AV_SAMPLE_FMT_FLTP)
        {
            for (int c = 0; c < channels; c++)
            {
                float* plane = (float*)_frame->extended_data[c];
                for (int i = 0; i < samples; i++) _interleaved[i * channels + c] = plane[i];
            }
        }
        else if (format == AVSampleFormat.AV_SAMPLE_FMT_FLT)
        {
            new ReadOnlySpan<float>(_frame->data[0], total).CopyTo(_interleaved);
        }
        else
        {
            throw new InvalidDataException($"Format audio inattendu : {format}.");
        }

        return new ArraySegment<float>(_interleaved, 0, total);
    }

    public void Dispose()
    {
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
