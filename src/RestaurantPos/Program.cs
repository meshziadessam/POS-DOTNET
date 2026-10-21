using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace RestaurantPos
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                string dataRoot = LocalData.DefaultRoot;
                for(int i=0;i<args.Length;i++)if(args[i]=="--data-root"&&i+1<args.Length)dataRoot=Path.GetFullPath(args[++i]);
                if(args.Contains("--initialize")){for(int a=0;a<args.Length;a++){if(args[a]=="--data-root"&&a+1<args.Length){a++;continue;}if(args[a]!="--initialize")throw new ArgumentException("تهيئة البيانات لا تقبل أوامر تشغيل أخرى");}LocalData.Initialize(dataRoot);Branding.ImportSetup(dataRoot,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"branding.ini"));return;}
                Branding.ImportSetup(dataRoot,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"branding.ini"));
                if(!Licensing.Ensure(dataRoot))return;
                for(int i=0;i<args.Length;i++)if(args[i]=="--printer-test"&&i+1<args.Length){var settings=new PosSettings(dataRoot){PrinterName=args[i+1]};new ReceiptPrinter(settings,dataRoot).PrintTest();return;}
                string database = null;
                bool explicitDatabase = false;
                for (int i = 0; i < args.Length; i++)
                    if (args[i] == "--database" && i + 1 < args.Length) { database = Path.GetFullPath(args[++i]); explicitDatabase = true; }
                if(explicitDatabase&&!File.Exists(database))throw new FileNotFoundException("ملف قاعدة البيانات المحدد غير موجود: "+database);
                if(!explicitDatabase)database=LocalData.Initialize(dataRoot);
                if(args.Contains("--initialize"))return;
                string mutexName;
                using(var sha=System.Security.Cryptography.SHA256.Create())mutexName="Local\\RestaurantPos-"+BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(database).ToUpperInvariant()))).Replace("-","");
                for(int i=0;i<args.Length;i++)if(args[i]=="--restart-parent"&&i+1<args.Length){int pid=int.Parse(args[++i]);try{using(var parent=System.Diagnostics.Process.GetProcessById(pid))if(!parent.WaitForExit(20000))throw new IOException("تعذر إغلاق النسخة السابقة لإعادة التشغيل");}catch(ArgumentException){}}
                bool owns;
                using(var instance=new System.Threading.Mutex(true,mutexName,out owns))
                {
                    if(!owns){Dialogs.Show("البرنامج مفتوح بالفعل لهذه البيانات. استخدم النافذة المفتوحة.","Push");return;}
                    try
                    {
                        if(!explicitDatabase)try{BackupService.Daily(dataRoot);}catch(Exception backupError){AppLog.Error(dataRoot,backupError);}
                        Application.ThreadException+=(sender,eventArgs)=>{AppLog.Error(dataRoot,eventArgs.Exception);Dialogs.Show(eventArgs.Exception.Message,"Push",MessageBoxButtons.OK,MessageBoxIcon.Error);};
                        AppDomain.CurrentDomain.UnhandledException+=(sender,eventArgs)=>{if(eventArgs.ExceptionObject is Exception ex)AppLog.Error(dataRoot,ex);};
                        AppLog.Write(dataRoot,"Application started");
                var store = new Store(database,!explicitDatabase);
                if(!explicitDatabase)store.MigrateLegacyManager();
                if(!store.AnyUsers())using(var initial=new InitialManagerForm(store)){if(initial.ShowDialog()!=DialogResult.OK)return;}
                if(args.Contains("--configure-accounts"))return;
                using (var login = new LoginForm(store))
                    if (login.ShowDialog() == DialogResult.OK) Application.Run(new PosForm(store, login.Session));
                    }
                    finally{instance.ReleaseMutex();}
                }
            }
            catch (Exception error) { AppLog.Error(LocalData.DefaultRoot,error);Environment.ExitCode=1;if(args.Contains("--initialize")||args.Contains("--printer-test")){Console.Error.WriteLine(error);return;}Dialogs.Show(error.Message, "Push", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

}
