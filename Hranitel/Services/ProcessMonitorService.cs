using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hranitel.Models;

namespace Hranitel.Services;

public class ProcessMonitorService
{
    [System.Diagnostics.Conditional("DEBUG_OFF")]
    private static void Dbg(string msg, object? data = null) { }
    private readonly Func<AppSettings> _getSettings;
    private readonly SettingsService _settingsService = new();

    public ProcessMonitorService(Func<AppSettings>? getSettings = null)
    {
        _getSettings = getSettings ?? (() => _settingsService.Load());
    }
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private const int PollIntervalMs = 500;
    private readonly HashSet<int> _recentlyKilled = new();
    private const int RecentlyKilledTtlMs = 2000;

    public event Action<string>? OnBlockedAppLaunched;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _monitorTask = Task.Run(() => MonitorLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _monitorTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException ex)
        {
            ex.Handle(e => e is OperationCanceledException or TaskCanceledException);
        }
        catch (TaskCanceledException) { /* нормально при выходе */ }
        catch (OperationCanceledException) { /* нормально при выходе */ }
    }

    private async Task MonitorLoop(CancellationToken ct)
    {
        var lastCleanup = DateTime.UtcNow;
        var lastLog = DateTime.MinValue;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var settings = _getSettings();
                if (!settings.BlockingEnabled)
                {
                    if ((DateTime.UtcNow - lastLog).TotalSeconds > 2)
                    {
                        Dbg("skip", new { hypothesisId = "H2", BlockingEnabled = settings.BlockingEnabled, Locked = settings.Locked });
                        lastLog = DateTime.UtcNow;
                    }
                    await Task.Delay(PollIntervalMs, ct);
                    continue;
                }

                var inBlockedTime = IsInBlockedTime(settings.BlockStart, settings.BlockEnd);
                if (!inBlockedTime)
                {
                    if ((DateTime.UtcNow - lastLog).TotalSeconds > 2)
                    {
                        Dbg("skip", new { hypothesisId = "H3", BlockStart = settings.BlockStart.ToString(), BlockEnd = settings.BlockEnd.ToString(), Now = DateTime.Now.TimeOfDay.ToString() });
                        lastLog = DateTime.UtcNow;
                    }
                    await Task.Delay(PollIntervalMs, ct);
                    continue;
                }

                List<Process> toKill;
                try
                {
                    toKill = GetBlockedAppsToKill(settings);
                }
                catch (Exception ex)
                {
                    Dbg("GetBlockedAppsToKill error", new { hypothesisId = "H8", Err = ex.ToString() });
                    await Task.Delay(PollIntervalMs, ct);
                    continue;
                }
                if (toKill.Count > 0)
                {
                    Dbg("toKill", new { hypothesisId = "H4", Count = toKill.Count, Names = toKill.Select(p => p.ProcessName).ToList() });
                    lastLog = DateTime.UtcNow;
                }
                else if ((DateTime.UtcNow - lastLog).TotalSeconds > 3)
                {
                    Dbg("blocking active, toKill=0", new { hypothesisId = "H4", BlockedCount = settings.BlockedApps.Count(x => x.Enabled), Apps = settings.BlockedApps.Where(a => a.Enabled).Select(a => a.ProcessName).ToList() });
                    lastLog = DateTime.UtcNow;
                }
                foreach (var proc in toKill)
                {
                    try
                    {
                        if (_recentlyKilled.Contains(proc.Id))
                            continue;
                        if (proc.HasExited) continue;

                        var killOk = false;
                        try
                        {
                            proc.Kill(entireProcessTree: true);
                            killOk = true;
                        }
                        catch (System.ComponentModel.Win32Exception ex)
                        {
                            TryTaskKill(proc.Id);
                            Dbg("Kill fallback", new { hypothesisId = "H5", Id = proc.Id, Name = proc.ProcessName, Err = ex.Message });
                        }
                        Dbg(killOk ? "Kill ok" : "Kill via taskkill", new { hypothesisId = "H5", Id = proc.Id, Name = proc.ProcessName });
                        _recentlyKilled.Add(proc.Id);
                        OnBlockedAppLaunched?.Invoke(proc.ProcessName);
                    }
                    catch { /* ignore */ }
                }

                if ((DateTime.UtcNow - lastCleanup).TotalMilliseconds > RecentlyKilledTtlMs)
                {
                    _recentlyKilled.Clear();
                    lastCleanup = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* ignore */ }

            await Task.Delay(PollIntervalMs, ct);
        }
    }

    private static bool IsInBlockedTime(TimeSpan start, TimeSpan end)
    {
        var now = DateTime.Now.TimeOfDay;

        if (start > end)
            return now >= start || now < end;

        return now >= start && now < end;
    }

    private static DateTime _lastNoMatchLog = DateTime.MinValue;
    private static List<Process> GetBlockedAppsToKill(AppSettings settings)
    {
        var result = new List<Process>();
        var addedIds = new HashSet<int>();
        var processes = Process.GetProcesses();
        var enabled = settings.BlockedApps.Where(a => a.Enabled).ToList();

        foreach (var app in enabled)
        {
            foreach (var proc in processes)
            {
                try
                {
                    if (proc.HasExited) continue;
                    if (Matches(app, proc) && addedIds.Add(proc.Id))
                        result.Add(proc);
                }
                catch (InvalidOperationException)
                {
                    try { proc.Dispose(); } catch { }
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    try { proc.Dispose(); } catch { }
                }
                catch (Exception)
                {
                    try { proc.Dispose(); } catch { }
                }
            }
        }
        foreach (var proc in processes)
        {
            try
            {
                if (proc.HasExited)
                {
                    proc.Dispose();
                    continue;
                }
                if (!addedIds.Contains(proc.Id))
                    proc.Dispose();
            }
            catch (InvalidOperationException)
            {
                try { proc.Dispose(); } catch { }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                try { proc.Dispose(); } catch { }
            }
        }

        // #region agent log
        if (result.Count == 0 && enabled.Count > 0 && (DateTime.UtcNow - _lastNoMatchLog).TotalSeconds > 5)
        {
            _lastNoMatchLog = DateTime.UtcNow;
            var runningNames = Process.GetProcesses().Select(p => { try { var n = p.ProcessName; p.Dispose(); return n; } catch { return null; } }).Where(x => x != null).Take(50).ToList();
            Dbg("no matches", new { hypothesisId = "H6", BlockedApps = enabled.Select(a => new { a.ProcessName, a.FullPath }).ToList(), RunningCount = Process.GetProcesses().Length, SampleProcessNames = runningNames });
        }
        // #endregion

        return result;
    }

    private static string NormalizeProcessName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return name.Replace('\u00B5', 'u'); // µ (micro) -> u for uTorrent
    }

    private static bool Matches(BlockedApp app, Process proc)
    {
        var procName = proc.ProcessName;
        if (string.IsNullOrEmpty(procName)) return false;

        var appProcName = app.ProcessName;
        if (appProcName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            appProcName = appProcName[..^4];
        var matchByName = string.Equals(NormalizeProcessName(procName), NormalizeProcessName(appProcName), StringComparison.OrdinalIgnoreCase);
        if (matchByName && string.IsNullOrEmpty(app.FullPath))
            return true;

        if (!string.IsNullOrEmpty(app.FullPath))
        {
            try
            {
                var procPath = GetProcessPath(proc.Id);
                if (!string.IsNullOrEmpty(procPath))
                {
                    if (string.Equals(procPath, app.FullPath, StringComparison.OrdinalIgnoreCase))
                        return true;
                    // #region agent log
                    if (matchByName)
                        Dbg("path mismatch", new { hypothesisId = "H7", appProcName, procName, appFullPath = app.FullPath, procPath });
                    // #endregion
                }
            }
            catch (Exception ex)
            {
                // #region agent log
                if (matchByName)
                    Dbg("GetProcessPath error", new { hypothesisId = "H7", procName, appProcName, err = ex.Message });
                // #endregion
            }
        }

        return matchByName;
    }

    private static void TryTaskKill(int processId)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/F /PID {processId}",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            p?.WaitForExit(2000);
        }
        catch { /* ignore */ }
    }

    private static string? GetProcessPath(int processId)
    {
        try
        {
            using var proc = Process.GetProcessById(processId);
            return proc.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }
}
