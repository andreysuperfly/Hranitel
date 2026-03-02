using System;
using System.Windows;

namespace Hranitel.Services;

public static class ToastService
{
    private const string Message = "Братишка, займись делом — хватит прожигать время, жизнь не вечная";

    public static void ShowBlocked(string appName)
    {
        Show(Message, $"Хранитель — {appName} заблокирован");
    }

    public static void Show(string message, string title = "Хранитель")
    {
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }
        catch { /* ignore */ }
    }
}
