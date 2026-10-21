using System;
using System.IO;
using System.Data.SQLite;
using System.Reflection;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Security.Cryptography;

namespace RestaurantPos
{
    public static class LocalData
    {
        public static string DefaultRoot { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PushPOS"); } }
        public static string Initialize(string root)
        {
            string database = Path.Combine(root, "data", "pos.db");
            Directory.CreateDirectory(Path.GetDirectoryName(database));
            Directory.CreateDirectory(Path.Combine(root,"settings"));
            Directory.CreateDirectory(Path.Combine(root,"backups"));
            Directory.CreateDirectory(Path.Combine(root,"data","item_images"));
            if (File.Exists(database)) { BackupService.Validate(database); return database; }
            string temporary = database + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = temporary, Pooling = false }.ConnectionString))
                {
                    connection.Open();
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RestaurantPos.schema.sql"))
                    using (var reader = new StreamReader(stream))
                    using (var command = new SQLiteCommand(reader.ReadToEnd(), connection)) command.ExecuteNonQuery();
                }
                File.Move(temporary,database);
                string logo=Path.Combine(root,"data","restaurant_logo.png");if(!File.Exists(logo))using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("RestaurantPos.default-logo.png"))if(stream!=null)using(var target=new FileStream(logo,FileMode.CreateNew,FileAccess.Write))stream.CopyTo(target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return database;
        }

        internal static void AtomicText(string file, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllText(temporary,text,new System.Text.UTF8Encoding(false)); if (File.Exists(file)) File.Replace(temporary,file,file+".bak"); else File.Move(temporary,file); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    public sealed class PosSettings
    {
        private readonly string file;
        private readonly string managerFile;
        public string PrinterName { get; set; } = "";
        public string PrintMode { get; set; } = "both";
        public string Theme { get; set; } = "dark";
        public int PaperWidth { get; set; } = 80;
        public bool AllowPriceEdit { get; set; } = true;
        public string PrinterTransport { get; set; } = "escpos";
        public bool CutPaper { get; set; } = true;
        public string BusinessName { get; set; } = "Push";
        public string ReceiptPhones { get; set; } = "";
        public string ReceiptAddress { get; set; } = "";
        public string ReceiptFooter { get; set; } = "شكرًا لزيارتكم";
        public PosSettings(string root)
        {
            file=Path.Combine(root,"settings","settings.json"); managerFile=Path.Combine(root,"settings","manager_password.json");
            if (File.Exists(file))
            {
                Dictionary<string,object> values=null;
                try{values=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(file));}
                catch(Exception error)when(error is ArgumentException||error is InvalidOperationException||error is IOException){AppLog.Error(root,error);if(File.Exists(file+".bak"))try{values=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(file+".bak"));}catch(Exception backupError){AppLog.Error(root,backupError);}}
                if(values==null)values=new Dictionary<string,object>();
                if(values.ContainsKey("printer_name")) PrinterName=Convert.ToString(values["printer_name"]);
                if(values.ContainsKey("print_mode")) PrintMode=Convert.ToString(values["print_mode"]);
                if(values.ContainsKey("theme")) Theme=Convert.ToString(values["theme"]);
                if(values.ContainsKey("business_name"))BusinessName=Convert.ToString(values["business_name"]);
                if(values.ContainsKey("receipt_phones"))ReceiptPhones=Convert.ToString(values["receipt_phones"]);
                if(values.ContainsKey("receipt_address"))ReceiptAddress=Convert.ToString(values["receipt_address"]);
                if(values.ContainsKey("receipt_footer"))ReceiptFooter=Convert.ToString(values["receipt_footer"]);
                int paper;if(values.ContainsKey("paper_width")&&int.TryParse(Convert.ToString(values["paper_width"]),out paper))PaperWidth=paper;
                bool allowed;if(values.ContainsKey("allow_price_edit")&&bool.TryParse(Convert.ToString(values["allow_price_edit"]),out allowed))AllowPriceEdit=allowed;
                if(values.ContainsKey("printer_transport"))PrinterTransport=Convert.ToString(values["printer_transport"]);
                bool cut;if(values.ContainsKey("cut_paper")&&bool.TryParse(Convert.ToString(values["cut_paper"]),out cut))CutPaper=cut;
            }
            if(PaperWidth!=58&&PaperWidth!=80)PaperWidth=80;if(Theme!="light"&&Theme!="dark")Theme="dark";if(PrintMode!="both"&&PrintMode!="customer")PrintMode="both";if(PrinterTransport!="escpos"&&PrinterTransport!="windows")PrinterTransport="escpos";
        }
        public void Save() { LocalData.AtomicText(file,new JavaScriptSerializer().Serialize(new {business_name=BusinessName,receipt_phones=ReceiptPhones,receipt_address=ReceiptAddress,receipt_footer=ReceiptFooter,printer_name=PrinterName,print_mode=PrintMode,theme=Theme,paper_width=PaperWidth,allow_price_edit=AllowPriceEdit,printer_transport=PrinterTransport,cut_paper=CutPaper})); }
        public bool HasManager { get { return File.Exists(managerFile); } }
        public bool VerifyManager(string password)
        {
            if(!HasManager)return false;
            try{var p=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(managerFile));return p!=null&&PinSecurity.Verify(password,Convert.ToString(p["salt"]),Convert.ToString(p["digest"]),Convert.ToInt32(p["iterations"]));}
            catch(Exception error)when(error is ArgumentException||error is KeyNotFoundException||error is FormatException||error is InvalidOperationException||error is IOException){AppLog.Error(Path.GetDirectoryName(Path.GetDirectoryName(managerFile)),error);return false;}
        }
        public void SetManager(string password)
        {
            if(string.IsNullOrWhiteSpace(password)||password.Length<4)throw new ArgumentException("كلمة السر يجب ألا تقل عن 4 أحرف أو أرقام");
            var salt=new byte[16]; using(var random=RandomNumberGenerator.Create())random.GetBytes(salt);
            using(var kdf=new Rfc2898DeriveBytes(password,salt,240000,HashAlgorithmName.SHA256))
                LocalData.AtomicText(managerFile,new JavaScriptSerializer().Serialize(new {algorithm="pbkdf2_sha256",iterations=240000,salt=Convert.ToBase64String(salt),digest=Convert.ToBase64String(kdf.GetBytes(32))}));
        }
    }
}
