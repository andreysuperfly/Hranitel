using System;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace Hranitel.Services;

/// <summary>
/// Определяет путь к steam.exe для блокировки игр через Steam.
/// </summary>
public static class SteamResolver
{
    public static string? GetSteamPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var path = key?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(path))
            {
                path = path.Replace('/', Path.DirectorySeparatorChar);
                var exe = Path.Combine(path, "steam.exe");
                if (File.Exists(exe))
                    return exe;
            }
        }
        catch
        {
            // игнорируем
        }

        var commonPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steam.exe")
        };

        foreach (var p in commonPaths)
        {
            if (File.Exists(p))
                return p;
        }

        return null;
    }

    /// <summary>
    /// Парсит .url файл и возвращает URL (например steam://rungameid/480).
    /// </summary>
    public static string? ParseUrlFile(string urlFilePath)
    {
        if (!File.Exists(urlFilePath) || !urlFilePath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var content = File.ReadAllText(urlFilePath, Encoding.Default);
            foreach (var line in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                    return trimmed.Substring(4).Trim();
            }
        }
        catch
        {
            // игнорируем
        }

        return null;
    }

    public static bool IsSteamUrl(string url)
    {
        return !string.IsNullOrWhiteSpace(url) &&
               url.StartsWith("steam://", StringComparison.OrdinalIgnoreCase);
    }
}
