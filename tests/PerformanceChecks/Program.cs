using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using RestaurantPos;

static class PerformanceChecks
{
    static readonly List<object> results=new List<object>();
    static IEnumerable<Control> All(Control p){foreach(Control c in p.Controls){yield return c;foreach(var d in All(c))yield return d;}}
    static void Measure(string name,int count,Action action){action();var samples=new List<double>();for(int i=0;i<count;i++){var sw=Stopwatch.StartNew();action();sw.Stop();samples.Add(sw.Elapsed.TotalMilliseconds);}samples.Sort();var row=new {name,count,median_ms=samples[count/2],p95_ms=samples[(int)Math.Ceiling(count*.95)-1],max_ms=samples.Last()};results.Add(row);Console.WriteLine(new JavaScriptSerializer().Serialize(row));}
    static Cart Sale(){var cart=new Cart();cart.Lines.Add(new CartLine{ItemName="صنف اختبار 0001",UnitPrice=30,Quantity=2});return cart;}
    static void VerifyCatalog(Form form,Store store,string output)
    {
        var field=typeof(PosForm).GetField("products",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var panel=(Panel)field.GetValue(form);var arrange=panel.GetType().GetMethod("ArrangeTiles",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Virtual catalog: "+message);Console.WriteLine("PASS: "+message);};
        check(panel.Controls.OfType<Button>().Count()<100,"native button count bounded for 1000 items");
        var first=panel.Controls.OfType<Button>().First();
        var reload=typeof(PosForm).GetMethod("Reload",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        reload.Invoke(form,null);Application.DoEvents();check(panel.Controls.Contains(first),"reload reuses existing buttons");
        var scrollTo=panel.GetType().GetMethod("ScrollToPixel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        scrollTo.Invoke(panel,new object[]{int.MaxValue});Application.DoEvents();
        var last=panel.Controls.OfType<Button>().SingleOrDefault(b=>b.Visible&&b.Text.StartsWith("صنف اختبار 1000\n"));
        check(last!=null&&panel.ClientRectangle.IntersectsWith(last.Bounds),"last product reachable by scrolling");
        var wheel=panel.GetType().GetMethod("OnMouseWheel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        wheel.Invoke(panel,new object[]{new HandledMouseEventArgs(MouseButtons.None,0,20,20,-12000)});Application.DoEvents();
        check(panel.Controls.OfType<Button>().Any(b=>b.Visible&&panel.ClientRectangle.IntersectsWith(b.Bounds)),"mouse wheel at bottom keeps products visible");
        using(var timer=new Timer{Interval=100}){
            bool seen=false;timer.Tick+=(s,e)=>{var modal=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text.StartsWith("صنف:"));if(modal==null)return;timer.Stop();seen=modal.Text.Contains("1000");modal.DialogResult=DialogResult.Cancel;modal.Close();};
            timer.Start();last.PerformClick();check(seen,"recycled last button opens the correct product");
        }
        using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(Point.Empty,form.Size));bmp.Save(Path.Combine(output,"catalog-bottom.png"));}
        using(var c=new SQLiteConnection("Data Source="+store.DatabasePath+";Pooling=False")){c.Open();using(var cmd=new SQLiteCommand("UPDATE catalog_items SET price=37 WHERE name='صنف اختبار 0001'",c))cmd.ExecuteNonQuery();}
        reload.Invoke(form,null);Application.DoEvents();
        check(panel.Controls.OfType<Button>().Any(b=>b.Visible&&b.Text=="صنف اختبار 0001\n37.00 ج"),"catalog edits refresh recycled buttons");
        using(var c=new SQLiteConnection("Data Source="+store.DatabasePath+";Pooling=False")){c.Open();using(var cmd=new SQLiteCommand("UPDATE catalog_items SET price=30 WHERE name='صنف اختبار 0001'",c))cmd.ExecuteNonQuery();}
        All(form).OfType<Button>().Single(b=>b.Text=="قسم 1").PerformClick();Application.DoEvents();
        check(panel.Controls.OfType<Button>().Where(b=>b.Visible).All(b=>int.Parse(b.Text.Split('\n')[0].Split(' ').Last())%20==0),"category contains only its own products");
        All(form).OfType<Button>().Single(b=>b.Text=="الكل").PerformClick();Application.DoEvents();
        check(panel.Controls.OfType<Button>().Count()<100,"category switching does not grow native controls with catalog size");
    }
    [STAThread] static int Main(string[] args)
    {
        try {
            string output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
            string root=Path.Combine(output,"sandbox");bool manual=args.Contains("--manager")||args.Contains("--employee");
            string db=LocalData.Initialize(root);var store=new Store(db,true);
            if(!store.AnyUsers()){
                store.CreateInitialManager("مدير اختبار","qa-manager-only");store.SaveUser("موظف اختبار","qa-employee-only","cashier",true,null);
                using(var connection=new SQLiteConnection("Data Source="+db+";Pooling=False")){connection.Open();using(var tx=connection.BeginTransaction()){
                    for(int i=1;i<=20;i++)using(var cmd=new SQLiteCommand("INSERT INTO categories(name,sort_order) VALUES(@n,@i)",connection,tx)){cmd.Parameters.AddWithValue("@n","قسم "+i);cmd.Parameters.AddWithValue("@i",i);cmd.ExecuteNonQuery();}
                    for(int i=1;i<=1000;i++)using(var cmd=new SQLiteCommand("INSERT INTO catalog_items(catalog_type,name,price,category,barcode,sort_order) VALUES('menu',@n,30,@c,@b,@i)",connection,tx)){cmd.Parameters.AddWithValue("@n","صنف اختبار "+i.ToString("0000"));cmd.Parameters.AddWithValue("@c","قسم "+(i%20+1));cmd.Parameters.AddWithValue("@b","QA"+i.ToString("0000"));cmd.Parameters.AddWithValue("@i",i);cmd.ExecuteNonQuery();}
                    tx.Commit();}}
                store.SaveOrder(Sale(),"اختبار أداء","كاش",100,false);
                using(var connection=new SQLiteConnection("Data Source="+db+";Pooling=False")){connection.Open();using(var tx=connection.BeginTransaction()){
                    // Clone a valid saved fixture, preserving every schema column; setup is outside measurements.
                    string[] columns;using(var cmd=new SQLiteCommand("PRAGMA table_info(orders)",connection,tx))using(var r=cmd.ExecuteReader()){var names=new List<string>();while(r.Read())names.Add(Convert.ToString(r["name"]));columns=names.ToArray();}
                    string select=string.Join(",",columns.Select(c=>c=="order_id"?"@id":"\""+c+"\""));
                    using(var cmd=new SQLiteCommand("INSERT INTO orders SELECT "+select+" FROM orders WHERE order_id=1 LIMIT 1",connection,tx)){cmd.Parameters.Add("@id",System.Data.DbType.Int64);for(int i=2;i<=10000;i++){cmd.Parameters[0].Value=i;cmd.ExecuteNonQuery();}}
                    tx.Commit();}}
                new PosSettings(root){Theme="dark",PrinterName="QA-NO-PRINTER"}.Save();
            }
            Application.EnableVisualStyles();
            if(args.Contains("--reload-bench")){
                var who=UserSession.FromUser(store.Authenticate("مدير اختبار","qa-manager-only"));
                using(var form=new PosForm(store,who)){
                    form.Show();Application.DoEvents();
                    var reload=typeof(PosForm).GetMethod("Reload",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    Measure("settings_close_reload_1000_tiles",5,()=>{reload.Invoke(form,null);Application.DoEvents();});
                    Measure("category_50_then_all_1000_tiles",5,()=>{All(form).OfType<Button>().Single(b=>b.Text=="قسم 1").PerformClick();Application.DoEvents();All(form).OfType<Button>().Single(b=>b.Text=="الكل").PerformClick();Application.DoEvents();});
                    VerifyCatalog(form,store,output);
                }
                File.WriteAllText(Path.Combine(output,"reload-performance.json"),new JavaScriptSerializer().Serialize(results));return 0;
            }
            if(manual){bool employee=args.Contains("--employee");var row=store.Authenticate(employee?"موظف اختبار":"مدير اختبار",employee?"qa-employee-only":"qa-manager-only");using(var form=new PosForm(store,UserSession.FromUser(row))){form.Text="QA ONLY — "+(employee?"EMPLOYEE":"MANAGER");Application.Run(form);}return 0;}
            Measure("barcode_1000_products",100,()=>{if(store.Barcode("QA0500").Rows.Count!=1)throw new Exception("barcode result");});
            Measure("search_1000_products",100,()=>{if(store.Menu("","اختبار 05").Rows.Count!=100)throw new Exception("search result");});
            Measure("recent_10000_orders",50,()=>{if(store.RecentOrders().Rows.Count!=200)throw new Exception("history result");});
            Measure("summary_10000_orders",15,()=>{if(new ReportService(store).Summary().Orders!=10000)throw new Exception("summary result");});
            Measure("authenticate_PBKDF2_240000",7,()=>{if(store.Authenticate("مدير اختبار","qa-manager-only")==null)throw new Exception("login result");});
            // 31 measured+warmup transactions use real SaveOrder, no printer and no real customers.
            Measure("save_order_transaction",30,()=>store.SaveOrder(Sale(),"اختبار أداء","كاش",100,false));
            var session=UserSession.FromUser(store.Authenticate("مدير اختبار","qa-manager-only"));
            var process=Process.GetCurrentProcess();process.Refresh();long before=process.PrivateMemorySize64;
            Measure("cashier_construct_show_1000_tiles",5,()=>{using(var f=new PosForm(store,session)){f.Show();Application.DoEvents();f.Refresh();Application.DoEvents();}});
            using(var form=new PosForm(store,session)){form.Show();Application.DoEvents();GC.Collect();GC.WaitForPendingFinalizers();process.Refresh();results.Add(new{name="memory_loaded",private_mb=process.PrivateMemorySize64/1048576.0,working_set_mb=process.WorkingSet64/1048576.0,private_before_ui_mb=before/1048576.0,buttons=All(form).OfType<Button>().Count()});using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(Point.Empty,form.Size));bmp.Save(Path.Combine(output,"stress-1000-products.png"));}}
            if(store.Integrity()!="ok")throw new Exception("integrity");
            File.WriteAllText(Path.Combine(output,"performance.json"),new JavaScriptSerializer().Serialize(new{utc=DateTime.UtcNow.ToString("o"),process_bits=IntPtr.Size*8,products=1000,orders_before_transactions=10000,warmup_per_metric=1,results}));return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
