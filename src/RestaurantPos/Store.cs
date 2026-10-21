using System;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace RestaurantPos
{
    public sealed partial class Store
    {
        private readonly string path;
        private readonly bool writable;
        public string DatabasePath { get { return path; } }
        public string DataRoot { get { return Directory.GetParent(Path.GetDirectoryName(path)).FullName; } }
        public Store(string path, bool writable = false) { this.path = Path.GetFullPath(path); this.writable = writable; }
        private SQLiteConnection Open()
        {
            var builder = new SQLiteConnectionStringBuilder { DataSource = path, ReadOnly = true, FailIfMissing = true, Pooling = false };
            var connection = new SQLiteConnection(builder.ConnectionString);
            try { connection.Open(); return connection; }
            catch { connection.Dispose(); throw; }
        }
        internal DataTable Query(string sql, params object[] values)
        {
            using (var connection = Open())
            using (var command = new SQLiteCommand(sql, connection))
            {
                for (int i = 0; i < values.Length; i++) command.Parameters.AddWithValue("@p" + i, values[i] ?? DBNull.Value);
                using (var reader = command.ExecuteReader()) { var table = new DataTable(); table.Load(reader); return table; }
            }
        }
        public DataTable Menu(string category, string search)
        {
            return Query("SELECT name,price,category,image_path,barcode,stock_qty,low_stock_threshold FROM catalog_items WHERE catalog_type='menu' AND (@p0='' OR category=@p0) AND (@p1='' OR instr(name,@p1)>0 OR barcode=@p1) ORDER BY sort_order,name", category, search);
        }
        public DataTable Categories() { return Query("SELECT name FROM categories ORDER BY sort_order,name"); }
        public DataTable Barcode(string code) { return Query("SELECT name,price FROM catalog_items WHERE catalog_type='menu' AND barcode=@p0 ORDER BY sort_order LIMIT 2", code); }
        public DataTable Users() { return Query("SELECT user_id,name,role FROM users WHERE active=1 ORDER BY name"); }
        public bool AnyUsers() { return Convert.ToInt64(Query("SELECT COUNT(*) FROM users").Rows[0][0]) > 0; }
        public DataRow Authenticate(string name, string pin)
        {
            var rows = Query("SELECT user_id,name,role,pin_salt,pin_hash,pin_iterations FROM users WHERE active=1 AND name=@p0", name);
            if (rows.Rows.Count != 1) return null;
            var row = rows.Rows[0];
            return PinSecurity.Verify(pin, Convert.ToString(row["pin_salt"]), Convert.ToString(row["pin_hash"]), Convert.ToInt32(row["pin_iterations"])) ? row : null;
        }
        public DataTable RecentOrders() { return Query("SELECT order_id,shift_id,created_at,order_type,customer_name,driver_name,total,paid,payment_method,status,cashier_name FROM orders ORDER BY created_at DESC,order_id DESC LIMIT 200"); }
        public DataTable Customers(string search) { return Query("SELECT name,phone,address,notes FROM customers WHERE @p0='' OR instr(name,@p0)>0 OR instr(phone,@p0)>0 ORDER BY name LIMIT 200", search); }
        public DataTable Waiting() { return Query("SELECT order_id,shift_id,created_at,order_type,total,customer_name,table_no FROM orders WHERE status='waiting' ORDER BY created_at LIMIT 200"); }
        public DataTable LowStock() { return Query("SELECT name,stock_qty,low_stock_threshold FROM catalog_items WHERE catalog_type='menu' AND stock_qty IS NOT NULL AND stock_qty<=COALESCE(low_stock_threshold,5) ORDER BY name"); }
        public DataTable Shifts() { return Query("SELECT shift_id,start_at,end_at,status FROM shifts ORDER BY shift_id DESC LIMIT 200"); }
        public string Integrity() { return Convert.ToString(Query("PRAGMA integrity_check").Rows[0][0]); }

        public static void Snapshot(string source, string target)
        {
            if (File.Exists(target)) throw new IOException("Destination already exists; refusing to overwrite.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target)));
            string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var sourceBuilder = new SQLiteConnectionStringBuilder { DataSource = Path.GetFullPath(source), ReadOnly = true, FailIfMissing = true, Pooling = false };
                using (var original = new SQLiteConnection(sourceBuilder.ConnectionString))
                using (var copy = new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = temporary, Pooling = false }.ConnectionString))
                {
                    original.Open(); copy.Open(); original.BackupDatabase(copy, "main", "main", -1, null, 0);
                }
                if (new Store(temporary).Integrity() != "ok") throw new InvalidDataException("Database snapshot failed integrity check.");
                File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
