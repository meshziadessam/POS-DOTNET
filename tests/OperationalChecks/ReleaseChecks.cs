using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using RestaurantPos;

internal static class ReleaseChecks
{
    public static int Run(string fixtures,string screenshots)
    {
        int count=0;Action<bool,string> check=(ok,label)=>{count++;if(!ok)throw new Exception(label);};string root=Path.Combine(Path.GetTempPath(),"pos-release-"+Guid.NewGuid().ToString("N"));
        try
        {
            var input=(Dictionary<string,object>)new JavaScriptSerializer().DeserializeObject(File.ReadAllText(fixtures));var expected=(Dictionary<string,object>)input["expected"];var database=LocalData.Initialize(root);var store=new Store(database,true);
            using(var c=new SQLiteConnection("Data Source="+database+";Pooling=False"))
            {
                c.Open();using(var tx=c.BeginTransaction())
                {
                    foreach(string table in new[]{"orders","expenses"})foreach(Dictionary<string,object> row in (object[])input[table]){var stored=new Dictionary<string,object>(row);if(stored.ContainsKey("driver_id")&&Convert.ToString(stored["driver_id"])=="")stored["driver_id"]=null;Insert(c,tx,table,stored);}
                    foreach(Dictionary<string,object> row in (object[])input["catalog"]){var item=new Dictionary<string,object>(row){{"catalog_type","menu"},{"price",1}};Insert(c,tx,"catalog_items",item);}
                    using(var command=new SQLiteCommand("INSERT INTO shifts(shift_id,start_at,end_at,status) VALUES(2,'2026-01-01 08:00:00','','closed')",c,tx))command.ExecuteNonQuery();tx.Commit();
                }
            }
            var report=new ReportService(store);var summary=report.Summary(1);var s=(Dictionary<string,object>)expected["summary"];
            foreach(var pair in new Dictionary<string,decimal>{{"orders_count",summary.Orders},{"items_count",summary.Items},{"gross_sales",summary.Gross},{"discounts_total",summary.Discounts},{"delivery_fee_total",summary.Delivery},{"net_after_discount",summary.Net},{"vault_total",summary.Expenses},{"net_after_vault",summary.AfterExpenses}})check(Math.Abs(pair.Value-Convert.ToDecimal(s[pair.Key],CultureInfo.InvariantCulture))<.00000001m,"Python report summary differs: "+pair.Key);
            Compare(report.ByItem(1),(object[])expected["items"],check);Compare(report.ByType(1),(object[])expected["types"],check);Compare(report.ByDriver(1),(object[])expected["drivers"],check);
            DateTime today=DateTime.ParseExact((string)input["today"],"yyyy-MM-dd",CultureInfo.InvariantCulture);Compare(report.SalesByDay(7,today),(object[])expected["daily"],check);Compare(report.SalesByCategory(7,today),(object[])expected["categories"],check);
            var daily=report.Summary(null,today.ToString("yyyy-MM-dd"),today.ToString("yyyy-MM-dd"));check(daily.Net==Convert.ToDecimal(((Dictionary<string,object>)expected["daily_summary"])["net_after_discount"]),"Daily summary differs");
            check(report.ShiftLines(1,true).Any(line=>line.Contains("#1")),"Shift report missing orders");check(!report.ShiftLines(1,false).Any(line=>line.StartsWith("#")),"Shift report checkbox ignored");check(report.DriverLines(1,1).Any(line=>line.Contains("عدد الأوردرات: 2")),"Driver report dropped waiting or delivery orders");
            var settings=new PosSettings(root){PrinterName="POS Printer Emulator",PrintMode="both",PrinterTransport="escpos"};settings.SetManager(" manager with spaces ");settings.Save();check(new PosSettings(root).VerifyManager(" manager with spaces "),"Manager spaces changed");check(!new PosSettings(root).VerifyManager("manager with spaces"),"Manager password trimmed");
            byte[] raw;using(var bitmap=new Bitmap(9,2)){using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.White);graphics.FillRectangle(Brushes.Black,0,0,1,1);}raw=EscPosRaster.Encode(bitmap,true);}
            check(raw[0]==0x1b&&raw[1]==0x40&&raw[5]==0x1d&&raw[6]==0x76&&raw[9]==2,"ESC/POS raster header incorrect");check(raw[13]==0x80&&raw[14]==0&&raw[15]==0&&raw[16]==0,"ESC/POS bit order or white padding incorrect");check(raw[raw.Length-3]==0x1d&&raw[raw.Length-2]==0x56,"ESC/POS cut missing");
            using(var logo=new Bitmap(32,32)){using(var graphics=Graphics.FromImage(logo))graphics.Clear(Color.DarkBlue);logo.Save(Path.Combine(root,"data","restaurant_logo.png"));}
            string package=Path.Combine(root,"backups","full.zip");BackupService.Create(root,package);check(File.Exists(package),"Full backup missing");int original=report.Orders().Rows.Count;
            store.SaveRecord("customers",new Dictionary<string,object>{{"name","عميل تجريبي"},{"phone","+20 100 123 4567"},{"address","عنوان تجريبي"},{"notes",""}},null);check(store.SearchContacts("customers","1001234567").Rows.Count==1,"Normalized contact search failed");
            string safety=BackupService.Restore(root,package);check(File.Exists(safety),"Restore omitted recovery backup");store=new Store(database,true);check(new ReportService(store).Orders().Rows.Count==original&&store.Records("customers").Rows.Count==0,"Restore did not restore data exactly");check(new PosSettings(root).VerifyManager(" manager with spaces "),"Restore lost manager settings");check(File.Exists(Path.Combine(root,"data","restaurant_logo.png")),"Restore lost receipt logo");
            string badZip=Path.Combine(root,"backups","bad.zip");using(var zip=ZipFile.Open(badZip,ZipArchiveMode.Create)){using(var writer=new StreamWriter(zip.CreateEntry("../outside.txt").Open()))writer.Write("bad");}bool rejected=false;try{BackupService.Restore(root,badZip);}catch(InvalidDataException){rejected=true;}check(rejected&&!File.Exists(Path.Combine(root,"outside.txt")),"Zip traversal accepted");check(new ReportService(store).Orders().Rows.Count==original,"Bad restore changed current data");
            store.ReserveTable(EnsureTable(store));rejected=false;try{store.ReserveTable(10);}catch(InvalidOperationException){rejected=true;}check(rejected,"Occupied table reserved twice");store.ReleaseTable(10);check(Convert.ToString(store.Records("restaurant_tables").Rows[0]["status"])=="free","Abandoned table remained occupied");
            var forms=new List<Form>{new CatalogForm(store,false),new ContactsForm(store,"customers",false),new ContactEditorForm(store,"customers",null,false),new ContactsForm(store,"drivers",false,true),new ContactsForm(store,"companies",false,true),new UsersForm(store,false),new TablesForm(store,false),new PrinterSettingsForm(store,settings),new ExpensesForm(store,settings),new AuditForm(store,settings),new ShiftReportsForm(store,settings,1),new PreviousShiftsForm(store,settings),new SalesDashboardForm(store,settings),new ArchiveForm(store,settings)};
            for(int i=0;i<forms.Count;i++)using(var form=forms[i]){form.Show();Application.DoEvents();check(form.Controls.Count>0,"Empty release form: "+form.Text);using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(screenshots,"release-form-"+i+".png"));}form.Close();}
            // Validate all closed-shift batches and prove that archiving preserves order detail.
            using(var c=new SQLiteConnection("Data Source="+database+";Pooling=False")){c.Open();using(var command=new SQLiteCommand("UPDATE shifts SET status='closed',end_at='2026-01-01 18:00:00'",c))command.ExecuteNonQuery();for(int sid=3;sid<=16;sid++)using(var command=new SQLiteCommand("INSERT INTO shifts(shift_id,start_at,end_at,status) VALUES("+sid+",'2026-01-01 08:00:00','2026-01-01 18:00:00','closed')",c))command.ExecuteNonQuery();using(var command=new SQLiteCommand("INSERT INTO shifts(shift_id,start_at,end_at,status) VALUES(17,'2026-01-01 08:00:00','','open')",c))command.ExecuteNonQuery();}
            store.UpdateArchives();check(store.Records("shift_batch_archives").Rows.Count==15,"15-shift batch incomplete");check(new ReportService(store).Orders().Rows.Count==original,"Archiving removed historical orders");store.UpdateArchives();check(store.Records("shift_batch_archives").Rows.Count==15,"Repeated archive duplicated summaries");
            return count;
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    private static int EnsureTable(Store store){store.SaveRecord("restaurant_tables",new Dictionary<string,object>{{"table_no",10},{"label","تجربة"},{"status","free"}},null);return 10;}
    private static void Insert(SQLiteConnection c,SQLiteTransaction tx,string table,Dictionary<string,object> row)
    {using(var cmd=new SQLiteCommand("INSERT INTO "+table+"("+string.Join(",",row.Keys.Select(k=>"\""+k+"\""))+") VALUES("+string.Join(",",Enumerable.Range(0,row.Count).Select(i=>"@p"+i))+")",c,tx)){int i=0;foreach(var value in row.Values)cmd.Parameters.AddWithValue("@p"+i++,value??DBNull.Value);cmd.ExecuteNonQuery();}}
    private static void Compare(DataTable actual,object[] expected,Action<bool,string> check)
    {
        check(actual.Rows.Count==expected.Length,"Python report row count differs: "+string.Join(",",actual.Columns.Cast<DataColumn>().Select(c=>c.ColumnName))+" actual="+actual.Rows.Count+" expected="+expected.Length+" rows="+string.Join(";",actual.Rows.Cast<DataRow>().Select(r=>string.Join("|",r.ItemArray))));foreach(Dictionary<string,object> row in expected)
        {check(actual.Rows.Cast<DataRow>().Any(candidate=>row.All(pair=>{if(!actual.Columns.Contains(pair.Key))return false;decimal a,b;if(decimal.TryParse(Convert.ToString(candidate[pair.Key]),NumberStyles.Any,CultureInfo.InvariantCulture,out a)&&decimal.TryParse(Convert.ToString(pair.Value,CultureInfo.InvariantCulture),NumberStyles.Any,CultureInfo.InvariantCulture,out b))return Math.Abs(a-b)<.00000001m;return Convert.ToString(candidate[pair.Key])==Convert.ToString(pair.Value);})),"Python report row differs: "+string.Join(" | ",row.Values));}
    }
}
