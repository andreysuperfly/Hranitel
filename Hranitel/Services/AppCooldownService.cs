using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using Hranitel.Constants;
using Hranitel.ViewModels;

namespace Hranitel.Services;

/// <summary> Управляет таймером проверки кулдаунов приложений (удаление, тумблер). Останавливается, когда все приложения прошли кулдаун. </summary>
public class AppCooldownService
{
    private DispatcherTimer? _timer;
    private Func<ObservableCollection<BlockedAppViewModel>> _getApps;

    public AppCooldownService(Func<ObservableCollection<BlockedAppViewModel>> getApps)
    {
        _getApps = getApps;
    }

    public void Start()
    {
        var apps = _getApps();
        if (apps.Count == 0) return;

        _timer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(CooldownConstants.CooldownPollIntervalSeconds) };
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        if (!_timer.IsEnabled)
            _timer.Start();
    }

    private static bool IsAppPastCooldown(BlockedAppViewModel vm)
    {
        if (!vm.CanRemoveApp) return false;
        if (!vm.Model.Enabled) return true;
        if (vm.Model.ToggledOnAt == default) return true;
        return DateTime.Now >= vm.Model.ToggledOnAt.AddMinutes(CooldownConstants.AppToggleCooldownMinutes);
    }

    private void OnTick(object? s, EventArgs e)
    {
        var apps = _getApps();
        if (apps.All(IsAppPastCooldown))
            _timer?.Stop();
    }
}
