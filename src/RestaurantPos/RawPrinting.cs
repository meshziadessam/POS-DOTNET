using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace RestaurantPos
{
    public static class EscPosRaster
    {
        public static byte[] Encode(Bitmap source,bool cut)
        {
            int widthBytes=(source.Width+7)/8;
            using(var bitmap=new Bitmap(source.Width,source.Height,PixelFormat.Format24bppRgb))
            using(var output=new MemoryStream())
            {
                using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.White);g.DrawImage(source,new Rectangle(0,0,source.Width,source.Height),0,0,source.Width,source.Height,GraphicsUnit.Pixel);}
                var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format24bppRgb);
                try
                {
                    var pixels=new byte[Math.Abs(data.Stride)*bitmap.Height];Marshal.Copy(data.Scan0,pixels,0,pixels.Length);
                    output.Write(new byte[]{0x1b,0x40,0x1b,0x61,0x01},0,5);
                    for(int start=0;start<bitmap.Height;start+=128)
                    {
                        int rows=Math.Min(128,bitmap.Height-start);byte[] header={0x1d,0x76,0x30,0,(byte)(widthBytes&255),(byte)(widthBytes>>8),(byte)(rows&255),(byte)(rows>>8)};output.Write(header,0,header.Length);var raster=new byte[widthBytes*rows];
                        for(int y=0;y<rows;y++)for(int x=0;x<bitmap.Width;x++){int offset=(start+y)*Math.Abs(data.Stride)+x*3;int gray=(pixels[offset]*29+pixels[offset+1]*150+pixels[offset+2]*77)>>8;if(gray<160)raster[y*widthBytes+x/8]|=(byte)(0x80>>(x%8));}output.Write(raster,0,raster.Length);
                    }
                    output.Write(new byte[]{0x1b,0x64,0x03},0,3);if(cut)output.Write(new byte[]{0x1d,0x56,0x00},0,3);return output.ToArray();
                }
                finally{bitmap.UnlockBits(data);}
            }
        }
    }
    internal static class WindowsRawPrinter
    {
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private sealed class DocInfo { [MarshalAs(UnmanagedType.LPWStr)]public string DocumentName;[MarshalAs(UnmanagedType.LPWStr)]public string OutputFile;[MarshalAs(UnmanagedType.LPWStr)]public string DataType="RAW"; }
        [DllImport("winspool.drv",EntryPoint="OpenPrinterW",SetLastError=true,CharSet=CharSet.Unicode)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool OpenPrinter(string name,out IntPtr handle,IntPtr defaults);
        [DllImport("winspool.drv",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool ClosePrinter(IntPtr handle);
        [DllImport("winspool.drv",EntryPoint="StartDocPrinterW",SetLastError=true,CharSet=CharSet.Unicode)]private static extern int StartDocPrinter(IntPtr handle,int level,[In]DocInfo info);
        [DllImport("winspool.drv",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool EndDocPrinter(IntPtr handle);
        [DllImport("winspool.drv",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool AbortPrinter(IntPtr handle);
        [DllImport("winspool.drv",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool StartPagePrinter(IntPtr handle);
        [DllImport("winspool.drv",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool EndPagePrinter(IntPtr handle);
        [DllImport("winspool.drv",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool WritePrinter(IntPtr handle,byte[] bytes,int count,out int written);
        public static int Send(string printer,string title,byte[] bytes)
        {
            if(string.IsNullOrWhiteSpace(printer))throw new InvalidOperationException("اختر اسم الطابعة من الإعدادات قبل الطباعة");
            IntPtr handle;if(!OpenPrinter(printer,out handle,IntPtr.Zero))throw Error();bool completed=false;int job=0;
            try
            {
                job=StartDocPrinter(handle,1,new DocInfo{DocumentName="Restaurant POS — "+title});if(job==0)throw Error();if(!StartPagePrinter(handle))throw Error();int written;if(!WritePrinter(handle,bytes,bytes.Length,out written)||written!=bytes.Length)throw Error();if(!EndPagePrinter(handle)||!EndDocPrinter(handle))throw Error();completed=true;return job;
            }
            finally{if(!completed&&job>0)AbortPrinter(handle);ClosePrinter(handle);}
        }
        private static InvalidOperationException Error(){return new InvalidOperationException("تعذر إرسال الإيصال إلى طابعة Windows: "+new Win32Exception(Marshal.GetLastWin32Error()).Message);}
    }
}
