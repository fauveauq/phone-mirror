using System.Windows;
using System.Windows.Threading;

namespace PhoneMirror;

public partial class App : Application
{
    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        PhoneMirror.Core.AppLog.Write("Erreur inattendue : " + e.Exception);
        MessageBox.Show(e.Exception.Message, "Phone Mirror — erreur inattendue",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
