using System.Windows;
using System.Windows.Input;
using Hranitel.Services;
using Hranitel.ViewModels;

namespace Hranitel;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        Icon = App.CreateWindowIconSource(); // иконка окна Хранитель (в диспетчере при раскрытии)
        _vm = new MainViewModel();
        DataContext = _vm;

        DropZone.DragEnter += OnDragEnter;
        DropZone.DragLeave += OnDragLeave;
        DropZone.Drop += OnDrop;
        DropZone.PreviewDragOver += OnPreviewDragOver;

        DropZoneInner.DragEnter += OnDragEnter;
        DropZoneInner.DragLeave += OnDragLeave;
        DropZoneInner.Drop += OnDrop;
        DropZoneInner.PreviewDragOver += OnPreviewDragOver;

        AppList.DragEnter += OnDragEnter;
        AppList.DragLeave += OnDragLeave;
        AppList.Drop += OnDrop;
        AppList.PreviewDragOver += OnPreviewDragOver;

        Loaded += (_, _) => _vm.Start();
        Closing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };
    }

    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (DragDropHelper.HasSupportedData(e.Data))
            DropZone.BorderBrush = System.Windows.Media.Brushes.DodgerBlue;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        DropZone.BorderBrush = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3D3834"));
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        DropZone.BorderBrush = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3D3834"));

        var path = DragDropHelper.TryGetPath(e.Data);
        if (!string.IsNullOrEmpty(path))
            _vm.HandleDrop(path);
    }
}
