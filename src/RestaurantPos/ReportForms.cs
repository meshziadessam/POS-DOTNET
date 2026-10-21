using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RestaurantPos
{
    public sealed class ShiftReportsForm : Form
    {
        public ShiftReportsForm(Store store,PosSettings settings,long? shift=null)
        {
            long sid=shift??store.ActiveShift;var reports=new ReportService(store);Text="تقارير الوردية";Size=new Size(980,680);MinimumSize=new Size(760,560);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",11,FontStyle.Bold);AutoScaleMode=AutoScaleMode.Dpi;RightToLeft=RightToLeft.Yes;
            var root=ReportUi.Root();root.RowCount=7;root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,50));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,50));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(new Label{Text="الوردية رقم "+sid,AutoSize=true,Font=new Font(Font.FontFamily,17,FontStyle.Bold)},0,0);
            var summary=new Label{Text=reports.Summary(sid).ToString(),AutoSize=true,MaximumSize=new Size(900,0)};root.SizeChanged+=(s,e)=>summary.MaximumSize=new Size(Math.Max(300,root.ClientSize.Width-24),0);root.Controls.Add(summary,0,1);
            root.Controls.Add(new Label{Text="حسب الصنف",AutoSize=true},0,2);root.Controls.Add(Ui.Grid(reports.ByItem(sid)),0,3);root.Controls.Add(new Label{Text="حسب نوع الطلب",AutoSize=true},0,4);root.Controls.Add(Ui.Grid(reports.ByType(sid)),0,5);
            var include=new CheckBox{Text="طباعة الأوردرات داخل التقرير",AutoSize=true,Checked=true};root.Controls.Add(Ui.Row(include,Ui.Button("طباعة تقرير الوردية",()=>{new ReceiptPrinter(settings,store.DataRoot).PrintShift(reports,sid,include.Checked);store.Audit("shift_report_print","shift="+sid);}),Ui.Button("طباعة إيصالات الطيارين",()=>{var rows=reports.ByDriver(sid);if(rows.Rows.Count==0)throw new ArgumentException("لا توجد أوردرات دليفري بطيارين في الوردية");foreach(DataRow row in rows.Rows)new ReceiptPrinter(settings,store.DataRoot).PrintText(reports.DriverLines(sid,Convert.ToInt64(row["driver_id"])),"إيصال طيار");store.Audit("driver_report_print","shift="+sid);},"success"),Ui.Button("إغلاق",Close,"default")),0,6);Controls.Add(root);Ui.Theme(this,settings.Theme=="light");
        }
    }
    internal static class ReportUi
    {
        public static TableLayoutPanel Root(){var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,Padding=new Padding(12)};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));return root;}
        public static void PrintTable(Store store,PosSettings settings,DataTable data,string title)
        {var lines=new System.Collections.Generic.List<string>{title};foreach(DataRow row in data.Rows){lines.Add("────────────────────");foreach(DataColumn column in data.Columns)if(column.ColumnName!="_rowid")lines.Add(Ui.Caption(column.ColumnName)+": "+Convert.ToString(row[column]));}new ReceiptPrinter(settings,store.DataRoot).PrintText(lines,title);}
    }
    public sealed class PreviousShiftsForm : Form
    {
        public PreviousShiftsForm(Store store,PosSettings settings)
        {
            Text="الورديات السابقة";Size=new Size(1120,700);MinimumSize=new Size(850,550);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",11,FontStyle.Bold);AutoScaleMode=AutoScaleMode.Dpi;RightToLeft=RightToLeft.Yes;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(12)};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,66));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,34));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var list=new ListBox{Dock=DockStyle.Fill,IntegralHeight=false};var shifts=store.Shifts();foreach(DataRow row in shifts.Rows)list.Items.Add("وردية #"+row["shift_id"]+" | "+row["start_at"]+" | "+(Convert.ToString(row["status"])=="open"?"مستمرة":Convert.ToString(row["end_at"])));
            var detail=ReportUi.Root();detail.RowCount=3;detail.RowStyles.Add(new RowStyle(SizeType.AutoSize));detail.RowStyles.Add(new RowStyle(SizeType.Percent,100));detail.RowStyles.Add(new RowStyle(SizeType.AutoSize));var summary=new Label{AutoSize=true,MaximumSize=new Size(650,0)};var grid=Ui.Grid(new DataTable());long selected=0;var reports=new ReportService(store);detail.Controls.Add(summary,0,0);detail.Controls.Add(grid,0,1);
            list.SelectedIndexChanged+=(s,e)=>{if(list.SelectedIndex<0)return;selected=Convert.ToInt64(shifts.Rows[list.SelectedIndex]["shift_id"]);summary.Text="وردية #"+selected+"\n"+reports.Summary(selected);grid.DataSource=reports.Brief(selected);Ui.Localize(grid);};
            var include=new CheckBox{Text="طباعة الأوردرات داخل التقرير",Checked=true,AutoSize=true};detail.Controls.Add(Ui.Row(include,Ui.Button("طباعة تقرير الوردية",()=>{if(selected==0)throw new ArgumentException("اختر وردية أولاً");new ReceiptPrinter(settings,store.DataRoot).PrintShift(reports,selected,include.Checked);}),Ui.Button("إغلاق",Close,"default")),0,2);root.Controls.Add(detail,0,0);root.Controls.Add(list,1,0);Controls.Add(root);Ui.Theme(this,settings.Theme=="light");if(list.Items.Count>0)list.SelectedIndex=0;
        }
    }
    public sealed class SalesDashboardForm : Form
    {
        public SalesDashboardForm(Store store,PosSettings settings)
        {
            Text="لوحة المبيعات";Size=new Size(860,680);MinimumSize=new Size(650,550);Font=new Font("Segoe UI",11,FontStyle.Bold);StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.Dpi;RightToLeft=RightToLeft.Yes;
            var root=ReportUi.Root();root.RowCount=7;root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,60));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,40));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(new Label{Text="لوحة المبيعات",AutoSize=true,Font=new Font(Font.FontFamily,18,FontStyle.Bold)},0,0);var range=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=180,DataSource=new[]{"آخر 7 أيام","آخر 14 يومًا","آخر 30 يومًا"}};var chart=new SalesChart{Dock=DockStyle.Fill,MinimumSize=new Size(0,180)};var grid=Ui.Grid(new DataTable());var reports=new ReportService(store);
            Action refresh=()=>{int days=new[]{7,14,30}[Math.Max(0,range.SelectedIndex)];chart.Data=reports.SalesByDay(days);grid.DataSource=reports.SalesByCategory(days);Ui.Localize(grid);chart.Invalidate();};range.SelectedIndexChanged+=(s,e)=>refresh();root.Controls.Add(Ui.Row(new Label{Text="الفترة:",AutoSize=true},range,Ui.Button("تحديث",refresh)),0,1);
            root.Controls.Add(new Label{Text="المبيعات اليومية (صافي بعد الخصومات، بدون رسوم الدليفري)",AutoSize=true},0,2);root.Controls.Add(chart,0,3);root.Controls.Add(new Label{Text="المبيعات حسب الفئة",AutoSize=true},0,4);root.Controls.Add(grid,0,5);root.Controls.Add(Ui.Button("إغلاق",Close,"default"),0,6);Controls.Add(root);Ui.Theme(this,settings.Theme=="light");refresh();
        }
    }
    internal sealed class SalesChart : Control
    {
        public DataTable Data { get; set; }
        public SalesChart(){DoubleBuffered=true;}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);if(Data==null||Data.Rows.Count==0)return;float width=ClientSize.Width-75,height=ClientSize.Height-60;if(width<20||height<20)return;
            decimal maximum=Data.Rows.Cast<DataRow>().Max(r=>Convert.ToDecimal(r["total"]));if(maximum<=0)maximum=1;float step=width/Data.Rows.Count;using(var pen=new Pen(Color.Gray))using(var brush=new SolidBrush(Ui.Gold))using(var text=new SolidBrush(ForeColor))using(var font=new Font(Font.FontFamily,8))
            {e.Graphics.DrawLine(pen,55,15,55,15+height);e.Graphics.DrawLine(pen,55,15+height,55+width,15+height);for(int i=0;i<Data.Rows.Count;i++){decimal value=Convert.ToDecimal(Data.Rows[i]["total"]);float h=(float)(value/maximum)*height;float x=55+i*step+step*.15f;e.Graphics.FillRectangle(brush,x,15+height-h,Math.Max(2,step*.7f),h);if(Data.Rows.Count<=14||i%3==0)e.Graphics.DrawString(Convert.ToString(Data.Rows[i]["date"]).Substring(5),font,text,x,18+height);if(Data.Rows.Count<=14)e.Graphics.DrawString(value.ToString("0"),font,text,x,Math.Max(0,2+height-h));}}
        }
    }
    public sealed class ArchiveForm : Form
    {
        public ArchiveForm(Store store,PosSettings settings)
        {
            Text="مراجعة إجمالي كل 15 وردية";Size=new Size(1100,680);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",11,FontStyle.Bold);AutoScaleMode=AutoScaleMode.Dpi;var root=ReportUi.Root();root.RowCount=2;root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));var select=new ComboBox{Width=230,DropDownStyle=ComboBoxStyle.DropDownList};var grid=Ui.Grid(new DataTable());
            Action load=()=>{select.Items.Clear();foreach(DataRow row in store.Query("SELECT DISTINCT archive_key FROM shift_batch_archives ORDER BY archive_key DESC").Rows)select.Items.Add(row[0]);if(select.Items.Count>0)select.SelectedIndex=0;};
            select.SelectedIndexChanged+=(s,e)=>{var data=store.Query("SELECT shift_id,start_at,end_at,orders_count,items_count,gross_sales,discounts_total,delivery_fee_total,net_after_discount,vault_total,net_after_vault FROM shift_batch_archives WHERE archive_key=@p0 ORDER BY CAST(shift_id AS INTEGER)",select.Text);if(data.Rows.Count>0){var total=data.NewRow();total["shift_id"]="الإجمالي";foreach(DataColumn column in data.Columns)if(column.ColumnName!="shift_id"&&column.ColumnName!="start_at"&&column.ColumnName!="end_at")total[column]=data.Rows.Cast<DataRow>().Sum(r=>r[column]==DBNull.Value?0:Convert.ToDecimal(r[column]));data.Rows.Add(total);}grid.DataSource=data;Ui.Localize(grid);};
            root.Controls.Add(Ui.Row(select,Ui.Button("تحديث الأرشيف",()=>{store.UpdateArchives();load();}),Ui.Button("إغلاق",Close,"default")),0,0);root.Controls.Add(grid,0,1);Controls.Add(root);Ui.Theme(this,settings.Theme=="light");load();
        }
    }
}
