using System.Diagnostics;

namespace PhoneMirror.Core;

/// <summary>
/// Journal de l'application, écrit dans %APPDATA%\PhoneMirror\journal.log
/// (et dans la sortie de débogage). Le fichier est recréé à chaque lancement.
/// </summary>
public static class AppLog
{
    private static readonly Lock Gate = new();
    private static StreamWriter? _writer;

    /// <summary>
    /// Données de l'utilisateur (réglages, journal). Surtout pas %LOCALAPPDATA%\PhoneMirror :
    /// c'est le dossier d'installation de Velopack, effacé à la désinstallation.
    /// </summary>
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhoneMirror");

    public static string FilePath { get; } = Path.Combine(DataDirectory, "journal.log");

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
