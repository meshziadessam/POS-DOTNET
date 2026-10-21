using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace RestaurantPos
{
    public static class Branding
    {
        public static string LogoPath(string root){return Path.Combine(root,"data","restaurant_logo.png");}
        public static void SaveLogo(string root,string source)
        {
            if(new FileInfo(source).Length>10*1024*1024)throw new ArgumentException("الصورة يجب ألا تتجاوز 10 ميجابايت");
            string target=LogoPath(root);Directory.CreateDirectory(Path.GetDirectoryName(target));string temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var image=Image.FromFile(source))
                {
                    if(image.Width>8192||image.Height>8192)throw new ArgumentException("أبعاد الصورة كبيرة جدًا؛ الحد الأقصى 8192 بكسل");
                    double scale=Math.Min(1,1024d/Math.Max(image.Width,image.Height));
                    using(var bitmap=new Bitmap(Math.Max(1,(int)(image.Width*scale)),Math.Max(1,(int)(image.Height*scale))))
                    {using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.Transparent);graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;graphics.DrawImage(image,0,0,bitmap.Width,bitmap.Height);}bitmap.Save(temp,ImageFormat.Png);}
                }
                if(File.Exists(target))File.Replace(temp,target,target+".bak");else File.Move(temp,target);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        public static void ImportSetup(string root,string config)
        {
            // Installation defaults are applied once; upgrades preserve the manager's later edits.
            if(File.Exists(Path.Combine(root,"settings","settings.json"))||!File.Exists(config))return;
            var fields=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(string line in File.ReadAllLines(config)){int split=line.IndexOf('=');if(split>0)fields[line.Substring(0,split).Trim()]=line.Substring(split+1).Trim();}
            var settings=new PosSettings(root);
            if(fields.ContainsKey("Name")&&!string.IsNullOrWhiteSpace(fields["Name"]))settings.BusinessName=fields["Name"];
            if(fields.ContainsKey("Phone"))settings.ReceiptPhones=fields["Phone"];
            if(fields.ContainsKey("Address"))settings.ReceiptAddress=fields["Address"];
            if(fields.ContainsKey("Footer"))settings.ReceiptFooter=fields["Footer"];
            foreach(var value in new[]{settings.BusinessName,settings.ReceiptPhones,settings.ReceiptAddress,settings.ReceiptFooter})if(value.Length>180)throw new ArgumentException("بيانات المطعم أطول من الحد المسموح");
            string image=Path.Combine(Path.GetDirectoryName(config),"setup-logo.image");
            if(File.Exists(image))try{SaveLogo(root,image);}catch(Exception error)when(error is ArgumentException||error is IOException||error is OutOfMemoryException){AppLog.Error(root,error);}
            settings.Save();
        }
    }
}
