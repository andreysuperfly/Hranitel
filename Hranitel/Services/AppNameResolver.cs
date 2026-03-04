using System.Diagnostics;
using System.IO;

namespace Hranitel.Services;

public static class AppNameResolver
{
    public static string GetDisplayName(string exePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            var product = info.ProductName?.Trim();
            var fileDesc = info.FileDescription?.Trim();

            if (!string.IsNullOrEmpty(product))
                return product;
            if (!string.IsNullOrEmpty(fileDesc))
                return fileDesc;
        }
        catch { /* ignore */ }

        return Path.GetFileNameWithoutExtension(exePath) ?? "Приложение";
    }
}
