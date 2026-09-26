using System.Windows;

namespace Aliceestrap.UI.Elements.Bootstrapper.Base
{
    static class BaseFunctions
    {
        public static void ShowSuccess(string message, Action? callback)
        {
            Frontend.ShowMessageBox(message, MessageBoxImage.Information);

            if (callback is not null)
                callback();

            App.Terminate();
        }
    }
}
