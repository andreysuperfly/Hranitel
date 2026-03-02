using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Hranitel.Constants;
using Hranitel.Models;
using Hranitel.Services;
using Microsoft.Win32;

namespace Hranitel.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly SettingsService _settingsService = new();
    private readonly ProcessMonitorService _processMonitor;

    private TimeSpan _blockStart = new(0, 0, 0);
    private TimeSpan _blockEnd = new(8, 0, 0);
    private bool _blockingEnabled = false;
    private DateTime _blockingTurnedOnAt;
    private bool _locked;
    private DateTime _lockedUntil;
    private bool _lockToggleEnabled = true;
    private readonly BlockCooldownService _blockCooldownService;
    private readonly AppCooldownService _appCooldownService;

    public MainViewModel()
    {
        _processMonitor = new ProcessMonitorService(BuildSettingsForMonitor);
        _blockCooldownService = new BlockCooldownService();
        _blockCooldownService.OnCooldownTick += () => OnPropertyChanged(nameof(CanEditAppsAndSchedule));
        _appCooldownService = new AppCooldownService(() => BlockedApps);
        SelectCommand = new RelayCommand(_ => PickFile());
        ToggleLockCommand = new RelayCommand(_ => ToggleLock(), _ => _lockToggleEnabled);
        CopyLogsCommand = new RelayCommand(_ => CopyLogs());

        LoadSettings();

        _processMonitor.OnBlockedAppLaunched += appName =>
        {
            ToastService.ShowBlocked(appName);
        };

        AddAutostart(); // после LoadSettings; нужен только exe path

        BlockedApps.CollectionChanged += (_, _) => UpdateListHeight();
    }

    private const int ItemHeight = 52;
    private const int ListHeaderHeight = 52;
    private const int MaxVisibleItems = 7;

    public double ListAreaHeight
    {
        get
        {
            var count = BlockedApps.Count;
            var visible = Math.Max(4, Math.Min(count, MaxVisibleItems));
            return ListHeaderHeight + visible * ItemHeight + 16;
        }
    }

    private void UpdateListHeight()
    {
        OnPropertyChanged(nameof(ListAreaHeight));
        OnPropertyChanged(nameof(BlockedAppsCount));
    }

    public int BlockedAppsCount => BlockedApps.Count;

    public TimeSpan BlockStart
    {
        get => _blockStart;
        set
        {
            if (!CanEditAppsAndSchedule && value != _blockStart)
            {
                var msg = _locked ? "Настройки заблокированы на 2 дня." :
                    IsInBlockedTime(_blockStart, _blockEnd) ? "Нельзя изменить расписание во время действия блокировки." :
                    $"Нельзя изменить расписание раньше чем через {CooldownConstants.BlockCooldownMinutes} минут после включения блокировки.";
                ToastService.Show(msg, "Хранитель");
                OnPropertyChanged(nameof(BlockStart));
                return;
            }
            _blockStart = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public TimeSpan BlockEnd
    {
        get => _blockEnd;
        set
        {
            if (!CanEditAppsAndSchedule && value != _blockEnd)
            {
                var msg = _locked ? "Настройки заблокированы на 2 дня." :
                    IsInBlockedTime(_blockStart, _blockEnd) ? "Нельзя изменить расписание во время действия блокировки." :
                    $"Нельзя изменить расписание раньше чем через {CooldownConstants.BlockCooldownMinutes} минут после включения блокировки.";
                ToastService.Show(msg, "Хранитель");
                OnPropertyChanged(nameof(BlockEnd));
                return;
            }
            _blockEnd = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public bool BlockingEnabled
    {
        get => _blockingEnabled;
        set
        {
            if (value)
            {
                _blockingEnabled = true;
                _blockingTurnedOnAt = DateTime.Now;
                _blockCooldownService.Start(_blockingTurnedOnAt);
                OnPropertyChanged(); OnPropertyChanged(nameof(CanEditAppsAndSchedule)); SaveSettings();
            }
            else
            {
                if (DateTime.Now < _blockingTurnedOnAt.AddMinutes(CooldownConstants.BlockCooldownMinutes))
                {
                    ToastService.Show($"Нельзя выключить блокировку раньше чем через {CooldownConstants.BlockCooldownMinutes} минут после включения.", "Хранитель");
                    OnPropertyChanged(nameof(BlockingEnabled));
                    return;
                }
                if (IsInBlockedTime(_blockStart, _blockEnd))
                {
                    ToastService.Show("Нельзя выключить блокировку во время действия блокировки.", "Хранитель");
                    OnPropertyChanged(nameof(BlockingEnabled));
                    return;
                }
                _blockingEnabled = false;
                _blockCooldownService.Stop();
                OnPropertyChanged(); OnPropertyChanged(nameof(CanEditAppsAndSchedule)); SaveSettings();
            }
        }
    }

    private static bool IsInBlockedTime(TimeSpan start, TimeSpan end)
    {
        var now = DateTime.Now.TimeOfDay;
        if (start > end)
            return now >= start || now < end;
        return now >= start && now < end;
    }

    private void StartAppCooldownTimer() => _appCooldownService.Start();

    public bool Locked
    {
        get => _locked;
        set { _locked = value; OnPropertyChanged(); }
    }

    public bool LockToggleEnabled
    {
        get => _lockToggleEnabled;
        set { _lockToggleEnabled = value; OnPropertyChanged(); }
    }

    /// <summary> false = нельзя редактировать расписание (10 мин после включения или во время блокировки) </summary>
    public bool CanEditAppsAndSchedule =>
        !_locked && !(_blockingEnabled && (
            DateTime.Now < _blockingTurnedOnAt.AddMinutes(CooldownConstants.BlockCooldownMinutes) ||
            IsInBlockedTime(_blockStart, _blockEnd)));

    /// <summary> Можно добавлять приложения даже при 2-дневном замке. </summary>
    public bool CanAddApps => true;

    public string LockedUntilText => _locked ? $"Замок до {_lockedUntil:dd.MM.yyyy HH:mm}" : string.Empty;

    public ObservableCollection<BlockedAppViewModel> BlockedApps { get; } = new();

    public ICommand SelectCommand { get; }
    public ICommand ToggleLockCommand { get; }
    public ICommand CopyLogsCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? OnRequestClose;

    private void CopyLogs()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hranitel");
        var parts = new List<string>();
        foreach (var name in new[] { "monitor.log", "error.log" })
        {
            try
            {
                var p = Path.Combine(dir, name);
                if (File.Exists(p))
                    parts.Add($"=== {name} ===\n{File.ReadAllText(p)}");
            }
            catch { }
        }
        var content = parts.Count > 0 ? string.Join("\n\n", parts) : null;
        try
        {
            if (!string.IsNullOrEmpty(content))
            {
                Clipboard.SetText(content);
                ToastService.Show("Логи скопированы в буфер обмена.", "Хранитель");
            }
            else
            {
                ToastService.Show("Файл логов не найден.", "Хранитель");
            }
        }
        catch (Exception ex)
        {
            ToastService.Show($"Ошибка: {ex.Message}", "Хранитель");
        }
    }

    public void Start()
    {
        _processMonitor.Start();
    }

    public void Stop()
    {
        _processMonitor.Stop();
    }

    public void LoadSettings()
    {
        var s = _settingsService.Load();
        _blockStart = s.BlockStart;
        _blockEnd = s.BlockEnd;
        _blockingEnabled = s.BlockingEnabled;
        _blockingTurnedOnAt = s.BlockingTurnedOnAt;
        if (s.Locked && DateTime.Now < s.LockedUntil)
        {
            _locked = true;
            _lockedUntil = s.LockedUntil;
            _lockToggleEnabled = false;
        }
        else
        {
            _locked = false;
            _lockedUntil = default;
            _lockToggleEnabled = true;
        }

        BlockedApps.Clear();
        foreach (var a in s.BlockedApps)
            BlockedApps.Add(new BlockedAppViewModel(a, this));

        if (_blockingEnabled && DateTime.Now < _blockingTurnedOnAt.AddMinutes(CooldownConstants.BlockCooldownMinutes))
            _blockCooldownService.Start(_blockingTurnedOnAt);
        _appCooldownService.Start();

        OnPropertyChanged(nameof(BlockStart));
        OnPropertyChanged(nameof(BlockEnd));
        OnPropertyChanged(nameof(BlockingEnabled));
        OnPropertyChanged(nameof(Locked));
        OnPropertyChanged(nameof(LockToggleEnabled));
        OnPropertyChanged(nameof(CanEditAppsAndSchedule));
        OnPropertyChanged(nameof(CanAddApps));
        OnPropertyChanged(nameof(LockedUntilText));
        UpdateListHeight();
    }

    private AppSettings BuildSettingsForMonitor()
    {
        if (!Application.Current.Dispatcher.CheckAccess())
            return (AppSettings)Application.Current.Dispatcher.Invoke(BuildSettingsForMonitor);
        return BuildSettingsForMonitorCore();
    }

    private AppSettings BuildSettingsForMonitorCore()
    {
        return new AppSettings
        {
            BlockStart = BlockStart,
            BlockEnd = BlockEnd,
            BlockingEnabled = BlockingEnabled,
            Locked = _locked,
            BlockedApps = BlockedApps.Select(x => x.Model).ToList()
        };
    }

    public void SaveSettings()
    {
        if (_locked) return;

        var s = new AppSettings
        {
            BlockStart = BlockStart,
            BlockEnd = BlockEnd,
            BlockingEnabled = BlockingEnabled,
            BlockingTurnedOnAt = _blockingTurnedOnAt,
            Locked = _locked,
            LockedUntil = _lockedUntil,
            BlockedApps = BlockedApps.Select(x => x.Model).ToList()
        };
        _settingsService.Save(s);
    }

    /// <param name="path">Путь к .exe (вызывается только для exe; .url/.lnk обрабатываются выше).</param>
    public void AddAppFromPath(string path)
    {
        if (!CanAddApps) return;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        var name = AppNameResolver.GetDisplayName(path);
        var procName = Path.GetFileNameWithoutExtension(path);
        if (BlockedApps.Any(x => string.Equals(x.FullPath, path, StringComparison.OrdinalIgnoreCase)))
            return;

        var app = new BlockedApp
        {
            Name = name,
            ProcessName = procName,
            FullPath = path,
            Enabled = false,
            AddedAt = DateTime.Now
        };
        var vm = new BlockedAppViewModel(app, this);
        BlockedApps.Add(vm);
        StartAppCooldownTimer();
        SaveSettings();
    }

    public void AddAppFromShortcut(string shortcutPath)
    {
        var target = ShortcutParser.ResolveShortcut(shortcutPath);
        if (!string.IsNullOrEmpty(target))
            AddAppFromPath(target);
    }

    private void AddAppFromUrlFile(string urlFilePath)
    {
        var url = SteamResolver.ParseUrlFile(urlFilePath);
        if (string.IsNullOrEmpty(url) || !SteamResolver.IsSteamUrl(url))
        {
            ToastService.Show("Не удалось добавить: неверный формат ярлыка или это не ярлык Steam.", "Хранитель");
            return;
        }
        AddAppFromSteamUrl(url);
    }

    private void AddAppFromSteamUrl(string steamUrl)
    {
        var steamPath = SteamResolver.GetSteamPath();
        if (string.IsNullOrEmpty(steamPath))
        {
            ToastService.Show("Steam не найден. Добавьте steam.exe вручную через «Выбрать».", "Хранитель");
            return;
        }
        AddAppFromPath(steamPath);
    }

    public void RemoveApp(BlockedAppViewModel vm)
    {
        if (!vm.CanRemoveApp)
        {
            ToastService.Show("Нельзя удалить приложение в течение 10 минут после добавления.", "Хранитель");
            return;
        }
        if (System.Windows.MessageBox.Show(
                "Точно ли хочешь продолжить разрушать свою жизнь?",
                "Удалить приложение",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
            return;
        BlockedApps.Remove(vm);
        SaveSettings();
    }

    public void ToggleLock()
    {
        if (_locked) return;
        if (System.Windows.MessageBox.Show(
                "Уверены? Настройки будут заблокированы на 2 дня.",
                "Хранитель",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
            return;
        Locked = true;
        LockToggleEnabled = false;
        _lockedUntil = DateTime.Now.AddDays(CooldownConstants.LockDays);
        SaveSettings();
        OnPropertyChanged(nameof(LockedUntilText));
    }

    public void HandleDrop(string path)
    {
        if (string.IsNullOrEmpty(path) || !CanAddApps) return;

        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            AddAppFromShortcut(path);
            return;
        }

        if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
        {
            AddAppFromUrlFile(path);
            return;
        }

        if (SteamResolver.IsSteamUrl(path))
        {
            AddAppFromSteamUrl(path);
            return;
        }

        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            AddAppFromPath(path);
    }

    public void PickFile()
    {
        if (!CanAddApps) return;

        var dlg = new OpenFileDialog
        {
            Filter = "Исполняемые файлы|*.exe|Ярлыки|*.lnk|Ярлыки Steam (.url)|*.url|Все файлы|*.*",
            Title = "Выберите приложение"
        };
        if (dlg.ShowDialog() == true)
        {
            if (dlg.FileName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                AddAppFromShortcut(dlg.FileName);
            else if (dlg.FileName.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                AddAppFromUrlFile(dlg.FileName);
            else
                AddAppFromPath(dlg.FileName);
        }
    }

    private void AddAutostart()
    {
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath) && !AutostartService.IsEnabled())
            AutostartService.Enable(exePath);
    }

    protected void OnPropertyChanged(string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
