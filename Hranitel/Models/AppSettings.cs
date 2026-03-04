using System;
using System.Collections.Generic;

namespace Hranitel.Models;

public class AppSettings
{
    public TimeSpan BlockStart { get; set; } = new(0, 0, 0);   // 00:00
    public TimeSpan BlockEnd { get; set; } = new(8, 0, 0);     // 08:00
    public bool BlockingEnabled { get; set; } = false;
    public DateTime BlockingTurnedOnAt { get; set; }
    public List<BlockedApp> BlockedApps { get; set; } = new();
    public bool Locked { get; set; }
    public DateTime LockedUntil { get; set; }
}

public class BlockedApp
{
    public string Name { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string? FullPath { get; set; }
    public bool Enabled { get; set; } = true;
    /// <summary> Время добавления. Старые записи без поля = всегда можно удалить. </summary>
    public DateTime AddedAt { get; set; }
    /// <summary> Когда тумблер включили. 10 мин после этого — нельзя выключить. </summary>
    public DateTime ToggledOnAt { get; set; }
}
