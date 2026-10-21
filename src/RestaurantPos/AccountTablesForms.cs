using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace RestaurantPos
{
    public sealed class UsersForm : Form
    {
        public UsersForm(Store store,bool light)
        {
            Text="المستخدمون";Size=new Size(760,620);MinimumSize=new Size(650,500);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",11,FontStyle.Bold);RightToLeft=RightToLeft.Yes;AutoScaleMode=AutoScaleMode.Dpi;var root=ReportUi.Root();root.RowCount=4;root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var name=new TextBox{Dock=DockStyle.Fill};var pin=new TextBox{Dock=DockStyle.Fill,UseSystemPasswordChar=true};var role=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};role.Items.AddRange(new object[]{"موظف","مدير"});role.SelectedIndex=0;var form=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,RightToLeft=RightToLeft.No};form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,70));form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));Control[] edits={name,pin,role};string[] labels={"اسم المستخدم","الرقم السري (٤ أحرف أو أرقام على الأقل)","الصلاحية"};form=Ui.Fields(labels,edits);root.Controls.Add(form,0,0);
            var grid=Ui.Grid(new DataTable());Action reload=()=>{grid.DataSource=store.Records("users");Ui.Localize(grid);if(grid.Columns.Contains("user_id"))grid.Columns["user_id"].Visible=false;};Func<DataRow> selected=()=>{if(!(grid.CurrentRow?.DataBoundItem is DataRowView row))throw new ArgumentException("اختر مستخدمًا");return row.Row;};
            grid.CellClick+=(s,e)=>{if(e.RowIndex<0)return;var row=selected();name.Text=Convert.ToString(row["name"]);role.SelectedIndex=Convert.ToString(row["role"])=="manager"?1:0;pin.Clear();};
            root.Controls.Add(Ui.Row(Ui.Button("إضافة مستخدم",()=>{store.SaveUser(name.Text.Trim(),pin.Text,role.SelectedIndex==1?"manager":"cashier",true,null);pin.Clear();reload();},"success"),Ui.Button("حفظ تعديلات الحساب",()=>{var row=selected();store.SaveUser(name.Text.Trim(),pin.Text,role.SelectedIndex==1?"manager":"cashier",Convert.ToInt32(row["active"])==1,Convert.ToInt64(row["_rowid"]));pin.Clear();reload();},"default"),Ui.Button("تعيين رقم سري جديد",()=>{var row=selected();if(pin.Text.Length<4)throw new ArgumentException("أدخل الرقم السري الجديد");store.SaveUser(Convert.ToString(row["name"]),pin.Text,Convert.ToString(row["role"]),Convert.ToInt32(row["active"])==1,Convert.ToInt64(row["_rowid"]));pin.Clear();reload();}),Ui.Button("تفعيل / تعطيل",()=>{var row=selected();store.SaveUser(Convert.ToString(row["name"]),"",Convert.ToString(row["role"]),Convert.ToInt32(row["active"])!=1,Convert.ToInt64(row["_rowid"]));reload();},"default"),Ui.Button("حذف مستخدم",()=>{var row=selected();if(Dialogs.Show(this,"حذف المستخدم المحدد؟","تأكيد",MessageBoxButtons.YesNo)==DialogResult.Yes){store.DeleteRecord("users",Convert.ToInt64(row["_rowid"]));reload();}},"danger")),0,1);root.Controls.Add(grid,0,2);root.Controls.Add(Ui.Button("إغلاق",Close,"default"),0,3);Controls.Add(root);reload();Ui.Theme(this,light);
        }
    }
    public sealed class TablesForm : Form
    {
        public int? SelectedTable { get; private set; }
        public TablesForm(Store store,bool light,bool pick=false)
        {
            Text=pick?"اختيار طاولة":"إدارة الطاولات";Size=new Size(780,570);MinimumSize=new Size(650,450);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",11,FontStyle.Bold);AutoScaleMode=AutoScaleMode.Dpi;RightToLeft=RightToLeft.Yes;var root=ReportUi.Root();root.RowCount=4;root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));var tiles=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.RightToLeft};Action reload=null;
            reload=()=>{while(tiles.Controls.Count>0)tiles.Controls[0].Dispose();foreach(DataRow item in store.Records("restaurant_tables").Rows){DataRow row=item;bool free=Convert.ToString(row["status"])=="free";var b=Ui.Button("طاولة "+row["table_no"]+"\n"+row["label"]+"\n"+(free?"فاضية":"مشغولة"),()=>{if(pick){if(!free)throw new ArgumentException("هذه الطاولة مشغولة بالفعل");SelectedTable=Convert.ToInt32(row["table_no"]);DialogResult=DialogResult.OK;}else{store.SaveRecord("restaurant_tables",new Dictionary<string,object>{{"status",free?"occupied":"free"}},Convert.ToInt64(row["_rowid"]));reload();}},free?"success":"danger");b.AutoSize=false;b.Size=new Size(130,100);tiles.Controls.Add(b);}Ui.Theme(tiles,light);};
            var no=new NumericUpDown{Minimum=1,Maximum=999,Width=85};var label=new TextBox{Width=180};if(!pick)root.Controls.Add(Ui.Row(new Label{Text="رقم الطاولة:",AutoSize=true},no,label,Ui.Button("إضافة طاولة",()=>{store.SaveRecord("restaurant_tables",new Dictionary<string,object>{{"table_no",(int)no.Value},{"label",label.Text.Trim()},{"status","free"}},null);label.Clear();reload();},"success"),Ui.Button("حذف طاولة",()=>{var found=store.Records("restaurant_tables").Select("table_no="+(int)no.Value);if(found.Length==0)throw new ArgumentException("الطاولة غير موجودة");if(Dialogs.Show(this,"حذف طاولة "+no.Value+"؟","تأكيد",MessageBoxButtons.YesNo)==DialogResult.Yes){store.DeleteRecord("restaurant_tables",Convert.ToInt64(found[0]["_rowid"]));reload();}},"danger")),0,0);
            root.Controls.Add(new Label{Text=pick?"اضغط على طاولة فاضية (خضراء) لربطها بالأوردر.":"اضغط على الطاولة لتبديل حالتها (فاضية / مشغولة).",AutoSize=true},0,1);root.Controls.Add(tiles,0,2);root.Controls.Add(Ui.Button(pick?"إلغاء":"إغلاق",Close,"default"),0,3);Controls.Add(root);reload();Ui.Theme(this,light);
        }
    }
}
