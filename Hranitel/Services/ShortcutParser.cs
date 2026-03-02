using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using File = System.IO.File;

namespace Hranitel.Services;

public static class ShortcutParser
{
    public static string? ResolveShortcut(string shortcutPath)
    {
        if (!shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            return shortcutPath;

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return null;

            var shell = Activator.CreateInstance(shellType);
            if (shell == null) return null;

            var shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            if (shortcut == null) return null;

            var targetPath = shortcut.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;
            if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                return targetPath;

            return null;
        }
        catch
        {
            return null;
        }
    }
}
