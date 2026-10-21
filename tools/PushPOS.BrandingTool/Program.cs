using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

internal static class Program
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr BeginUpdateResource(string path, bool delete);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool UpdateResource(IntPtr handle, IntPtr type, IntPtr name, ushort language, byte[] data, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool EndUpdateResource(IntPtr handle, bool discard);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern void SHChangeNotify(uint e, uint flags, string path, IntPtr other);
    static int Main(string[] args)
    {
        string staged = null;
        try
        {
            if (args.Length != 2) throw new ArgumentException("Expected image and installed executable paths.");
            string target = Path.GetFullPath(args[1]);
            if (!string.Equals(Path.GetDirectoryName(target), AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                || string.Equals(target, System.Reflection.Assembly.GetExecutingAssembly().Location, StringComparison.OrdinalIgnoreCase)
                || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Target must be the application in the helper directory.");
            if (new FileInfo(args[0]).Length > 10 * 1024 * 1024) throw new ArgumentException("Image exceeds 10 MiB.");
            var images = new List<byte[]>();
            int[] sizes = {16, 32, 48, 64, 128, 256};
            using (var source = Image.FromFile(args[0]))
            {
                if (source.Width > 8192 || source.Height > 8192) throw new ArgumentException("Image dimensions exceed 8192.");
                foreach (int size in sizes)
                    using (var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                    using (var graphics = Graphics.FromImage(bitmap))
                    using (var stream = new MemoryStream())
                    {
                        graphics.Clear(Color.Transparent);
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        double scale = Math.Min((double)size / source.Width, (double)size / source.Height);
                        int width = Math.Max(1, (int)(source.Width * scale)), height = Math.Max(1, (int)(source.Height * scale));
                        graphics.DrawImage(source, (size - width) / 2, (size - height) / 2, width, height);
                        bitmap.Save(stream, ImageFormat.Png); images.Add(stream.ToArray());
                    }
            }
            staged = target + ".branding-" + Guid.NewGuid().ToString("N") + ".tmp";
            File.Copy(target, staged);
            IntPtr handle = BeginUpdateResource(staged, false);
            if (handle == IntPtr.Zero) throw new Win32Exception();
            try
            {
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
                    for (int i = 0; i < sizes.Length; i++)
                    {
                        writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                        writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
                        writer.Write((uint)images[i].Length); writer.Write((ushort)(60000 + i));
                        if (!UpdateResource(handle, (IntPtr)3, (IntPtr)(60000 + i), 0, images[i], (uint)images[i].Length)) throw new Win32Exception();
                    }
                    byte[] group = stream.ToArray();
                    // Group ID 1 is the first icon Windows extracts; existing non-icon resources are preserved.
                    if (!UpdateResource(handle, (IntPtr)14, (IntPtr)1, 0, group, (uint)group.Length)) throw new Win32Exception();
                }
                if (!EndUpdateResource(handle, false)) throw new Win32Exception();
                handle = IntPtr.Zero;
            }
            finally { if (handle != IntPtr.Zero) EndUpdateResource(handle, true); }
            File.Replace(staged, target, null); staged = null;
            SHChangeNotify(0x2000, 0x0005, target, IntPtr.Zero);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
        finally { if (staged != null && File.Exists(staged)) File.Delete(staged); }
    }
}
