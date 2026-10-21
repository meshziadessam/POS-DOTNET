using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace RestaurantPos
{
    public sealed class CatalogForm : Form
    {
        private readonly Store store;
        private readonly bool light;
        private long? selected;
        private string storedImage="",pendingImage="",savedName="";
        private readonly TextBox name=new TextBox{Dock=DockStyle.Fill};
        private readonly NumericUpDown price=Money();
        private readonly TextBox barcode=new TextBox{Dock=DockStyle.Fill};
        private readonly ComboBox category=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
        private readonly ComboBox manageCategory=new ComboBox{Width=150,DropDownStyle=ComboBoxStyle.DropDownList};
        private readonly Label station=new Label{AutoSize=true};
        private readonly CheckBox unlimited=new CheckBox{Text="مخزون غير محدود (بدون متابعة)",Checked=true,AutoSize=true};
        private readonly NumericUpDown stock=Money();
        private readonly NumericUpDown threshold=Money();
        private readonly PictureBox image=new PictureBox{Size=new Size(72,72),SizeMode=PictureBoxSizeMode.Zoom,BorderStyle=BorderStyle.FixedSingle};
        private readonly ListBox variants=new ListBox{Width=250,Height=85};
        private readonly TextBox variantName=new TextBox{Width=130};
        private readonly NumericUpDown variantPrice=Money();
        private readonly DataGridView grid=Ui.Grid(new DataTable());
        private DataTable variantRows=new DataTable();
        private static NumericUpDown Money(){return new NumericUpDown{Minimum=0,Maximum=1000000,DecimalPlaces=2,Width=120,Dock=DockStyle.Fill};}
        public CatalogForm(Store store,bool light)
        {
            this.store=store;this.light=light;Text="الأصناف";Size=new Size(1000,830);MinimumSize=new Size(800,650);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",11,FontStyle.Bold);AutoScaleMode=AutoScaleMode.Dpi;RightToLeft=RightToLeft.Yes;
            var root=ReportUi.Root();root.RowCount=3;root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Margin=new Padding(0,0,0,12)};var content=ReportUi.Root();content.Dock=DockStyle.Top;content.AutoSize=true;content.Padding=new Padding(0);scroll.Controls.Add(content);int row=0;
            Action refreshCategories=()=>{string old=category.Text;category.Items.Clear();category.Items.Add("بدون فئة");manageCategory.Items.Clear();foreach(DataRow c in store.Categories().Rows){category.Items.Add(c["name"]);manageCategory.Items.Add(c["name"]);}category.SelectedIndex=category.Items.Contains(old)?category.Items.IndexOf(old):0;if(manageCategory.Items.Count>0)manageCategory.SelectedIndex=0;};
            manageCategory.SelectedIndexChanged+=(s,e)=>{var rows=store.Query("SELECT station FROM categories WHERE name=@p0",manageCategory.Text);station.Text=rows.Rows.Count>0?Convert.ToString(rows.Rows[0][0]):"";};
            Add(content,ref row,Ui.Row(new Label{Text="الفئات الحالية:",AutoSize=true},manageCategory,station,Ui.Button("إضافة فئة",()=>{var fields=Ui.Input(this,"إضافة فئة",new Dictionary<string,string>{{"اسم الفئة",""}});if(fields==null)return;store.SaveRecord("categories",new Dictionary<string,object>{{"name",fields["اسم الفئة"]},{"station",""}},null);refreshCategories();},"success"),Ui.Button("تعيين قسم الطباعة",()=>{if(manageCategory.SelectedIndex<0)throw new ArgumentException("اختر فئة");var result=Ui.Input(this,"قسم الطباعة",new Dictionary<string,string>{{"القسم",station.Text}});if(result==null)return;var current=store.Query("SELECT rowid AS _rowid FROM categories WHERE name=@p0",manageCategory.Text);store.SaveRecord("categories",new Dictionary<string,object>{{"station",result["القسم"]}},Convert.ToInt64(current.Rows[0][0]));refreshCategories();},"default"),Ui.Button("حذف الفئة المحددة",()=>{if(manageCategory.SelectedIndex<0)throw new ArgumentException("اختر فئة");if(Dialogs.Show(this,"حذف الفئة؟ الأصناف ستظل محفوظة بدون فئة.","تأكيد",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;var current=store.Query("SELECT rowid AS _rowid FROM categories WHERE name=@p0",manageCategory.Text);store.DeleteRecord("categories",Convert.ToInt64(current.Rows[0][0]));refreshCategories();ReloadGrid();},"danger")));
            var fieldsPanel=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,RightToLeft=RightToLeft.No};fieldsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,75));fieldsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));Control[] fieldsControls={name,price,category,barcode};string[] labels={"اسم الصنف","السعر الأساسي","الفئة","الباركود"};for(int i=0;i<fieldsControls.Length;i++){fieldsControls[i].RightToLeft=i==3?RightToLeft.No:RightToLeft.Yes;fieldsPanel.Controls.Add(fieldsControls[i],0,i);fieldsPanel.Controls.Add(new Label{Text=labels[i],AutoSize=true,Anchor=AnchorStyles.Right},1,i);}Add(content,ref row,fieldsPanel);
            stock.Dock=threshold.Dock=DockStyle.None;threshold.Value=5;stock.Enabled=threshold.Enabled=false;unlimited.CheckedChanged+=(s,e)=>stock.Enabled=threshold.Enabled=!unlimited.Checked;
            Add(content,ref row,Ui.Row(unlimited,new Label{Text="الكمية الحالية:",AutoSize=true},stock,new Label{Text="حد التنبيه:",AutoSize=true},threshold));
            Add(content,ref row,Ui.Row(image,Ui.Button("اختيار صورة",()=>{using(var picker=new OpenFileDialog{Title="صورة الصنف",Filter="Images|*.png;*.jpg;*.jpeg;*.bmp"})if(picker.ShowDialog(this)==DialogResult.OK){if(new FileInfo(picker.FileName).Length>20*1024*1024)throw new ArgumentException("الصورة كبيرة جدًا؛ اختر صورة أقل من 20 ميجابايت");using(var original=Image.FromFile(picker.FileName)){if(original.Width>12000||original.Height>12000)throw new ArgumentException("أبعاد الصورة كبيرة جدًا");SetImage(new Bitmap(original));}pendingImage=picker.FileName;}},"default"),Ui.Button("إزالة الصورة",()=>{pendingImage="";storedImage="";SetImage(null);},"danger")));
            variantPrice.Dock=DockStyle.None;Add(content,ref row,new Label{Text="تنويعات الصنف (اختياري — صغير / وسط / كبير)",AutoSize=true});Add(content,ref row,Ui.Row(variants,variantName,variantPrice,Ui.Button("إضافة تنويع",()=>{if(!selected.HasValue)throw new ArgumentException("احفظ الصنف أولاً");store.SaveRecord("item_variants",new Dictionary<string,object>{{"item_name",savedName},{"variant_name",variantName.Text.Trim()},{"price",variantPrice.Value}},null);variantName.Clear();LoadVariants();},"success"),Ui.Button("حذف التنويع المحدد",()=>{if(variants.SelectedIndex<0)throw new ArgumentException("اختر تنويعًا");store.DeleteRecord("item_variants",Convert.ToInt64(variantRows.Rows[variants.SelectedIndex]["_rowid"]));LoadVariants();},"danger")));
            root.Controls.Add(scroll,0,0);root.Controls.Add(Ui.Row(Ui.Button("حفظ الصنف",Save,"success"),Ui.Button("صنف جديد",Clear,"default"),Ui.Button("حذف الصنف المحدد",()=>{if(!selected.HasValue)throw new ArgumentException("اختر صنفًا");if(Dialogs.Show(this,"حذف الصنف المحدد؟ لن تتغير الفواتير السابقة.","تأكيد",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;store.DeleteRecord("catalog_items",selected.Value);Clear();ReloadGrid();},"danger"),Ui.Button("خيارات الأصناف",()=>{using(var f=new ManagementForm(store,"item_options",light))f.ShowDialog(this);}),Ui.Button("إغلاق",Close,"default")),0,2);root.Controls.Add(grid,0,1);
            grid.CellClick+=(s,e)=>{if(e.RowIndex<0||!(grid.CurrentRow?.DataBoundItem is DataRowView view))return;var record=view.Row;selected=Convert.ToInt64(record["_rowid"]);name.Text=savedName=Convert.ToString(record["name"]);price.Value=Convert.ToDecimal(record["price"]);string cat=Convert.ToString(record["category"]);category.SelectedIndex=category.Items.Contains(cat)?category.Items.IndexOf(cat):0;barcode.Text=Convert.ToString(record["barcode"]);unlimited.Checked=record["stock_qty"]==DBNull.Value;if(!unlimited.Checked)stock.Value=Math.Max(0,Math.Min(stock.Maximum,Convert.ToDecimal(record["stock_qty"])));threshold.Value=record["low_stock_threshold"]==DBNull.Value?5:Math.Max(0,Math.Min(threshold.Maximum,Convert.ToDecimal(record["low_stock_threshold"])));storedImage=Convert.ToString(record["image_path"]);pendingImage="";string path=Path.Combine(store.DataRoot,"data","item_images",Path.GetFileName(storedImage));if(storedImage!=""&&File.Exists(path))using(var original=Image.FromFile(path))SetImage(new Bitmap(original));else SetImage(null);LoadVariants();};Controls.Add(root);SizeButtons(root);refreshCategories();ReloadGrid();Ui.Theme(this,light);Action arrange=()=>{int desired=content.GetPreferredSize(new Size(Math.Max(1,scroll.ClientSize.Width-SystemInformation.VerticalScrollBarWidth),0)).Height+16;root.RowStyles[0].Height=Math.Min(desired,Math.Max(240,ClientSize.Height-260));root.RowStyles[0].SizeType=SizeType.Absolute;};Shown+=(s,e)=>arrange();Resize+=(s,e)=>arrange();
        }
        private static void SizeButtons(Control parent){foreach(Control child in parent.Controls){if(child is Button button){button.AutoSize=false;button.Size=new Size(Math.Max(90,TextRenderer.MeasureText(button.Text,button.Font).Width+24),64);}SizeButtons(child);}}
        private static void Add(TableLayoutPanel panel,ref int row,Control control){panel.RowCount=row+1;panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));panel.Controls.Add(control,0,row++);}
        private void SetImage(Image value){var old=image.Image;image.Image=value;old?.Dispose();}
        private void ReloadGrid(){grid.DataSource=store.Records("catalog_items");Ui.Localize(grid);foreach(string key in new[]{"catalog_type","sort_order","image_path"})if(grid.Columns.Contains(key))grid.Columns[key].Visible=false;}
        private void LoadVariants(){variantRows=store.Query("SELECT rowid AS _rowid,variant_name,price FROM item_variants WHERE catalog_type='menu' AND item_name=@p0 ORDER BY sort_order",savedName);variants.Items.Clear();foreach(DataRow row in variantRows.Rows)variants.Items.Add(row["variant_name"]+" — "+Convert.ToDecimal(row["price"]).ToString("0.00")+" ج");}
        private void Clear(){selected=null;savedName="";name.Clear();price.Value=0;barcode.Clear();pendingImage=storedImage="";unlimited.Checked=true;stock.Value=0;threshold.Value=5;SetImage(null);variants.Items.Clear();variantName.Clear();}
        private void Save()
        {
            if(name.Text.Trim()=="")throw new ArgumentException("اسم الصنف مطلوب");string target=storedImage;
            if(pendingImage!=""){target=Guid.NewGuid().ToString("N")+".png";string destination=Path.Combine(store.DataRoot,"data","item_images",target);using(var original=Image.FromFile(pendingImage)){double ratio=Math.Min(1,1024d/Math.Max(original.Width,original.Height));using(var scaled=new Bitmap(original,new Size(Math.Max(1,(int)(original.Width*ratio)),Math.Max(1,(int)(original.Height*ratio)))))scaled.Save(destination,System.Drawing.Imaging.ImageFormat.Png);}}
            store.SaveRecord("catalog_items",new Dictionary<string,object>{{"name",name.Text.Trim()},{"price",price.Value},{"category",category.SelectedIndex<=0?"":category.Text},{"barcode",barcode.Text.Trim()},{"stock_qty",unlimited.Checked?null:(object)stock.Value},{"low_stock_threshold",threshold.Value},{"image_path",target}},selected);storedImage=target;pendingImage="";savedName=name.Text.Trim();selected=Convert.ToInt64(store.Query("SELECT rowid FROM catalog_items WHERE catalog_type='menu' AND name=@p0",savedName).Rows[0][0]);ReloadGrid();LoadVariants();
        }
        protected override void Dispose(bool disposing){if(disposing)image.Image?.Dispose();base.Dispose(disposing);}
    }
}
