using Velopack;

namespace PhoneMirror;

/// <summary>
/// Point d'entrée. Velopack doit s'exécuter avant tout le reste : lors d'une installation,
/// mise à jour ou désinstallation, il traite la demande puis quitte aussitôt.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main()
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
