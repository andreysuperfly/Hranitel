using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace Hranitel.Services;

/// <summary>
/// Извлекает путь к файлу из данных drag-and-drop.
/// Поддерживает FileDrop, Shell IDList Array (Steam), FileName/FileNameW, UnicodeText.
/// </summary>
public static class DragDropHelper
{
    private const string ShellIdListArray = "Shell IDList Array";
    private const string FileNameW = "FileNameW";
    private const string UniformResourceLocatorW = "UniformResourceLocatorW";
    private const string UniformResourceLocator = "UniformResourceLocator";

    public static string? TryGetPath(IDataObject data)
    {
        // 0. UniformResourceLocator — .url ярлыки Steam при перетаскивании могут отдавать steam://
        foreach (var format in new[] { UniformResourceLocatorW, UniformResourceLocator })
        {
            if (data.GetDataPresent(format))
            {
                var url = data.GetData(format) as string;
                if (!string.IsNullOrWhiteSpace(url))
                {
                    url = url.Trim();
                    if (url.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
                        return url;
                }
            }
        }

        // 1. FileDrop — стандартный формат при перетаскивании из Проводника
        if (data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = data.GetData(DataFormats.FileDrop) as string[];
            if (files is { Length: > 0 } && !string.IsNullOrWhiteSpace(files[0]))
                return files[0];
        }

        // 2. Shell IDList Array — используется Steam и другими приложениями
        var pathFromShell = TryGetPathFromShellIdList(data);
        if (pathFromShell != null)
            return pathFromShell;

        // 3. FileNameW / FileName — альтернативные форматы имён файлов
        if (data.GetDataPresent(FileNameW))
        {
            var s = data.GetData(FileNameW) as string;
            if (!string.IsNullOrWhiteSpace(s))
                return s;
        }

        // 4. UnicodeText — текст (путь, steam:// или file://)
        if (data.GetDataPresent(DataFormats.UnicodeText))
        {
            var text = data.GetData(DataFormats.UnicodeText) as string;
            if (!string.IsNullOrWhiteSpace(text))
            {
                text = text.Trim();
                if (text.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
                    return text;
                if (text.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                    text = new Uri(text).LocalPath;
                if (File.Exists(text) || Directory.Exists(text))
                    return text;
            }
        }

        return null;
    }

    /// <summary>true, если есть хотя бы один поддерживаемый формат.</summary>
    public static bool HasSupportedData(IDataObject data)
    {
        if (data.GetDataPresent(UniformResourceLocatorW) || data.GetDataPresent(UniformResourceLocator))
        {
            var url = data.GetData(UniformResourceLocatorW) ?? data.GetData(UniformResourceLocator);
            if (url is string s && s.Trim().StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return data.GetDataPresent(DataFormats.FileDrop)
            || data.GetDataPresent(ShellIdListArray)
            || data.GetDataPresent(FileNameW)
            || data.GetDataPresent(DataFormats.UnicodeText);
    }

    private static string? TryGetPathFromShellIdList(IDataObject data)
    {
        if (!data.GetDataPresent(ShellIdListArray))
            return null;

        try
        {
            var stream = data.GetData(ShellIdListArray) as MemoryStream;
            if (stream == null)
                return null;

            var bytes = stream.ToArray();
            if (bytes.Length < 4 + 4 + 4) // cidl + aoffset[0] + aoffset[1]
                return null;

            var cidl = BitConverter.ToUInt32(bytes, 0);
            if (cidl == 0)
                return null;

            var aoffset0 = BitConverter.ToUInt32(bytes, 4);
            var aoffset1 = BitConverter.ToUInt32(bytes, 8);

            if (aoffset0 >= (uint)bytes.Length || aoffset1 >= (uint)bytes.Length)
                return null;

            GCHandle handle = default;
            try
            {
                handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                var basePtr = handle.AddrOfPinnedObject();

                var parentPidl = IntPtr.Add(basePtr, (int)aoffset0);
                var childPidl = IntPtr.Add(basePtr, (int)aoffset1);

                var combined = ILCombine(parentPidl, childPidl);
                if (combined == IntPtr.Zero)
                    return null;

                try
                {
                    var sb = new StringBuilder(260);
                    if (SHGetPathFromIDListW(combined, sb))
                        return sb.ToString();
                }
                finally
                {
                    ILFree(combined);
                }
            }
            finally
            {
                if (handle.IsAllocated)
                    handle.Free();
            }
        }
        catch
        {
            // игнорируем ошибки
        }

        return null;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SHGetPathFromIDListW(IntPtr pidl, [Out] StringBuilder pszPath);

    [DllImport("shell32.dll")]
    private static extern IntPtr ILCombine(IntPtr pidl1, IntPtr pidl2);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr pidl);
}
