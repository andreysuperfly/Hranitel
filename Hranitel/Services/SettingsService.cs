using System;
using System.IO;
using System.Text.Json;
using Hranitel.Models;

namespace Hranitel.Services;

public class SettingsService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hranitel");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");
    private static readonly string BackupPath = Path.Combine(ConfigDir, "config.json.bak");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettings Load()
    {
        var fromFile = TryLoadFile(ConfigPath);
        if (fromFile != null)
            return Validate(fromFile);

        var fromBackup = TryLoadFile(BackupPath);
        if (fromBackup != null)
            return Validate(fromBackup);

        return new AppSettings();
    }

    private static AppSettings? TryLoadFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            return settings;
        }
        catch
        {
            return null;
        }
    }

    private static AppSettings Validate(AppSettings s)
    {
        s ??= new AppSettings();
        s.BlockedApps ??= new();

        if (s.BlockStart.TotalHours < 0 || s.BlockStart.TotalHours >= 24)
            s.BlockStart = new(0, 0, 0);
        if (s.BlockEnd.TotalHours < 0 || s.BlockEnd.TotalHours >= 24)
            s.BlockEnd = new(8, 0, 0);

        for (int i = s.BlockedApps.Count - 1; i >= 0; i--)
        {
            var a = s.BlockedApps[i];
            if (string.IsNullOrWhiteSpace(a?.ProcessName))
                s.BlockedApps.RemoveAt(i);
        }

        return s;
    }

    public void Save(AppSettings settings)
    {
        var tempPath = Path.Combine(ConfigDir, "config.json.tmp");
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(tempPath, json);
            if (File.Exists(ConfigPath))
                File.Copy(ConfigPath, BackupPath, overwrite: true);
            File.Move(tempPath, ConfigPath, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            try
            {
                if (File.Exists(BackupPath))
                    File.Copy(BackupPath, ConfigPath, overwrite: true);
            }
            catch { /* ignore */ }
        }
    }
}
