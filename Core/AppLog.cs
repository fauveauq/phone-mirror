using System.Diagnostics;

namespace PhoneMirror.Core;

/// <summary>
/// Journal de l'application, écrit dans %LOCALAPPDATA%\PhoneMirror\journal.log
/// (et dans la sortie de débogage). Le fichier est recréé à chaque lancement.
/// </summary>
public static class AppLog
{
    private static readonly Lock Gate = new();
    private static StreamWriter? _writer;

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhoneMirror", "journal.log");

    public static void Write(string message)
    {
        string line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
        Debug.WriteLine(line);

        lock (Gate)
        {
            try
            {
                if (_writer == null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                    _writer = new StreamWriter(FilePath, append: false) { AutoFlush = true };
                }
                _writer.WriteLine(line);
            }
            catch (IOException)
            {
                // Le journal ne doit jamais faire échouer l'application.
            }
        }
    }
}
