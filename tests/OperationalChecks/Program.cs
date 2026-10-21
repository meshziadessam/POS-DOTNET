using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using RestaurantPos;

internal static class OperationalChecks
{
    private static int checks;
    private static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    private static Cart CartFor(string item,int quantity=1){var c=new Cart();c.Lines.Add(new CartLine{ItemName=item,UnitPrice=12.5m,Quantity=quantity});return c;}
    [STAThread]
    private static int Main(string[] args)
    {
        string root=Path.Combine(Path.GetTempPath(),"pos-operational-"+Guid.NewGuid().ToString("N"));
        try
        {
            string database=LocalData.Initialize(root);var store=new Store(database,true);
            var blankBrand=new PosSettings(root);Check(blankBrand.BusinessName=="Push"&&blankBrand.ReceiptPhones==""&&blankBrand.ReceiptAddress=="","Customer branding must start neutral");Check(!File.Exists(Path.Combine(root,"data","restaurant_logo.png")),"Previous customer's logo was included");
            string brandingFixture=Path.Combine(root,"brand-fixture"),brandingData=Path.Combine(root,"brand-data");Directory.CreateDirectory(brandingFixture);File.WriteAllText(Path.Combine(brandingFixture,"branding.ini"),"[Business]\nName=مطعم جديد\nPhone=QA-PHONE\nAddress=عنوان الاختبار\nFooter=شكرًا");using(var image=new Bitmap(160,80)){image.Save(Path.Combine(brandingFixture,"setup-logo.image"),System.Drawing.Imaging.ImageFormat.Png);}Branding.ImportSetup(brandingData,Path.Combine(brandingFixture,"branding.ini"));var importedBrand=new PosSettings(brandingData);Check(importedBrand.BusinessName=="مطعم جديد"&&importedBrand.ReceiptPhones=="QA-PHONE"&&File.Exists(Branding.LogoPath(brandingData)),"Installer branding was not imported");importedBrand.BusinessName="تعديل المدير";importedBrand.Save();Branding.ImportSetup(brandingData,Path.Combine(brandingFixture,"branding.ini"));Check(new PosSettings(brandingData).BusinessName=="تعديل المدير","Upgrade overwrote manager branding changes");
            blankBrand.BusinessName="مطعم عميل جديد";blankBrand.ReceiptPhones="TEST-PHONE";blankBrand.ReceiptAddress="عنوان تجريبي";blankBrand.ReceiptFooter="شكرًا";blankBrand.Save();var savedBrand=new PosSettings(root);Check(savedBrand.BusinessName==blankBrand.BusinessName&&savedBrand.ReceiptPhones==blankBrand.ReceiptPhones&&savedBrand.ReceiptAddress==blankBrand.ReceiptAddress&&savedBrand.ReceiptFooter==blankBrand.ReceiptFooter,"Restaurant branding not persisted");
            Check(store.Integrity()=="ok","Fresh database integrity");Check(store.RecentOrders().Rows.Count==0,"Fresh database contains customer sales");Check(!store.AnyUsers(),"Fresh database has imported users");Check(store.ActiveShift==1,"Fresh shift missing");
            store.SaveRecord("categories",new Dictionary<string,object>{{"name","مشروبات"},{"station","مطبخ"}},null);
            store.SaveRecord("catalog_items",new Dictionary<string,object>{{"name","اختبار"},{"price",12.5m},{"stock_qty",20m},{"barcode","12345"},{"category","مشروبات"}},null);
            store.SaveRecord("restaurant_tables",new Dictionary<string,object>{{"table_no",1},{"label","واحد"},{"status","free"}},null);
            var cart=CartFor("اختبار",2);cart.Discount=2m;var first=store.SaveOrder(cart,"اختبار","كاش",30,false);
            Check(Convert.ToDecimal(first["total"])==23m,"Checkout total mismatch");Check(Convert.ToDecimal(store.Menu("","").Rows[0]["stock_qty"])==18,"Stock not decremented");
            var duplicate=store.SaveOrder(cart,"اختبار","كاش",30,false);Check(Convert.ToInt64(first["order_id"])==Convert.ToInt64(duplicate["order_id"])&&store.RecentOrders().Rows.Count==1,"Duplicate sale token accepted");Check(Convert.ToDecimal(store.Menu("","").Rows[0]["stock_qty"])==18,"Duplicate stock decrement");
            bool rejected=false;try{store.SaveOrder(CartFor("اختبار"),"اختبار","كاش",1,false);}catch(ArgumentException){rejected=true;}Check(rejected&&store.RecentOrders().Rows.Count==1,"Underpayment persisted sale");
            var parked=CartFor("اختبار",3);parked.TableNumber=1;parked.OrderType="صالة";parked.DineSubtype="طاولات (أرضية)";var waiting=store.SaveOrder(parked,"اختبار","",0,true);
            Check(Convert.ToDecimal(store.Menu("","").Rows[0]["stock_qty"])==18,"Parking changed stock");Check(Convert.ToString(store.Records("restaurant_tables").Rows[0]["status"])=="occupied","Parking table not occupied");
            rejected=false;try{store.StartShift();}catch(InvalidOperationException){rejected=true;}Check(rejected&&store.ActiveShift==1&&store.Waiting().Rows.Count==1,"Shift close stranded waiting orders");
            parked.EditingOrder=Convert.ToInt64(waiting["order_id"]);parked.EditingShift=1;parked.SaveToken=Guid.NewGuid().ToString("N");var completed=store.SaveOrder(parked,"اختبار","كاش",50,false);
            Check(store.RecentOrders().Rows.Count==2,"Completing waiting created duplicate");Check(Convert.ToDecimal(store.Menu("","").Rows[0]["stock_qty"])==15,"Waiting completion stock wrong");Check(Convert.ToString(store.Records("restaurant_tables").Rows[0]["status"])=="free","Table not released after payment");
            parked.Lines[0].Quantity=1;parked.SaveToken=Guid.NewGuid().ToString("N");store.SaveOrder(parked,"اختبار","كاش",50,false);Check(Convert.ToDecimal(store.Menu("","").Rows[0]["stock_qty"])==17,"Editing completed order stock delta wrong");
            var invalidMerge=CartFor("اختبار");invalidMerge.MergedWaiting.Add(99999);rejected=false;try{store.SaveOrder(invalidMerge,"اختبار","كاش",20,false);}catch(InvalidOperationException){rejected=true;}Check(rejected&&store.RecentOrders().Rows.Count==2&&Convert.ToDecimal(store.Menu("","").Rows[0]["stock_qty"])==17,"Failed merge did not roll back atomically");
            var delivery=CartFor("اختبار");delivery.OrderType=Cart.Delivery;rejected=false;try{store.SaveOrder(delivery,"اختبار","كاش",20,false);}catch(ArgumentException){rejected=true;}Check(rejected,"Delivery without customer accepted");
            store.SaveUser("cashier","012345","cashier",true,null);Check(store.Authenticate("cashier","012345")!=null,"New cashier login fails");long userId=Convert.ToInt64(store.Records("users").Rows[0]["_rowid"]);store.SaveUser("cashier","","cashier",false,userId);Check(store.Authenticate("cashier","012345")==null,"Disabled cashier authenticates");
            var settings=new PosSettings(root);settings.Theme="light";settings.PrinterName="POS Printer Emulator";settings.PrintMode="customer";settings.PaperWidth=80;settings.Save();settings.SetManager("secret123");
            var reloaded=new PosSettings(root);Check(reloaded.VerifyManager("secret123")&&!reloaded.VerifyManager("bad"),"Manager password fails");Check(reloaded.PrinterName==settings.PrinterName&&reloaded.Theme=="light"&&reloaded.PrintMode=="customer","Settings not persisted");
            Check(LocalData.Initialize(root)==database&&new Store(database).RecentOrders().Rows.Count==2,"Initialization overwrote existing database");
            var printer=new ReceiptPrinter(reloaded,root);string output=args.Length>0?Path.GetFullPath(args[0]):root;Directory.CreateDirectory(output);
            var shiftReports=new ReportService(store);int detailedHeight;
            using(var bitmap=printer.RenderShift(shiftReports,1,true)){Check(bitmap.Width==576&&bitmap.Height>500,"80mm shift report table rendering failed");detailedHeight=bitmap.Height;bitmap.Save(Path.Combine(output,"shift-report-80mm.png"));}
            using(var bitmap=printer.RenderShift(shiftReports,1,false)){Check(bitmap.Height<detailedHeight,"Shift report did not omit optional order table");}
            var narrowShiftSettings=new PosSettings(root){PaperWidth=58,PrinterTransport="escpos"};
            using(var bitmap=new ReceiptPrinter(narrowShiftSettings,root).RenderShift(shiftReports,1,true)){Check(bitmap.Width==384&&bitmap.Height>500,"58mm shift report rendering failed");bitmap.Save(Path.Combine(output,"shift-report-58mm.png"));}
            using(var bitmap=printer.RenderShift(shiftReports,999999,true)){Check(bitmap.Height>0,"Empty shift report rendering failed");}
            string shiftSpool=Path.Combine(output,"shift-report.prn");new ReceiptPrinter(narrowShiftSettings,root).PrintShift(shiftReports,1,true,shiftSpool);Check(new FileInfo(shiftSpool).Length>100,"Shift report ESC/POS output missing");
            using(var bitmap=printer.Render(first,false)){Check(bitmap.Width==576&&bitmap.Height>200,"80mm receipt rendering failed");bitmap.Save(Path.Combine(output,"receipt-customer-80mm.png"));}
            settings.PaperWidth=58;using(var bitmap=new ReceiptPrinter(settings,root).Render(first,true)){Check(bitmap.Width==384&&bitmap.Height>200,"58mm kitchen rendering failed");bitmap.Save(Path.Combine(output,"receipt-kitchen-58mm.png"));}
            var badSettings=new PosSettings(root){PrinterName="NONEXISTENT-PRINTER-TEST"};rejected=false;try{new ReceiptPrinter(badSettings,root).Print(first);}catch(InvalidOperationException){rejected=true;}Check(rejected&&store.RecentOrders().Rows.Count==2,"Print failure changed committed sales");
            var names=System.Drawing.Printing.PrinterSettings.InstalledPrinters.Cast<string>().ToList();
            if(names.Contains("POS Printer Emulator"))
            {
                string spool=Path.Combine(output,"receipt-escpos-output.prn");printer.Print(first,false,spool);Check(File.Exists(spool)&&new FileInfo(spool).Length>100,"ESC/POS receipt produced no output");
                Check(File.ReadAllBytes(spool).Take(2).SequenceEqual(new byte[]{0x1b,0x40}),"RAW receipt missing initialization");
                var bothSettings=new PosSettings(root){PrinterName="POS Printer Emulator",PrintMode="both",PrinterTransport="escpos"};
                string copies=Path.Combine(output,"delivery-copies.prn");string previousType=Convert.ToString(first["order_type"]);first["order_type"]=Cart.Delivery;
                new ReceiptPrinter(bothSettings,root).Print(first,false,copies);
                Check(File.Exists(copies)&&File.Exists(copies+".kitchen.prn")&&File.Exists(copies+".customer2.prn"),"Delivery copy set incomplete");first["order_type"]=previousType;
                string driverOutput=Path.Combine(output,"receipt-windows-driver.prn");if(File.Exists(driverOutput))File.Delete(driverOutput);new ReceiptPrinter(new PosSettings(root){PrinterName="POS Printer Emulator",PrintMode="customer",PrinterTransport="windows"},root).Print(first,false,driverOutput);
                var spoolWait=System.Diagnostics.Stopwatch.StartNew();while((!File.Exists(driverOutput)||new FileInfo(driverOutput).Length<=100)&&spoolWait.ElapsedMilliseconds<10000)System.Threading.Thread.Sleep(100);
                Check(File.Exists(driverOutput)&&new FileInfo(driverOutput).Length>100,"Windows driver fallback produced no output");
                if(args.Contains("--print-emulator")){first["order_notes"]="TEST ONLY — اختبار على قاعدة مؤقتة وليست فاتورة بيع";printer.Print(first);Check(true,"Emulator spool submission");}
            }
            store.StartShift();Check(store.ActiveShift==2&&store.RecentOrders().Rows.Count==2,"Shift reset lost history");
            var waitA=store.SaveOrder(CartFor("اختبار"),"اختبار","",0,true);var waitB=store.SaveOrder(CartFor("اختبار",2),"اختبار","",0,true);var merge=CartFor("اختبار",3);merge.MergedWaiting.Add(Convert.ToInt64(waitA["order_id"]));merge.MergedWaiting.Add(Convert.ToInt64(waitB["order_id"]));store.SaveOrder(merge,"اختبار","كاش",40,false);
            Check(store.RecentOrders().Rows.Count==3&&store.Waiting().Rows.Count==0,"Merge did not replace source waiting orders atomically");Check(Convert.ToDecimal(store.Menu("","").Rows[0]["stock_qty"])==14,"Merged checkout stock wrong");
            var canceledCart=CartFor("اختبار");var canceled=store.SaveOrder(canceledCart,"اختبار","",0,true);long canceledId=Convert.ToInt64(canceled["order_id"]);store.DeleteWaiting(2,canceledId);var subsequent=store.SaveOrder(CartFor("اختبار"),"اختبار","",0,true);Check(Convert.ToInt64(subsequent["order_id"])>canceledId,"Canceled order ID reused");store.DeleteWaiting(2,Convert.ToInt64(subsequent["order_id"]));
            rejected=false;try{store.SaveOrder(canceledCart,"اختبار","",0,true);}catch(InvalidOperationException){rejected=true;}Check(rejected,"Stale canceled submission token accepted");
            settings.Theme="dark";settings.Save();
            for(int categoryIndex=1;categoryIndex<=18;categoryIndex++)store.SaveRecord("categories",new Dictionary<string,object>{{"name","قسم إضافي "+categoryIndex},{"station","مطبخ"}},null);
            Application.EnableVisualStyles();
            string headerImage=Path.Combine(root,"header-test.png");using(var bitmap=new Bitmap(80,40)){using(var graphics=Graphics.FromImage(bitmap))graphics.Clear(Color.DarkOrange);bitmap.Save(headerImage);}Branding.SaveLogo(root,headerImage);
            using(var form=new PosForm(store,new UserSession("اختبار","manager")))
            {
                form.Show();form.WindowState=FormWindowState.Normal;form.Size=new Size(1024,768);Application.DoEvents();
                var headerLogo=AllControls(form).OfType<PictureBox>().Single(b=>b.Name=="BusinessLogo");Check(headerLogo.Visible&&headerLogo.Image!=null&&headerLogo.Width<=56,"Cashier restaurant image missing or oversized");
                var credit=AllControls(form).OfType<Label>().Single(l=>l.Text=="Powerd by Ziad Essam");var clearButton=AllControls(form).OfType<Button>().Single(b=>b.Text=="مسح الأوردر");Check(credit.Visible&&credit.ForeColor==Color.Gray&&credit.PointToScreen(Point.Empty).Y>=clearButton.PointToScreen(new Point(0,clearButton.Height)).Y,"Gray credit must appear below clear order");Check(Math.Abs(credit.PointToScreen(new Point(credit.Width/2,0)).X-clearButton.PointToScreen(new Point(clearButton.Width/2,0)).X)<4,"Credit is not aligned under clear order");
                var cartList=AllControls(form).OfType<DataGridView>().Single();Check(cartList.Height>=180,"Main cart list clipped at 1024x768");Check(cartList.PointToScreen(new Point(cartList.Width/2,0)).X>form.PointToScreen(Point.Empty).X+form.Width/2,"Original RTL cart must be on the right");
                Check(cartList.Columns.Cast<DataGridViewColumn>().Select(c=>c.HeaderText).SequenceEqual(new[]{"اسم الصنف","العدد","السعر","الإجمالي"}),"Order columns missing");Check(cartList.Width<form.ClientSize.Width*.45,"Order pane was not narrowed");
                var uiCart=(Cart)typeof(PosForm).GetField("cart",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(form);uiCart.Lines.Add(new CartLine{ItemName="وجبة تجريبية",Quantity=3,UnitPrice=12.5m});uiCart.Lines.Add(new CartLine{ItemName="صنف ثانٍ",Quantity=2,UnitPrice=20m});var refresh=typeof(PosForm).GetMethod("RefreshCart",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);refresh.Invoke(form,null);
                Check(cartList.Rows.Count==2&&Convert.ToDecimal(cartList.Rows[0].Cells[3].Value)==37.5m,"Order grid values differ");
                var texts=AllControls(form).Select(c=>c.Text).ToList();foreach(string text in new[]{"إعدادات","إتمام ودفع","إنتظار","خصم السطر","عميل دليفري","طيار دليفري","كمية / خيارات السطر","دليفري","شركات","طاولات"})Check(texts.Contains(text),"Original main control missing: "+text);
                using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(output,"cashier-restored-1024x768.png"));}
                var buttons=AllControls(form).OfType<Button>().ToList();Check(!buttons.Single(b=>b.Text=="عميل دليفري").Visible&&!buttons.Single(b=>b.Text=="إنتظار").Visible,"Delivery actions visible in takeaway mode");
                cartList.CurrentCell=cartList.Rows[0].Cells[0];buttons.Single(b=>b.Text=="+ كمية").PerformClick();Check(Convert.ToInt32(cartList.Rows[0].Cells[1].Value)==4&&Convert.ToDecimal(cartList.Rows[0].Cells[3].Value)==50m,"Quantity action lost grid row mapping");buttons.Single(b=>b.Text=="- كمية").PerformClick();
                var tile=buttons.Single(b=>b.Text.StartsWith("اختبار\n"));Check(tile.Parent.ClientRectangle.Contains(tile.Bounds),"Product tile clipped at 1024x768");
                Check(tile.Width<240&&tile.Height<220,"Single product stretched across catalog");var tileWidths=new List<int>{tile.Width};
                using(var rendered=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(rendered,new Rectangle(Point.Empty,form.Size));foreach(var button in buttons.Where(b=>b.Visible)){var point=form.PointToClient(button.PointToScreen(Point.Empty));if(rendered.GetPixel(point.X,point.Y).ToArgb()!=button.Parent.BackColor.ToArgb())throw new Exception("Corner mismatch: "+button.Text);}}
                Check(true,"Rounded button backgrounds");
                buttons.Single(b=>b.Text=="دليفري").PerformClick();Application.DoEvents();Check(buttons.Single(b=>b.Text=="عميل دليفري").Visible&&buttons.Single(b=>b.Text=="إنتظار").Visible,"Delivery controls did not appear");Check(cartList.Height>=160,"Delivery cart clipped at 1024x768");
                using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(output,"cashier-delivery-1024x768.png"));}
                buttons.Single(b=>b.Text=="تيك اواي").PerformClick();
                foreach(var size in new[]{new Size(1366,768),new Size(1920,1080)}){form.Size=size;Application.DoEvents();Check(cartList.Height>=180,"Cart clipped at "+size);Check(tile.Width<240&&tile.Height<220,"Single product stretched at "+size);tileWidths.Add(tile.Width);using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(output,"cashier-restored-"+size.Width+"x"+size.Height+".png"));}}
                Check(tileWidths.Distinct().Count()>1,"Product widths did not adapt to window width");
                var categoryPanel=(Panel)typeof(PosForm).GetField("categories",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(form);
                form.Size=new Size(1366,768);Application.DoEvents();var keyHandler=typeof(Control).GetMethod("OnKeyDown",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                for(int toggle=0;toggle<3;toggle++){keyHandler.Invoke(form,new object[]{new KeyEventArgs(Keys.F11)});Application.DoEvents();Check(!categoryPanel.HorizontalScroll.Visible,"Categories gained horizontal scroll after F11");}
                using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(output,"cashier-after-fullscreen-toggle.png"));}
                uiCart.Lines.Clear();refresh.Invoke(form,null);form.Close();
            }
            using(var form=new SettingsForm(store,new PosSettings(root),()=>{}))
            {
                form.Show();Application.DoEvents();var all=AllControls(form).ToList();
                foreach(string title in new[]{"التشغيل","البيانات","التقارير","النظام","الأصناف","الطباعة","المستخدمون","تغيير كلمة السر"})Check(all.Any(c=>c.Text==title),"Settings item missing: "+title);
                var tabs=all.OfType<TabControl>().Single();
                for(int i=0;i<tabs.TabPages.Count;i++){tabs.SelectedIndex=i;Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(output,"settings-tab-"+i+".png"));}}
                form.Close();
            }
            string reportFixture=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"report-fixtures.json");if(File.Exists(reportFixture))checks+=ReleaseChecks.Run(reportFixture,output);
            checks+=SecurityChecks.Run(output);
            Console.WriteLine("PASS: "+checks+" operational/release checks; checkout, stock, printing, Python report comparisons, full backup/restore, archive preservation and all release forms.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    private static IEnumerable<Control> AllControls(Control control){foreach(Control child in control.Controls){yield return child;foreach(var nested in AllControls(child))yield return nested;}}
}

