using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

var sizes = new[] { 256, 48, 32, 16 };
var images = new List<(int size, byte[] png)>();

foreach (var size in sizes)
{
    var bmp = new Bitmap(size, size);
    using (var g = Graphics.FromImage(bmp))
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(26, 27, 34)); // холодный тёмно-синий #1A1B22
        using var brush = new SolidBrush(Color.FromArgb(189, 176, 208)); // холодный сиреневый #BDB0D0
        var s = size / 32f;
        var pts = new PointF[]
        {
            new(16 * s, 4 * s), new(26 * s, 10 * s), new(26 * s, 18 * s),
            new(16 * s, 28 * s), new(6 * s, 18 * s), new(6 * s, 10 * s)
        };
        g.FillPolygon(brush, pts);
        using var pen = new Pen(Color.FromArgb(127, 181, 168), Math.Max(1, (int)(2 * s))); // мятный #7FB5A8
        g.DrawPolygon(pen, pts);
    }
    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    images.Add((size, ms.ToArray()));
    bmp.Dispose();
}

// Hranitel\Assets — при dotnet run --project GenIcon BaseDirectory = GenIcon\bin\Debug\net8.0
var outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Assets"));
Directory.CreateDirectory(outDir);
var icoPath = Path.Combine(outDir, "icon.ico");

using (var fs = File.Create(icoPath))
using (var bw = new BinaryWriter(fs))
{
    bw.Write((ushort)0);
    bw.Write((ushort)1);
    bw.Write((ushort)images.Count);
    uint offset = 6 + (uint)(16 * images.Count);
    foreach (var img in images)
    {
        bw.Write((byte)(img.size >= 256 ? 0 : img.size));
        bw.Write((byte)(img.size >= 256 ? 0 : img.size));
        bw.Write((byte)0);
        bw.Write((byte)0);
        bw.Write((ushort)1);
        bw.Write((ushort)32);
        bw.Write((uint)img.png.Length);
        bw.Write(offset);
        offset += (uint)img.png.Length;
    }
    foreach (var img in images)
        bw.Write(img.png);
}

Console.WriteLine("Created " + Path.GetFullPath(icoPath));
