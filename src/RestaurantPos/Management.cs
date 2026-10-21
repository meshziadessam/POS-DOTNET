using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace RestaurantPos
{
    internal sealed class ManagementForm : Form
    {
        private readonly Store store;
        private readonly string table;
        private readonly DataGridView grid;
        private readonly string[][] fields;
        private static readonly Dictionary<string,string> Titles=new Dictionary<string,string>{{"catalog_items","الأصناف"},{"categories","الفئات"},{"item_options","خيارات الأصناف"},{"item_variants","تنويعات الأصناف"},{"customers","إدارة العملاء"},{"drivers","الطيارين"},{"companies","الشركات"},{"restaurant_tables","الطاولات"},{"expenses","الخزنة"},{"users","المستخدمون"}};
        private static readonly Dictionary<string,string[]> Definitions=new Dictionary<string,string[]>{
            {"catalog_items",new[]{"name|اسم الصنف|text","price|السعر الأساسي|money","category|الفئة|text","barcode|الباركود|text","stock_qty|الكمية (فارغ لمخزون غير محدود)|nullable","low_stock_threshold|حد التنبيه|money","image_path|ملف الصورة|text"}},
            {"categories",new[]{"name|اسم الفئة|text","station|قسم الطباعة|text"}},
            {"item_options",new[]{"option|الخيار|text"}},
            {"item_variants",new[]{"item_name|اسم الصنف|text","variant_name|التنويع|text","price|السعر|money"}},
            {"customers",new[]{"name|الاسم|text","phone|رقم التليفون|text","address|العناوين (افصل بعلامة ؛)|text","notes|ملاحظات|text"}},
            {"drivers",new[]{"name|الاسم|text","phone|رقم التليفون|text","notes|ملاحظات|text"}},
            {"companies",new[]{"name|الاسم|text","phone|رقم التليفون|text","notes|ملاحظات|text"}},
            {"restaurant_tables",new[]{"table_no|رقم الطاولة|int","label|اسم الطاولة|text","status|الحالة (free أو occupied)|text"}},
            {"expenses",new[]{"who|المستلم|text","amount_egp|المبلغ|money","note|ملاحظات|text"}},
            {"users",new[]{"name|الاسم|text","role|الصلاحية (cashier أو manager)|text","active|مفعّل (1 أو 0)|int"}}
        };
        public ManagementForm(Store store,string table,bool light)
        {
            this.store=store;this.table=table;fields=Definitions[table].Select(x=>x.Split('|')).ToArray();Text=Titles[table];Size=new Size(980,720);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",11,FontStyle.Bold);AutoScaleMode=AutoScaleMode.Dpi;RightToLeft=RightToLeft.Yes;
            var panel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(12)};panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var search=new TextBox{Width=250};panel.Controls.Add(Ui.Row(new Label{Text=Text,AutoSize=true},search),0,0);
            grid=Ui.Grid(store.Records(table));panel.Controls.Add(grid,0,1);
            search.TextChanged+=(s,e)=>{var data=(DataTable)grid.DataSource;string query=search.Text.Replace("'","''").Replace("[","[[]").Replace("%","[%]").Replace("*","[*]");data.DefaultView.RowFilter=string.Join(" OR ",fields.Where(f=>data.Columns.Contains(f[0])).Select(f=>"CONVERT(["+f[0]+"], 'System.String') LIKE '%"+query+"%'"));};
            var buttons=Ui.Row(Ui.Button("إضافة",()=>Edit(null),"success"),Ui.Button("تعديل",()=>Edit(Selected())),Ui.Button("حذف",Delete,"danger"));
            if(table=="catalog_items")
            {
                buttons.Controls.Add(Ui.Button("الفئات",()=>{using(var f=new ManagementForm(store,"categories",light))f.ShowDialog(this);}));
                buttons.Controls.Add(Ui.Button("الخيارات",()=>{using(var f=new ManagementForm(store,"item_options",light))f.ShowDialog(this);}));
                buttons.Controls.Add(Ui.Button("التنويعات",()=>{using(var f=new ManagementForm(store,"item_variants",light))f.ShowDialog(this);}));
                buttons.Controls.Add(Ui.Button("اختيار صورة",()=>
                {
                    var row=Selected();using(var picker=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.jpeg;*.bmp",CheckFileExists=true})
                    if(picker.ShowDialog(this)==DialogResult.OK)
                    {
                        string name=Guid.NewGuid().ToString("N")+".png";string path=Path.Combine(store.DataRoot,"data","item_images",name);
                        using(var image=Image.FromFile(picker.FileName))image.Save(path,System.Drawing.Imaging.ImageFormat.Png);
                        store.SaveRecord(table,new Dictionary<string,object>{{"image_path",name}},Convert.ToInt64(row["_rowid"]));RefreshData();
                    }
                }));
            }
            buttons.Controls.Add(Ui.Button("إغلاق",Close,"default"));panel.Controls.Add(buttons,0,2);Controls.Add(panel);RefreshData();Ui.Theme(this,light);
        }
        private DataRow Selected(){if(!(grid.CurrentRow?.DataBoundItem is DataRowView row))throw new ArgumentException("اختر صفًا أولاً");return row.Row;}
        private void RefreshData()
        {
            grid.DataSource=store.Records(table);if(grid.Columns.Contains("_rowid"))grid.Columns["_rowid"].Visible=false;
            foreach(var field in fields)if(grid.Columns.Contains(field[0]))grid.Columns[field[0]].HeaderText=field[1];
            foreach(string column in new[]{"catalog_type","sort_order"})if(grid.Columns.Contains(column))grid.Columns[column].Visible=false;
        }
        private void Edit(DataRow row)
        {
            var input=new Dictionary<string,string>();foreach(var field in fields)input[field[1]]=row==null?(field[0]=="price"?"0":field[0]=="low_stock_threshold"?"5":field[0]=="active"?"1":field[0]=="role"?"cashier":field[0]=="status"?"free":""):Convert.ToString(row[field[0]]);
            if(table=="users")input["الرقم السري (فارغ للإبقاء عليه)"]="";
            var result=Ui.Input(this,row==null?"إضافة — "+Text:"تعديل — "+Text,input,"الرقم السري (فارغ للإبقاء عليه)");if(result==null)return;
            var values=new Dictionary<string,object>();
            foreach(var field in fields)
            {
                string raw=result[field[1]];
                if(field[2]=="nullable"&&raw=="")values[field[0]]=null;
                else if(field[2]=="money"||field[2]=="nullable"){decimal n;if(!decimal.TryParse(raw,NumberStyles.Number,CultureInfo.CurrentCulture,out n)&&!decimal.TryParse(raw,NumberStyles.Number,CultureInfo.InvariantCulture,out n))throw new ArgumentException("راجع "+field[1]);if(n<0)throw new ArgumentException("القيمة لا يمكن أن تكون سالبة");values[field[0]]=n;}
                else if(field[2]=="int"){int n;if(!int.TryParse(raw,out n))throw new ArgumentException("راجع "+field[1]);values[field[0]]=n;}
                else values[field[0]]=raw;
            }
            if(table=="customers"&&Convert.ToString(values["address"]).Contains("؛"))values["address"]=new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(Convert.ToString(values["address"]).Split('؛').Select(v=>v.Trim()).Where(v=>v!="").ToArray());
            if(table=="users")store.SaveUser(Convert.ToString(values["name"]),result["الرقم السري (فارغ للإبقاء عليه)"],Convert.ToString(values["role"]),Convert.ToInt32(values["active"])!=0,row==null?(long?)null:Convert.ToInt64(row["_rowid"]));
            else store.SaveRecord(table,values,row==null?(long?)null:Convert.ToInt64(row["_rowid"]));RefreshData();
        }
        private void Delete(){var row=Selected();if(Dialogs.Show(this,"حذف الصف المحدد؟","تأكيد",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;store.DeleteRecord(table,Convert.ToInt64(row["_rowid"]));RefreshData();}
    }

    public sealed class SettingsForm : Form
    {
        public SettingsForm(Store store,PosSettings settings,Action refreshed,Func<bool> canRestore=null)
        {
            Text="الإعدادات";Size=new Size(860,650);MinimumSize=new Size(650,500);Font=new Font("Segoe UI",12,FontStyle.Bold);AutoScaleMode=AutoScaleMode.Dpi;StartPosition=FormStartPosition.CenterParent;RightToLeft=RightToLeft.Yes;
            var outer=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=3};outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));outer.RowStyles.Add(new RowStyle(SizeType.Percent,100));outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));outer.Controls.Add(new Label{Text="الإعدادات",AutoSize=true,Font=new Font(Font.FontFamily,18,FontStyle.Bold)},0,0);
            var tabs=new ThemeTabs{Dock=DockStyle.Fill,ItemSize=new Size(130,45),SizeMode=TabSizeMode.Fixed};outer.Controls.Add(tabs,0,1);
            var ops=Page(tabs,"التشغيل");
            var both=new RadioButton{Text="طباعة (عميل + مطبخ)",AutoSize=true,Checked=settings.PrintMode=="both"};var customer=new RadioButton{Text="طباعة (عميل فقط)",AutoSize=true,Checked=settings.PrintMode=="customer"};
            both.CheckedChanged+=(s,e)=>{if(both.Checked){settings.PrintMode="both";settings.Save();}};customer.CheckedChanged+=(s,e)=>{if(customer.Checked){settings.PrintMode="customer";settings.Save();}};
            var dark=new CheckBox{Text="الوضع الداكن",AutoSize=true,Checked=settings.Theme!="light"};dark.CheckedChanged+=(s,e)=>{settings.Theme=dark.Checked?"dark":"light";settings.Save();Ui.Theme(this,!dark.Checked);refreshed();};
            ops.Controls.Add(new GroupBox{Text="خيارات الطباعة",Width=650,Height=130,Controls={Ui.Row(both,customer)}});ops.Controls.Add(Ui.Row(dark));
            var data=Page(tabs,"البيانات");foreach(string table in new[]{"catalog_items","drivers","companies","restaurant_tables"}){string t=table;data.Controls.Add(Ui.Button(t=="catalog_items"?"الأصناف":t=="drivers"?"الطيارين":t=="companies"?"الشركات":"الطاولات",()=>{using(Form f=t=="catalog_items"?(Form)new CatalogForm(store,settings.Theme=="light"):t=="restaurant_tables"?(Form)new TablesForm(store,settings.Theme=="light"):new ContactsForm(store,t,settings.Theme=="light"))f.ShowDialog(this);refreshed();}));}
            var reports=Page(tabs,"التقارير");reports.Controls.Add(Ui.Button("تقارير الوردية",()=>{using(var f=new ShiftReportsForm(store,settings))f.ShowDialog(this);}));reports.Controls.Add(Ui.Button("الورديات السابقة",()=>{using(var f=new PreviousShiftsForm(store,settings))f.ShowDialog(this);}));reports.Controls.Add(Ui.Button("مراجعة إجمالي كل 15 وردية",()=>{using(var f=new ArchiveForm(store,settings))f.ShowDialog(this);}));reports.Controls.Add(Ui.Button("لوحة المبيعات",()=>{using(var f=new SalesDashboardForm(store,settings))f.ShowDialog(this);}));
            var system=Page(tabs,"النظام");system.Controls.Add(Ui.Button("المستخدمون",()=>{using(var f=new UsersForm(store,settings.Theme=="light"))f.ShowDialog(this);}));system.Controls.Add(Ui.Button("سجل النظام",()=>{using(var f=new AuditForm(store,settings))f.ShowDialog(this);}));system.Controls.Add(Ui.Button("الطباعة",()=>PrinterDialog(store,settings)));system.Controls.Add(Ui.Button("تغيير كلمة السر",()=>{using(var f=new UsersForm(store,settings.Theme=="light"))f.ShowDialog(this);}));system.Controls.Add(Ui.Button("بدء وردية جديدة / إعادة ضبط",()=>{if(Dialogs.Show(this,"إنهاء الوردية الحالية وبدء وردية جديدة؟","تأكيد",MessageBoxButtons.YesNo)==DialogResult.Yes){store.StartShift();refreshed();}}));
            system.Controls.Add(Ui.Button("نسخة احتياطية",()=>{using(var save=new SaveFileDialog{Filter="نسخة كاملة (بيانات وإعدادات وصور)|*.zip",FileName="pos-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".zip"})if(save.ShowDialog(this)==DialogResult.OK){BackupService.Create(store.DataRoot,save.FileName);store.Audit("backup","Complete backup created");Dialogs.Show(this,"تم حفظ نسخة كاملة من البيانات والإعدادات والصور");}}));
            system.Controls.Add(Ui.Button("استرجاع نسخة احتياطية",()=>
            {
                if(canRestore!=null&&!canRestore())throw new InvalidOperationException("احفظ أو امسح الأوردر الحالي قبل الاسترجاع");
                using(var picker=new OpenFileDialog{Filter="POS backup|*.zip;*.db",CheckFileExists=true})
                {
                    if(picker.ShowDialog(this)!=DialogResult.OK)return;if(Dialogs.Show(this,"سيتم حفظ نسخة أمان من البيانات الحالية، ثم استرجاع النسخة المحددة وإعادة تشغيل البرنامج. متابعة؟","تأكيد الاسترجاع",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
                    string safety=BackupService.Restore(store.DataRoot,picker.FileName);Dialogs.Show(this,"تم الاسترجاع. نسخة الأمان محفوظة في:\n"+safety+"\nسيُعاد تشغيل البرنامج الآن.");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath,"--restart-parent "+System.Diagnostics.Process.GetCurrentProcess().Id+" --data-root \""+store.DataRoot+"\""){UseShellExecute=false,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden});Application.Exit();
                }
            },"default"));
            system.Controls.Add(Ui.Button("الخزنة",()=>{using(var f=new ExpensesForm(store,settings))f.ShowDialog(this);}));
            system.Controls.Add(Ui.Button("صورة المطعم / الأيقونة",()=>{using(var picker=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.jpeg;*.bmp;*.ico",CheckFileExists=true})if(picker.ShowDialog(this)==DialogResult.OK){Branding.SaveLogo(store.DataRoot,picker.FileName);store.Audit("business_logo","Restaurant image updated");refreshed();}}));
            system.Controls.Add(Ui.Button("بيانات المطعم",()=>{var values=Ui.Input(this,"بيانات المطعم",new Dictionary<string,string>{{"اسم المطعم",settings.BusinessName},{"التليفونات",settings.ReceiptPhones},{"العنوان",settings.ReceiptAddress},{"ختام الإيصال",settings.ReceiptFooter}});if(values==null)return;if(string.IsNullOrWhiteSpace(values["اسم المطعم"]))throw new ArgumentException("اسم المطعم مطلوب");if(values.Values.Any(v=>v.Length>180))throw new ArgumentException("كل حقل يجب ألا يزيد عن 180 حرفًا");settings.BusinessName=values["اسم المطعم"];settings.ReceiptPhones=values["التليفونات"];settings.ReceiptAddress=values["العنوان"];settings.ReceiptFooter=values["ختام الإيصال"];settings.Save();store.Audit("business_settings",settings.BusinessName);refreshed();}));
            system.Controls.Add(Ui.Button("الترخيص",()=>{LicenseGrant license;if(!Licensing.Read(store.DataRoot,out license))throw new InvalidOperationException("لا يوجد ترخيص صالح لهذا الجهاز");Dialogs.Show(this,"Push — ترخيص دائم لجهاز واحد\nالعميل: "+license.Customer+"\nرقم الترخيص: "+license.LicenseId+"\nكود الجهاز:\n"+Licensing.DeviceCode,"الترخيص");}));
            outer.Controls.Add(Ui.Button("إغلاق",Close,"default"),0,2);Controls.Add(outer);Ui.Theme(this,settings.Theme=="light");
        }
        private FlowLayoutPanel Page(TabControl tabs,string name){var p=new TabPage(name);var content=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(16),RightToLeft=RightToLeft.No,FlowDirection=FlowDirection.RightToLeft,WrapContents=true};content.ControlAdded+=(s,e)=>{if(e.Control is Button button){button.AutoSize=false;button.Size=new Size(220,76);button.Margin=new Padding(8);button.MinimumSize=new Size(160,64);}else if(e.Control is GroupBox group){group.Padding=new Padding(12,28,12,12);}};p.Controls.Add(content);tabs.TabPages.Add(p);return content;}
        private void PrinterDialog(Store store,PosSettings settings)
        {using(var form=new PrinterSettingsForm(store,settings))form.ShowDialog(this);}
        public static void ShowReport(Store store,PosSettings settings,DataTable data,string title)
        {
            using(var f=Ui.Form(title,1000,650))
            {
                var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));var grid=Ui.Grid(data);root.Controls.Add(grid,0,0);
                root.Controls.Add(Ui.Row(Ui.Button("طباعة",()=>
                {
                    ReportUi.PrintTable(store,settings,data,title);
                }),Ui.Button("إغلاق",f.Close,"default")),0,1);f.Controls.Add(root);Ui.Theme(f,settings.Theme=="light");f.ShowDialog();
            }
        }
    }
}
