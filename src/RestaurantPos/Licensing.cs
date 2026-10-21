using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RestaurantPos
{
    public sealed class LicenseGrant
    {
        public int Version { get; set; }
        public string Product { get; set; }
        public string LicenseId { get; set; }
        public string Customer { get; set; }
        public string Device { get; set; }
        public string IssuedUtc { get; set; }
    }
    public sealed class SignedLicense
    {
        public string Payload { get; set; }
        public string Signature { get; set; }
    }
    public static class Licensing
    {
        public const string Product="PushPOS.Desktop";
        private const int MaxBytes=32768;
        private static string device;
        private static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=MaxBytes,RecursionLimit=8};}
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
        private static extern bool GetVolumeInformation(string root,StringBuilder name,int nameSize,out uint serial,out uint maxComponent,out uint flags,StringBuilder filesystem,int filesystemSize);
        public static string DeviceCode
        {
            get
            {
                if(device!=null)return device;
                string guid;
                using(var key=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,Environment.Is64BitOperatingSystem?RegistryView.Registry64:RegistryView.Registry32))
                using(var crypto=key.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))guid=Convert.ToString(crypto?.GetValue("MachineGuid"));
                uint serial,max,flags;
                if(string.IsNullOrWhiteSpace(guid)||!GetVolumeInformation(Path.GetPathRoot(Environment.SystemDirectory),null,0,out serial,out max,out flags,null,0))throw new InvalidOperationException("تعذر قراءة هوية الجهاز. أعد المحاولة من حساب Windows عادي بصلاحيات قراءة النظام.");
                using(var hash=SHA256.Create())device="PP1-"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Product+"|"+guid.Trim().ToUpperInvariant()+"|"+serial.ToString("X8")))).Replace("-","");
                return device;
            }
        }
        public static bool ValidDevice(string value){return value!=null&&Regex.IsMatch(value,@"\APP1-[0-9A-F]{64}\z");}
        public static string PublicKey
        {
            get{using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PushPOS.PublicKey.xml"))using(var reader=new StreamReader(stream))return reader.ReadToEnd();}
        }
        public static bool Verify(string document,string expectedDevice,out LicenseGrant grant)
        {
            grant=null;
            if(document==null||document.Length>MaxBytes||!ValidDevice(expectedDevice))return false;
            try
            {
                var envelope=Serializer().Deserialize<SignedLicense>(document);
                if(envelope==null||string.IsNullOrEmpty(envelope.Payload)||string.IsNullOrEmpty(envelope.Signature))return false;
                byte[] payload=Convert.FromBase64String(envelope.Payload),signature=Convert.FromBase64String(envelope.Signature);
                if(payload.Length>8192||signature.Length!=512)return false;
                using(var rsa=new RSACryptoServiceProvider(new CspParameters(24))){rsa.PersistKeyInCsp=false;rsa.FromXmlString(PublicKey);if(!rsa.VerifyData(payload,CryptoConfig.MapNameToOID("SHA256"),signature))return false;}
                var value=Serializer().Deserialize<LicenseGrant>(new UTF8Encoding(false,true).GetString(payload));
                Guid id;DateTime issued;
                if(value==null||value.Version!=1||value.Product!=Product||value.Device!=expectedDevice||!ValidDevice(value.Device)||!Guid.TryParse(value.LicenseId,out id)||string.IsNullOrWhiteSpace(value.Customer)||value.Customer.Length>120||!DateTime.TryParse(value.IssuedUtc,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out issued))return false;
                grant=value;return true;
            }
            catch(Exception error)when(error is ArgumentException||error is FormatException||error is InvalidOperationException||error is CryptographicException||error is DecoderFallbackException){return false;}
        }
        public static string LicensePath(string root){return Path.Combine(root,"activation","pushpos.lic");}
        public static bool Read(string root,out LicenseGrant grant)
        {
            grant=null;string path=LicensePath(root);
            try{return File.Exists(path)&&new FileInfo(path).Length<=MaxBytes&&Verify(File.ReadAllText(path),DeviceCode,out grant);}
            catch(IOException){return false;}
            catch(UnauthorizedAccessException){return false;}
        }
        public static void Import(string root,string path)
        {
            if(new FileInfo(path).Length>MaxBytes)throw new ArgumentException("حجم ملف التفعيل غير صحيح");
            string text=File.ReadAllText(path);LicenseGrant grant;
            if(!Verify(text,DeviceCode,out grant))throw new ArgumentException("ملف التفعيل غير صالح أو صادر لجهاز آخر. اطلب ملفًا مطابقًا لكود هذا الجهاز.");
            LocalData.AtomicText(LicensePath(root),text);
        }
        public static bool Ensure(string root)
        {
            LicenseGrant grant;if(Read(root,out grant))return true;
            using(var form=new ActivationForm(root))return form.ShowDialog()==DialogResult.OK&&Read(root,out grant);
        }
    }
    public sealed class ActivationForm : Form
    {
        public ActivationForm(string root)
        {
            Text="تفعيل Push";ClientSize=new Size(690,390);MinimumSize=new Size(630,420);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",11);RightToLeft=RightToLeft.Yes;AutoScaleMode=AutoScaleMode.Dpi;
            var panel=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=5};
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(new Label{Text="Push",Font=new Font("Segoe UI",24,FontStyle.Bold),AutoSize=true},0,0);
            panel.Controls.Add(new Label{Text="ترخيص دائم لجهاز واحد\nأرسل كود الجهاز للبائع، ثم استورد ملف التفعيل الذي يصدره لك.",AutoSize=true,Margin=new Padding(0,12,0,12)},0,1);
            panel.Controls.Add(new TextBox{Text=Licensing.DeviceCode,ReadOnly=true,Dock=DockStyle.Fill,RightToLeft=RightToLeft.No},0,2);
            panel.Controls.Add(new Label{Text="نسخ البرنامج أو ملف التفعيل إلى جهاز مختلف لا ينقل الترخيص. تغيير Windows أو القرص قد يتطلب إعادة إصدار التفعيل.",AutoSize=true,MaximumSize=new Size(620,0),Margin=new Padding(0,16,0,10)},0,3);
            panel.Controls.Add(Ui.Row(Ui.Button("استيراد التفعيل",()=>{using(var picker=new OpenFileDialog{Filter="Push license|*.lic",CheckFileExists=true})if(picker.ShowDialog(this)==DialogResult.OK){Licensing.Import(root,picker.FileName);DialogResult=DialogResult.OK;}},"success"),Ui.Button("نسخ كود الجهاز",()=>Clipboard.SetText(Licensing.DeviceCode)),Ui.Button("خروج",()=>DialogResult=DialogResult.Cancel,"default")),0,4);
            Controls.Add(panel);Ui.Theme(this,false);
        }
    }
}
