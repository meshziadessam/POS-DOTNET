using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace RestaurantPos
{
    public sealed class ContactEditorForm : Form
    {
        public ContactEditorForm(Store store,string table,DataRow record,bool light)
        {
            if(!new[]{"customers","drivers","companies"}.Contains(table))throw new ArgumentException(nameof(table));
            Text=record==null?"إضافة "+(table=="customers"?"عميل":table=="drivers"?"طيار":"شركة"):"تعديل البيانات";Size=new Size(590,table=="customers"?650:410);MinimumSize=new Size(480,350);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",11,FontStyle.Bold);RightToLeft=RightToLeft.Yes;AutoScaleMode=AutoScaleMode.Dpi;
            var root=ReportUi.Root();root.RowCount=3;root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var fields=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,RightToLeft=RightToLeft.No};fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,70));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
            var name=new TextBox{Dock=DockStyle.Fill,Text=record==null?"":Convert.ToString(record["name"])};var phone=new TextBox{Dock=DockStyle.Fill,Text=record==null?"":Convert.ToString(record["phone"]),RightToLeft=RightToLeft.No};var notes=new TextBox{Dock=DockStyle.Fill,Multiline=true,Height=70,Text=record==null?"":Convert.ToString(record["notes"])};
            fields.Controls.Add(name,0,0);fields.Controls.Add(new Label{Text="الاسم",AutoSize=true},1,0);fields.Controls.Add(phone,0,1);fields.Controls.Add(new Label{Text="رقم التليفون",AutoSize=true},1,1);fields.Controls.Add(notes,0,2);fields.Controls.Add(new Label{Text="ملاحظات",AutoSize=true},1,2);root.Controls.Add(fields,0,0);
            var addresses=new ListBox{Dock=DockStyle.Fill,IntegralHeight=false};
            if(table=="customers")
            {
                if(record!=null){string value=Convert.ToString(record["address"]);if(value.StartsWith("[")){foreach(string address in new JavaScriptSerializer().Deserialize<string[]>(value))addresses.Items.Add(address);}else if(value!="")addresses.Items.Add(value);}
                var addressPanel=ReportUi.Root();addressPanel.RowCount=3;addressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));addressPanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));addressPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));addressPanel.Controls.Add(new Label{Text="عناوين العميل",AutoSize=true},0,0);addressPanel.Controls.Add(addresses,0,1);
                Action<bool> edit=existing=>{if(existing&&addresses.SelectedIndex<0)throw new ArgumentException("اختر عنوانًا");var result=Ui.Input(this,existing?"تعديل العنوان":"إضافة عنوان",new Dictionary<string,string>{{"العنوان",existing?Convert.ToString(addresses.SelectedItem):""}});if(result==null)return;if(result["العنوان"]=="")throw new ArgumentException("العنوان فارغ");if(existing)addresses.Items[addresses.SelectedIndex]=result["العنوان"];else addresses.Items.Add(result["العنوان"]);};
                addressPanel.Controls.Add(Ui.Row(Ui.Button("إضافة عنوان",()=>edit(false)),Ui.Button("تعديل العنوان",()=>edit(true),"default"),Ui.Button("حذف العنوان",()=>{if(addresses.SelectedIndex>=0)addresses.Items.RemoveAt(addresses.SelectedIndex);},"danger")),0,2);root.Controls.Add(addressPanel,0,1);
            }
            root.Controls.Add(Ui.Row(Ui.Button("حفظ",()=>{var values=new Dictionary<string,object>{{"name",name.Text.Trim()},{"phone",phone.Text.Trim()},{"notes",notes.Text.Trim()}};if(table=="customers"){var items=addresses.Items.Cast<string>().ToList();values["address"]=items.Count==1?items[0]:items.Count==0?"":new JavaScriptSerializer().Serialize(items);}store.SaveRecord(table,values,record==null?(long?)null:Convert.ToInt64(record["_rowid"]));DialogResult=DialogResult.OK;},"success"),Ui.Button("إلغاء",()=>DialogResult=DialogResult.Cancel,"danger")),0,2);Controls.Add(root);Ui.Theme(this,light);
        }
    }
    public sealed class ContactsForm : Form
    {
        public DataRow SelectedRecord { get; private set; }
        public ContactsForm(Store store,string table,bool light,bool pick=false)
        {
            if(!new[]{"customers","drivers","companies"}.Contains(table))throw new ArgumentException(nameof(table));Text=table=="customers"?"إدارة العملاء":table=="drivers"?"الطيارين":"الشركات";Size=new Size(950,650);MinimumSize=new Size(680,500);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",11,FontStyle.Bold);AutoScaleMode=AutoScaleMode.Dpi;RightToLeft=RightToLeft.Yes;
            var root=ReportUi.Root();root.RowCount=3;root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));var search=new TextBox{Width=350};root.Controls.Add(Ui.Row(new Label{Text="بحث بالاسم أو التليفون",AutoSize=true},search),0,0);var grid=Ui.Grid(new DataTable());root.Controls.Add(grid,0,1);
            Action reload=()=>{grid.DataSource=store.SearchContacts(table,search.Text);Ui.Localize(grid);foreach(string key in new[]{"driver_id","company_id"})if(grid.Columns.Contains(key))grid.Columns[key].Visible=false;};search.TextChanged+=(s,e)=>reload();
            Func<DataRow> selected=()=>{if(!(grid.CurrentRow?.DataBoundItem is DataRowView row))throw new ArgumentException("اختر صفًا أولاً");return row.Row;};
            Action choose=()=>{SelectedRecord=selected();DialogResult=DialogResult.OK;};var actions=Ui.Row(Ui.Button("إضافة سريع",()=>{using(var f=new ContactEditorForm(store,table,null,light))if(f.ShowDialog(this)==DialogResult.OK){search.Clear();reload();}},"success"),Ui.Button("تعديل المحدد",()=>{using(var f=new ContactEditorForm(store,table,selected(),light))if(f.ShowDialog(this)==DialogResult.OK)reload();}));
            if(pick)actions.Controls.Add(Ui.Button("تطبيق على الأوردر",choose,"success"));actions.Controls.Add(Ui.Button("إغلاق",Close,"default"));root.Controls.Add(actions,0,2);grid.CellDoubleClick+=(s,e)=>{if(pick&&e.RowIndex>=0)choose();};Controls.Add(root);reload();Ui.Theme(this,light);
        }
    }
    public sealed partial class Store
    {
        public DataTable SearchContacts(string table,string query)
        {
            if(!new[]{"customers","drivers","companies"}.Contains(table))throw new ArgumentException(nameof(table));query=(query??"").Trim();string normalized=NormalizePhone(query);
            return Query("SELECT rowid AS _rowid,* FROM "+table+" WHERE @p0='' OR instr(name,@p0)>0 OR instr(phone,@p0)>0 OR (@p1<>'' AND instr(phone,@p1)>0) ORDER BY name",query,normalized);
        }
        public static string NormalizePhone(string value)
        {
            string phone=System.Text.RegularExpressions.Regex.Replace(value??"",@"\D","");if(!phone.StartsWith("0")&&phone.Length==12&&phone.StartsWith("20"))phone=phone.Substring(2);if(phone.Length==10&&phone.StartsWith("1"))phone="0"+phone;if(phone.Length==11&&phone.StartsWith("1"))phone="0"+phone.Substring(1);return phone;
        }
    }
}
