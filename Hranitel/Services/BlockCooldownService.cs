using System;
using System.Windows.Threading;
using Hranitel.Constants;

namespace Hranitel.Services;

/// <summary> Управляет 10-минутным кулдауном после включения главной блокировки. </summary>
public class BlockCooldownService
{
    private DispatcherTimer? _timer;
    private DateTime _turnedOnAt;
    private bool _blockingEnabled;

    /// <summary> Вызывается при каждом тике (каждые 30 сек) для обновления UI. </summary>
    public event Action? OnCooldownTick;

    public void Start(DateTime turnedOnAt)
    {
        _blockingEnabled = true;
        _turnedOnAt = turnedOnAt;
        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(CooldownConstants.CooldownPollIntervalSeconds) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void Stop()
    {
        _blockingEnabled = false;
        _timer?.Stop();
    }

    public bool IsInCooldown => _blockingEnabled && DateTime.Now < _turnedOnAt.AddMinutes(CooldownConstants.BlockCooldownMinutes);

    private void OnTick(object? s, EventArgs e)
    {
        OnCooldownTick?.Invoke();
        if (!_blockingEnabled || DateTime.Now >= _turnedOnAt.AddMinutes(CooldownConstants.BlockCooldownMinutes))
            _timer?.Stop();
    }
}
