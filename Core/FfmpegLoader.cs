using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;
using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;

namespace PhoneMirror.Core;

public static unsafe class FfmpegLoader
{
    private static readonly Lock Gate = new();
    private static bool _loaded;

    public static string Version { get; private set; } = "";

    public static void EnsureLoaded(string directory)
    {
        lock (Gate)
        {
            if (_loaded) return;

            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException($"Dossier FFmpeg introuvable : {directory}. Voir le README.");

            try
            {
                DynamicallyLoadedBindings.LibrariesPath = directory;
                DynamicallyLoadedBindings.Initialize();
                Version = ffmpeg.av_version_info();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Impossible de charger FFmpeg depuis {directory}. Il faut les DLL « shared » 64 bits " +
                    "dans la même version que le paquet FFmpeg.AutoGen.", ex);
            }

            _loaded = true;
        }
    }

    public static void Check(int result, string operation)
    {
        if (result < 0)
            throw new InvalidOperationException($"FFmpeg : échec de {operation} ({ErrorText(result)}).");
    }

    public static string ErrorText(int code)
    {
        const int size = 256;
        byte* buffer = stackalloc byte[size];
        ffmpeg.av_strerror(code, buffer, size);
        return Marshal.PtrToStringAnsi((IntPtr)buffer) ?? $"code {code}";
    }
}
