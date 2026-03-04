using System;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Hranitel;

public partial class App : System.Windows.Application
{
    private const string MutexName = "WindowsGift_SingleInstance";
    private const string ShowWindowEventName = "WindowsGift_ShowWindow";
    private static Mutex? _mutex;
    private static EventWaitHandle? _showWindowEvent;
    private NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private Thread? _showWindowThread;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            ShowError("Ошибка", (ex.ExceptionObject as Exception)?.ToString() ?? "Неизвестная ошибка");
        DispatcherUnhandledException += (_, ex) =>
        {
            ShowError("Ошибка", ex.Exception.ToString());
            ex.Handled = true;
        };
        try
        {
            var createdNew = false;
            try
            {
                _mutex = new Mutex(true, MutexName, out createdNew);
            }
            catch (AbandonedMutexException)
            {
                createdNew = true;
                _mutex = new Mutex(true, MutexName);
            }
            if (!createdNew)
            {
                try { EventWaitHandle.OpenExisting(ShowWindowEventName).Set(); } catch { }
                Shutdown();
                return;
            }

        base.OnStartup(e);

            _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
            _showWindowThread = new Thread(ShowWindowLoop) { IsBackground = true };
            _showWindowThread.Start();

        _mainWindow = new MainWindow();
        _mainWindow.Closing += (_, ev) => { ev.Cancel = true; _mainWindow.Hide(); };
        // Окно не показываем при старте — только в трее. Исключение: аргумент --show (после установки).
        var showOnStart = e.Args != null && Array.Exists(e.Args, a => string.Equals(a, "--show", StringComparison.OrdinalIgnoreCase));
        if (showOnStart)
            _mainWindow.Show();

        _trayIcon = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "Хранитель",
            Visible = true
        };
        void ShowMainWindow()
        {
            _mainWindow.Show();
            _mainWindow.Activate();
        }
        _trayIcon.Click += (_, _) => ShowMainWindow();
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) =>
        {
            _mainWindow.Show();
            _mainWindow.Activate();
        });
        // "Выход" убран — блокировка работает в фоне. Остановить можно только удалением приложения.
        _trayIcon.ContextMenuStrip = menu;
        }
        catch (Exception ex)
        {
            ShowError("Ошибка запуска", ex.ToString());
            Shutdown();
        }
    }

    private static void ShowError(string title, string message)
    {
        // Запись в error.log отключена
        //try
        //{
        //    var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hranitel", "error.log");
        //    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        //    File.AppendAllText(path, $"\n[{DateTime.Now}]\n{title}\n{message}\n");
        //}
        //catch { }
        System.Windows.Forms.MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void ShowWindowLoop()
    {
        while (_showWindowEvent != null)
        {
            try
            {
                if (_showWindowEvent.WaitOne(500))
                {
                    try
                    {
                        Dispatcher.Invoke(() => { _mainWindow?.Show(); _mainWindow?.Activate(); });
                    }
                    catch (TaskCanceledException) { /* приложение закрывается */ }
                    catch (InvalidOperationException) { /* Dispatcher уже остановлен */ }
                }
            }
            catch (ObjectDisposedException) { break; }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWindowEvent?.Set();
        _showWindowEvent?.Dispose();
        _showWindowEvent = null;
        try { _mutex?.ReleaseMutex(); } catch (Exception) { /* не владеем мьютексом или уже освобождён */ }
        _mutex?.Dispose();
        if (_mainWindow?.DataContext is ViewModels.MainViewModel vm)
            vm.Stop();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }

    private static Icon CreateIcon()
    {
        var bmp = CreateIconBitmap();
        var hicon = bmp.GetHicon();
        var icon = Icon.FromHandle(hicon);
        bmp.Dispose();
        return icon;
    }

    /// <summary> ImageSource для иконки окна (Хранитель в диспетчере). Exe без встроенной иконки — Windows Gift без иконки. </summary>
    public static ImageSource CreateWindowIconSource()
    {
        using var icon = CreateIcon();
        return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            System.Windows.Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
    }

    private static Bitmap CreateIconBitmap()
    {
        var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.FromArgb(26, 27, 34)); // #1A1B22
            using var brush = new SolidBrush(System.Drawing.Color.FromArgb(189, 176, 208)); // #BDB0D0 Pastel Lilac
            var pts = new System.Drawing.PointF[]
            {
                new(16, 4), new(26, 10), new(26, 18),
                new(16, 28), new(6, 18), new(6, 10)
            };
            g.FillPolygon(brush, pts);
            using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(127, 181, 168), 1); // #7FB5A8 Mint
            g.DrawPolygon(pen, Array.ConvertAll(pts, p => new System.Drawing.Point((int)p.X, (int)p.Y)));
        }
        return bmp;
    }
}
