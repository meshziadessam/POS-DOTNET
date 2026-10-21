using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace RestaurantPos
{
    public sealed class PrinterSettingsForm : Form
    {
        public PrinterSettingsForm(Store store,PosSettings settings)
        {
            Text="إعدادات الطابعة الحرارية";Size=new Size(680,570);MinimumSize=new Size(580,480);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",11,FontStyle.Bold);AutoScaleMode=AutoScaleMode.Dpi;RightToLeft=RightToLeft.Yes;
            var root=ReportUi.Root();root.RowCount=2;root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));var form=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,RightToLeft=RightToLeft.No};form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,72));form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28));
            var printers=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};foreach(string printer in PrinterSettings.InstalledPrinters)printers.Items.Add(printer);printers.SelectedItem=printers.Items.Contains(settings.PrinterName)?settings.PrinterName:new PrinterSettings().PrinterName;
            var paper=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};paper.Items.AddRange(new object[]{58,80});paper.SelectedItem=settings.PaperWidth;
            var transport=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};transport.Items.AddRange(new object[]{"RAW / ESC-POS (مثل النسخة الأصلية)","تعريف Windows (GDI)"});transport.SelectedIndex=settings.PrinterTransport=="windows"?1:0;
            var cut=new CheckBox{Text="قص الورق بعد كل إيصال (RAW)",Checked=settings.CutPaper,AutoSize=true};Control[] fields={printers,paper,transport,cut};string[] names={"اسم الطابعة في Windows","عرض الورق (مم)","طريقة الإرسال","القص"};form=Ui.Fields(names,fields);
            var content=new Panel{Dock=DockStyle.Fill,AutoScroll=true};content.Controls.Add(form);root.Controls.Add(content,0,0);Action save=()=>{if(printers.SelectedIndex<0)throw new ArgumentException("اختر طابعة Windows مثبتة");settings.PrinterName=printers.Text;settings.PaperWidth=Convert.ToInt32(paper.SelectedItem);settings.PrinterTransport=transport.SelectedIndex==0?"escpos":"windows";settings.CutPaper=cut.Checked;settings.Save();store.Audit("printer_settings",settings.PrinterName+" / "+settings.PrinterTransport);};
            var buttons=Ui.Row(Ui.Button("حفظ",()=>{save();Close();},"success"),Ui.Button("طباعة اختبار",()=>{save();new ReceiptPrinter(settings,store.DataRoot).PrintTest();Dialogs.Show(this,"تم إرسال إيصال اختبار واضح للطابعة. راجع الإخراج المطبوع.");}),Ui.Button("اختيار شعار الإيصال",()=>{using(var picker=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.jpeg;*.bmp"})if(picker.ShowDialog(this)==DialogResult.OK){string target=Path.Combine(store.DataRoot,"data","restaurant_logo.png");string temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";using(var logo=Image.FromFile(picker.FileName)){double ratio=Math.Min(1,1024d/Math.Max(logo.Width,logo.Height));using(var scaled=new Bitmap(logo,new Size(Math.Max(1,(int)(logo.Width*ratio)),Math.Max(1,(int)(logo.Height*ratio)))))scaled.Save(temp,System.Drawing.Imaging.ImageFormat.Png);}if(File.Exists(target))File.Replace(temp,target,target+".bak");else File.Move(temp,target);}} ,"default"),Ui.Button("إغلاق",Close,"default"));root.Controls.Add(buttons,0,1);Controls.Add(root);Ui.Theme(this,settings.Theme=="light");
        }
    }
    public sealed class ExpensesForm : Form
    {
        public ExpensesForm(Store store,PosSettings settings)
        {
            Text="الخزنة — مصروفات";Size=new Size(900,600);MinimumSize=new Size(650,450);Font=new Font("Segoe UI",11,FontStyle.Bold);StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.Dpi;RightToLeft=RightToLeft.Yes;var root=ReportUi.Root();root.RowCount=4;root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var who=new TextBox{Dock=DockStyle.Fill};var note=new TextBox{Dock=DockStyle.Fill};var amount=new NumericUpDown{Dock=DockStyle.Fill,Minimum=0,Maximum=1000000,DecimalPlaces=2,Increment=5};var fields=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2};fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,75));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));Control[] edits={who,amount,note};string[] labels={"مين","كام (جنيه)","ملاحظة"};for(int i=0;i<3;i++){fields.Controls.Add(edits[i],0,i);fields.Controls.Add(new Label{Text=labels[i],AutoSize=true},1,i);}root.Controls.Add(fields,0,0);var grid=Ui.Grid(new DataTable());var total=new Label{AutoSize=true};Action refresh=()=>{var data=store.Query("SELECT who,\"when\",amount_egp,note FROM expenses WHERE shift_id=@p0 ORDER BY \"when\" DESC",store.ActiveShift);grid.DataSource=data;Ui.Localize(grid);total.Text="إجمالي خزنة الوردية #"+store.ActiveShift+": "+data.Rows.Cast<DataRow>().Sum(r=>Convert.ToDecimal(r["amount_egp"])).ToString("0.00")+" ج";};
            root.Controls.Add(Ui.Row(Ui.Button("تسجيل مصروف",()=>{if(who.Text.Trim()==""||amount.Value<=0)throw new ArgumentException("المستلم مطلوب والمبلغ يجب أن يكون أكبر من صفر");store.SaveRecord("expenses",new Dictionary<string,object>{{"who",who.Text.Trim()},{"amount_egp",amount.Value},{"note",note.Text.Trim()}},null);refresh();},"success"),Ui.Button("تحديث",refresh),Ui.Button("إغلاق",Close,"default")),0,1);root.Controls.Add(grid,0,2);root.Controls.Add(total,0,3);Controls.Add(root);refresh();Ui.Theme(this,settings.Theme=="light");
        }
    }
    public sealed class AuditForm : Form
    {
        public AuditForm(Store store,PosSettings settings)
        {
            Text="سجل التدقيق";Size=new Size(900,600);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",11);AutoScaleMode=AutoScaleMode.Dpi;var root=ReportUi.Root();root.RowCount=2;root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));var text=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false};var lines=new List<string>();string log=Path.Combine(store.DataRoot,"logs","application.log");if(File.Exists(log)){lines.Add("=== سجل البرنامج ===");lines.AddRange(File.ReadLines(log).Reverse().Take(500).Reverse());}lines.Add("=== سجل قاعدة البيانات ===");foreach(DataRow row in store.Query("SELECT \"when\",action,detail FROM (SELECT rowid AS id,* FROM audit ORDER BY rowid DESC LIMIT 500) ORDER BY id").Rows)lines.Add(string.Join(" | ",row.ItemArray.Select(Convert.ToString)));text.Text=string.Join(Environment.NewLine,lines);root.Controls.Add(text,0,0);root.Controls.Add(Ui.Button("إغلاق",Close,"default"),0,1);Controls.Add(root);Ui.Theme(this,settings.Theme=="light");
        }
    }
}
