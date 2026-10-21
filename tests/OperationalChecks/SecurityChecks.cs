using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using RestaurantPos;

internal static class SecurityChecks
{
    public static int Run(string output)
    {
        int count=0;Action<bool,string> check=(ok,message)=>{count++;if(!ok)throw new Exception(message);};
        string root=Path.Combine(Path.GetTempPath(),"pos-accounts-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new Store(LocalData.Initialize(root),true);
            Capture(new InitialManagerForm(store),output,"initial-manager");store.CreateInitialManager("QA Manager","test-manager-42");
            check(store.AnyUsers(),"Setup did not provision manager");check(store.Authenticate("QA Manager","wrong")==null,"Wrong login accepted");
            var manager=UserSession.FromUser(store.Authenticate("QA Manager","test-manager-42"));check(manager.IsManager,"Manager role lost on login");manager.RequireManager(store);
            bool blocked=false;try{store.CreateInitialManager("replacement","test-pass");}catch(InvalidOperationException){blocked=true;}check(blocked,"Provisioning overwrote existing accounts");
            foreach(Action action in new Action[]{()=>store.SaveUser(manager.Name,"","manager",false,manager.UserId),()=>store.SaveUser(manager.Name,"","cashier",true,manager.UserId),()=>store.DeleteRecord("users",manager.UserId)})
            {blocked=false;try{action();}catch(InvalidOperationException){blocked=true;}check(blocked,"Last active manager could be removed");}
            store.SaveUser("QA Employee","test-worker-42","cashier",true,null);var employee=UserSession.FromUser(store.Authenticate("QA Employee","test-worker-42"));check(!employee.IsManager,"Employee escalated on login");blocked=false;try{employee.RequireManager(store);}catch(UnauthorizedAccessException){blocked=true;}check(blocked,"Employee manager operation allowed");
            store.SaveUser(employee.Name,"","cashier",false,employee.UserId);check(store.Authenticate(employee.Name,"test-worker-42")==null,"Disabled account logged in");store.SaveUser(employee.Name,"","cashier",true,employee.UserId);
            Capture(new LoginForm(store),output,"login-dark");
            store.SaveRecord("catalog_items",new Dictionary<string,object>{{"name","Popup item"},{"price",30m},{"stock_qty",50m}},null);
            var sample=new Cart{Discount=5};sample.Lines.Add(new CartLine{ItemName="Popup item",UnitPrice=30,Quantity=3,LineDiscount=5});var completedSample=store.SaveOrder(sample,manager.Name,"كاش",100,false);
            foreach(var session in new[]{manager,employee})
            using(var main=new PosForm(store,session))
            {
                main.Show();main.WindowState=FormWindowState.Normal;main.Size=new Size(1366,768);Application.DoEvents();
                check(Controls(main).OfType<Button>().Any(b=>b.Text=="إعدادات")==session.IsManager,"Settings button role visibility wrong");
                using(var timer=new Timer{Interval=200})
                {
                    bool seen=false;Exception failure=null;timer.Tick+=(s,e)=>{var history=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text=="الطلبات السابقة");if(history==null)return;timer.Stop();try{seen=true;check(Controls(history).OfType<Button>().Any(b=>b.Text=="إعادة طباعة"),"Reprint missing for authenticated user");check(Controls(history).OfType<Button>().Any(b=>b.Text=="تعديل الأوردر")==session.IsManager,"Edit order role visibility wrong");Save(history,output,session.IsManager?"history-manager":"history-employee");}catch(Exception error){failure=error;}finally{history.Close();}};
                    timer.Start();Controls(main).OfType<Button>().Single(b=>b.Text=="الطلبات السابقة").PerformClick();check(seen,"History modal was not inspected");if(failure!=null)throw failure;
                }
                if(!session.IsManager)
                {
                    bool denied=false;try{typeof(PosForm).GetMethod("LoadCart",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(main,new object[]{completedSample});}catch(System.Reflection.TargetInvocationException error){denied=error.InnerException is UnauthorizedAccessException;}
                    check(denied,"Employee could load a completed order for editing");
                }
                if(session.IsManager)
                using(var timer=new Timer{Interval=200})
                {
                    bool opened=false;timer.Tick+=(s,e)=>{var settings=Application.OpenForms.OfType<SettingsForm>().FirstOrDefault();if(settings==null)return;timer.Stop();opened=true;Save(settings,output,"manager-settings-direct");settings.Close();};timer.Start();Controls(main).OfType<Button>().Single(b=>b.Text=="إعدادات").PerformClick();check(opened,"Manager settings still requires internal authentication");
                }
                if(session.IsManager)
                {
                    InspectModal(main,()=>Controls(main).OfType<Button>().Single(b=>b.Text.StartsWith("Popup item\n")).PerformClick(),f=>f.Text.StartsWith("صنف:"),output,"product-popup");
                    InspectModal(main,()=>Controls(main).OfType<Button>().Single(b=>b.Text=="خصم").PerformClick(),f=>f.Text=="خصم الأوردر",output,"amount-popup");
                    InspectModal(main,()=>Controls(main).OfType<Button>().Single(b=>b.Text=="ملاحظات الأوردر").PerformClick(),f=>f.Text=="ملاحظات الأوردر",output,"notes-popup");
                    var cart=(Cart)typeof(PosForm).GetField("cart",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(main);cart.Lines.Add(new CartLine{ItemName="Popup item",UnitPrice=30});typeof(PosForm).GetMethod("RefreshCart",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(main,null);
                    InspectModal(main,()=>Controls(main).OfType<Button>().Single(b=>b.Text=="إتمام ودفع").PerformClick(),f=>f.Text=="الدفع",output,"payment-popup");cart.Lines.Clear();
                }
                main.Close();
            }
            store.SaveUser("Second Manager","test-manager-43","manager",true,null);store.SaveUser(manager.Name,"","cashier",true,manager.UserId);blocked=false;try{manager.RequireManager(store);}catch(UnauthorizedAccessException){blocked=true;}check(blocked,"Revoked manager retained permission");
            // Receipt scaling must be independent of bitmap DPI. This caught the small emulator output.
            using(var image=new Bitmap(576,200))
            {
                using(var g=Graphics.FromImage(image)){g.Clear(Color.White);g.FillRectangle(Brushes.Black,10,10,550,170);}
                image.SetResolution(96,96);var normal=EscPosRaster.Encode(image,false);image.SetResolution(203,203);var thermal=EscPosRaster.Encode(image,false);check(normal.SequenceEqual(thermal),"ESC/POS raster shrinks thermal-DPI source");
                int ink=normal.Skip(13).Take(72*128).Sum(b=>Convert.ToString(b,2).Count(c=>c=='1'));check(ink>50000,"Thermal raster unexpectedly small");
            }
            var messageType=typeof(PosForm).Assembly.GetType("RestaurantPos.Dialogs");var message=(Form)messageType.GetMethod("Create",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{"هل تريد متابعة العملية؟\nسيظل الأوردر محفوظًا إذا فشلت الطباعة.","تأكيد",MessageBoxButtons.YesNo,false});Capture(message,output,"confirmation-popup");
            string legacyRoot=Path.Combine(root,"legacy");var legacyStore=new Store(LocalData.Initialize(legacyRoot),true);legacyStore.SaveUser("Old Employee","worker-old","cashier",true,null);var legacySettings=new PosSettings(legacyRoot);legacySettings.SetManager("legacy-manager-42");check(legacyStore.MigrateLegacyManager(),"Old manager password was not moved to login");check(UserSession.FromUser(legacyStore.Authenticate("المدير","legacy-manager-42")).IsManager,"Old manager credential changed");check(!legacyStore.MigrateLegacyManager(),"Legacy manager migration duplicated account");
            var light=new PosSettings(root){Theme="light"};light.Save();Capture(new LoginForm(store),output,"login-light");
            return count;
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    private static void InspectModal(Form owner,Action open,Func<Form,bool> match,string output,string name)
    {
        using(var timer=new Timer{Interval=250}){bool seen=false;timer.Tick+=(s,e)=>{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(match);if(dialog==null)return;timer.Stop();seen=true;Save(dialog,output,name);dialog.DialogResult=DialogResult.Cancel;dialog.Close();};timer.Start();open();if(!seen)throw new Exception("Popup not inspected: "+name);}
    }
    private static IEnumerable<Control> Controls(Control parent){foreach(Control child in parent.Controls){yield return child;foreach(var nested in Controls(child))yield return nested;}}
    private static void Capture(Form form,string output,string name){using(form){form.Show();Application.DoEvents();Save(form,output,name);form.Close();}}
    private static void Save(Form form,string output,string name){using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(output,name+".png"));}}
}
