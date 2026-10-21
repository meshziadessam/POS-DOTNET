using System;
using System.IO;
using System.Collections.Generic;

namespace RestaurantPos
{
    public static class PreviewImport
    {
        public static string ResolveDatabase(string selection)
        {
            if (string.IsNullOrWhiteSpace(selection)) throw new IOException("اختر مجلد المشروع أو مجلد data أو ملف pos.db.");
            string path = Path.GetFullPath(selection.Trim().Trim('"'));
            if (File.Exists(path)) return path;
            if (!Directory.Exists(path)) throw new IOException("المسار المختار غير موجود: " + path);
            string database = Path.Combine(path, "data", "pos.db");
            if (File.Exists(database)) return database;
            database = Path.Combine(path, "pos.db");
            if (File.Exists(database)) return database;
            var candidates = new List<string>();
            foreach (string child in Directory.GetDirectories(path))
            {
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                database = Path.Combine(child, "data", "pos.db");
                if (File.Exists(database)) candidates.Add(database);
            }
            if (candidates.Count == 1) return candidates[0];
            if (candidates.Count > 1) throw new IOException("المجلد يحتوي أكثر من قاعدة بيانات. اختر ملف pos.db المطلوب مباشرةً.");
            throw new FileNotFoundException("لم أجد قاعدة البيانات في المجلد المختار. اختر مجلد المشروع الذي يحتوي data، أو مجلد data، أو استخدم زر اختيار ملف pos.db.");
        }

        public static void Create(string sourceDirectory, string destinationDirectory)
        {
            string sourceDatabase = ResolveDatabase(sourceDirectory);
            string sourceDataDirectory = Path.GetDirectoryName(sourceDatabase);
            sourceDirectory = string.Equals(Path.GetFileName(sourceDataDirectory), "data", StringComparison.OrdinalIgnoreCase)
                ? Directory.GetParent(sourceDataDirectory).FullName : sourceDataDirectory;
            destinationDirectory = Path.GetFullPath(destinationDirectory).TrimEnd(Path.DirectorySeparatorChar);
            if (Directory.Exists(destinationDirectory) || File.Exists(destinationDirectory)) throw new IOException("Import destination already exists. Existing data will not be overwritten.");
            if (destinationDirectory.Equals(sourceDirectory, StringComparison.OrdinalIgnoreCase) || destinationDirectory.StartsWith(sourceDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Import destination must be outside the original application.");
            string parent = Path.GetDirectoryName(destinationDirectory);
            Directory.CreateDirectory(parent);
            string staging = Path.Combine(parent, "pos-preview-import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                string database = Path.Combine(staging, "data", "pos.db");
                Store.Snapshot(sourceDatabase, database);
                CopyTree(Path.Combine(sourceDirectory,"settings"), Path.Combine(staging,"settings"));
                CopyTree(Path.Combine(sourceDataDirectory,"item_images"), Path.Combine(staging,"data","item_images"));
                string logo = Path.Combine(sourceDataDirectory,"restaurant_logo.png");
                if (File.Exists(logo)) File.Copy(logo,Path.Combine(staging,"data","restaurant_logo.png"));
                File.Copy(database,Path.Combine(staging,"original-snapshot.db"));
                File.WriteAllText(Path.Combine(staging,"IMPORT.txt"),"Independent preview snapshot. Original schema and historical values preserved.\r\nSource: " + sourceDirectory);
                Directory.Move(staging,destinationDirectory);
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging,true); }
        }
        private static void CopyTree(string source,string destination)
        {
            if (!Directory.Exists(source)) return;
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked source directories are not supported by the importer.");
            Directory.CreateDirectory(destination);
            foreach (string path in Directory.GetFiles(source))
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked source files are not supported by the importer.");
                File.Copy(path,Path.Combine(destination,Path.GetFileName(path)));
            }
            foreach (string path in Directory.GetDirectories(source)) CopyTree(path,Path.Combine(destination,Path.GetFileName(path)));
        }
    }
}
