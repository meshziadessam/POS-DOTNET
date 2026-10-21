using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Data.SQLite;
using System.Linq;

namespace RestaurantPos
{
    public static class AppLog
    {
        private static readonly object Sync=new object();
        public static void Write(string root,string message)
        {
            try{lock(Sync){string folder=Path.Combine(root,"logs");Directory.CreateDirectory(folder);string file=Path.Combine(folder,"application.log");if(File.Exists(file)&&new FileInfo(file).Length>5*1024*1024){if(File.Exists(file+".1"))File.Delete(file+".1");File.Move(file,file+".1");}File.AppendAllText(file,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" | "+message+Environment.NewLine);}}catch(IOException){}catch(UnauthorizedAccessException){}
        }
        public static void Error(string root,Exception error){Write(root,error.GetType().Name+": "+error.Message+Environment.NewLine+error.StackTrace);}
    }
    public static class BackupService
    {
        public static void Daily(string root)
        {
            string file=Path.Combine(root,"backups","daily-"+DateTime.Today.ToString("yyyyMMdd")+".db");if(!File.Exists(file))Store.Snapshot(Path.Combine(root,"data","pos.db"),file);
        }
        public static void Validate(string database)
        {
            if(new Store(database).Integrity()!="ok")throw new InvalidDataException("النسخة الاحتياطية غير سليمة");
            using(var c=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=database,ReadOnly=true,FailIfMissing=true,Pooling=false}.ConnectionString))
            {c.Open();using(var command=new SQLiteCommand("SELECT version FROM pos_schema",c)){if(Convert.ToInt32(command.ExecuteScalar())!=1)throw new InvalidDataException("إصدار قاعدة البيانات غير متوافق");}foreach(string table in new[]{"orders","shifts","session","catalog_items","customers","drivers","companies","users","save_tokens","categories","item_variants","restaurant_tables","audit","expenses","shift_batch_archives","item_options"})using(var command=new SQLiteCommand("SELECT * FROM "+table+" LIMIT 0",c))using(var reader=command.ExecuteReader()){}using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM shifts WHERE status='open'",c))if(Convert.ToInt64(cmd.ExecuteScalar())!=1)throw new InvalidDataException("النسخة يجب أن تحتوي وردية مفتوحة واحدة");}
        }
        public static void Create(string root,string archive)
        {
            if(File.Exists(archive))throw new IOException("ملف النسخة الاحتياطية موجود بالفعل؛ اختر اسمًا جديدًا");string parent=Path.GetDirectoryName(Path.GetFullPath(archive));Directory.CreateDirectory(parent);string staged=Path.Combine(parent,"pos-backup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staged);string temporary=archive+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                Store.Snapshot(Path.Combine(root,"data","pos.db"),Path.Combine(staged,"pos.db"));
                using(var zip=ZipFile.Open(temporary,ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(Path.Combine(staged,"pos.db"),"data/pos.db",CompressionLevel.Optimal);
                    zip.CreateEntry("settings/");zip.CreateEntry("data/item_images/");
                    foreach(string folder in new[]{"settings","data/item_images"})
                    {
                        string source=Path.Combine(root,folder.Replace('/',Path.DirectorySeparatorChar));if(!Directory.Exists(source))continue;
                        foreach(string file in SafeFiles(source))zip.CreateEntryFromFile(file,(folder+"/"+file.Substring(source.Length+1)).Replace('\\','/'),CompressionLevel.Optimal);
                    }
                    string logo=Path.Combine(root,"data","restaurant_logo.png");if(File.Exists(logo))zip.CreateEntryFromFile(logo,"data/restaurant_logo.png",CompressionLevel.Optimal);
                    var manifest=zip.CreateEntry("backup-info.txt");using(var writer=new StreamWriter(manifest.Open()))writer.Write("Push backup format 1\n"+DateTime.Now.ToString("O"));
                }
                File.Move(temporary,archive);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);if(Directory.Exists(staged))Directory.Delete(staged,true);}
        }
        private static IEnumerable<string> SafeFiles(string folder)
        {
            if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)throw new IOException("المجلدات المرتبطة غير مدعومة في النسخ الاحتياطي");foreach(string file in Directory.GetFiles(folder)){if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("الملفات المرتبطة غير مدعومة");yield return file;}foreach(string directory in Directory.GetDirectories(folder))foreach(string file in SafeFiles(directory))yield return file;
        }
        public static string Restore(string root,string archive)
        {
            string staging=Path.Combine(root,"restore-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);string safety=Path.Combine(root,"backups","before-restore-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".zip");
            var moved=new List<Tuple<string,string>>();var installed=new List<string>();
            try
            {
                if(Path.GetExtension(archive).Equals(".db",StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(Path.Combine(staging,"data"));Store.Snapshot(archive,Path.Combine(staging,"data","pos.db"));string images=Path.Combine(root,"data","item_images");if(Directory.Exists(images))foreach(string file in SafeFiles(images)){string target=Path.Combine(staging,"data","item_images",file.Substring(images.Length+1));Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(file,target);}string logo=Path.Combine(root,"data","restaurant_logo.png");if(File.Exists(logo))File.Copy(logo,Path.Combine(staging,"data","restaurant_logo.png"));
                }
                else
                {
                    using(var zip=ZipFile.OpenRead(archive))
                    {
                        long total=0;foreach(var entry in zip.Entries)
                        {
                            total+=entry.Length;if(total>2L*1024*1024*1024||zip.Entries.Count>50000)throw new InvalidDataException("حجم النسخة الاحتياطية غير مدعوم");if(entry.FullName.Contains(":"))throw new InvalidDataException("مسار غير آمن داخل النسخة");string name=entry.FullName.Replace('/',Path.DirectorySeparatorChar);string target=Path.GetFullPath(Path.Combine(staging,name));
                            if(!target.StartsWith(Path.GetFullPath(staging)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("مسار غير آمن داخل النسخة الاحتياطية");
                            if(entry.FullName.EndsWith("/")) {Directory.CreateDirectory(target);continue;}
                            if(!(entry.FullName.StartsWith("data/",StringComparison.Ordinal)||entry.FullName.StartsWith("settings/",StringComparison.Ordinal)||entry.FullName=="backup-info.txt"))throw new InvalidDataException("ملف غير معروف في النسخة الاحتياطية");
                            Directory.CreateDirectory(Path.GetDirectoryName(target));using(var source=entry.Open())using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write))source.CopyTo(output);
                        }
                    }
                }
                Validate(Path.Combine(staging,"data","pos.db"));
                Create(root,safety);
                // Checkpoint any existing WAL before moving the complete data directory.
                using(var c=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=Path.Combine(root,"data","pos.db"),FailIfMissing=true,Pooling=false}.ConnectionString))
                {c.Open();using(var cmd=new SQLiteCommand("PRAGMA wal_checkpoint(TRUNCATE)",c))using(var reader=cmd.ExecuteReader())if(reader.Read()&&Convert.ToInt32(reader[0])!=0)throw new IOException("قاعدة البيانات مستخدمة؛ أغلق أي اتصال آخر قبل الاسترجاع");}
                foreach(string folder in Path.GetExtension(archive).Equals(".db",StringComparison.OrdinalIgnoreCase)?new[]{"data"}:new[]{"data","settings"})
                {
                    string staged=Path.Combine(staging,folder);if(!Directory.Exists(staged))continue;string destination=Path.Combine(root,folder);string old=Path.Combine(root,folder+"-before-"+Guid.NewGuid().ToString("N"));if(Directory.Exists(destination)){Directory.Move(destination,old);moved.Add(Tuple.Create(old,destination));}
                    Directory.Move(staged,destination);installed.Add(destination);
                }
                AppLog.Write(root,"Database restored. Recovery snapshot: "+safety);return safety;
            }
            catch
            {
                foreach(string directory in installed.AsEnumerable().Reverse())if(Directory.Exists(directory))Directory.Move(directory,directory+"-failed-"+Guid.NewGuid().ToString("N"));foreach(var directory in moved.AsEnumerable().Reverse())if(Directory.Exists(directory.Item1)&&!Directory.Exists(directory.Item2))Directory.Move(directory.Item1,directory.Item2);throw;
            }
            finally{if(Directory.Exists(staging))Directory.Delete(staging,true);}
        }
    }
}
