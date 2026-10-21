using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RestaurantPos
{
    internal static class Ui
    {
        public static readonly Color Gold=ColorTranslator.FromHtml("#d4af37");
        public static Button Button(string text,Action click,string variant="primary")
        {
            var b=new TouchButton{Text=text,AutoSize=true,MinimumSize=new Size(90,64),Margin=new Padding(3),FlatStyle=FlatStyle.Flat,Tag=variant};
            b.Click+=(s,e)=>{try{click();}catch(Exception error){Dialogs.Show(b.FindForm(),error.Message,"تنبيه",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};return b;
        }
        public static FlowLayoutPanel Row(params Control[] controls){var p=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,RightToLeft=RightToLeft.No,Margin=new Padding(0),FlowDirection=FlowDirection.RightToLeft};p.Controls.AddRange(controls);return p;}
        public static Form Form(string title,int width=850,int height=600)
        {var form=new Form{Text=title,Size=new Size(width,height),MinimumSize=new Size(450,Math.Min(300,height)),StartPosition=FormStartPosition.CenterParent,Font=new Font("Segoe UI",11),AutoScaleMode=AutoScaleMode.Dpi,RightToLeft=RightToLeft.Yes};form.Shown+=(s,e)=>Theme(form,form.Owner!=null?IsLight(form.Owner):IsLight(form));return form;}
        public static bool IsLight(Control control){return control!=null&&control.BackColor.GetBrightness()>.6f;}
        public static TableLayoutPanel Fields(string[] labels,Control[] edits)
        {
            var fields=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,RightToLeft=RightToLeft.No,Padding=new Padding(0,4,0,4)};fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,68));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,32));
            for(int i=0;i<labels.Length;i++){fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));edits[i].Dock=DockStyle.Fill;edits[i].Margin=new Padding(0,8,8,8);edits[i].MinimumSize=new Size(0,30);fields.Controls.Add(edits[i],0,i);fields.Controls.Add(new Label{Text=labels[i],AutoSize=true,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,RightToLeft=RightToLeft.Yes,Margin=new Padding(8)},1,i);}return fields;
        }
        public static void Theme(Control control,bool light)
        {
            control.BackColor=ColorTranslator.FromHtml(light?"#f5f1e8":"#121212");control.ForeColor=light?Color.FromArgb(29,29,29):Color.WhiteSmoke;
            foreach(Control child in control.Controls)Theme(child,light);
            if(control is Button){string kind=Convert.ToString(control.Tag);control.BackColor=kind=="danger"?ColorTranslator.FromHtml("#b71c1c"):kind=="success"?ColorTranslator.FromHtml("#2e7d32"):kind=="default"?ColorTranslator.FromHtml(light?"#ebe3d4":"#242424"):Gold;control.ForeColor=kind=="danger"||kind=="success"?Color.WhiteSmoke:kind=="default"?(light?Color.FromArgb(29,29,29):Color.WhiteSmoke):Color.FromArgb(17,17,17);}
            if(control is DataGridView grid){grid.BackgroundColor=control.BackColor;grid.DefaultCellStyle.BackColor=control.BackColor;grid.DefaultCellStyle.ForeColor=control.ForeColor;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(26,77,109);grid.DefaultCellStyle.SelectionForeColor=Color.White;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(50,50,50);grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;}
            if(control is ComboBox combo)combo.FlatStyle=FlatStyle.Flat;
            if(control is TextBoxBase text){text.BackColor=ColorTranslator.FromHtml(light?"#ffffff":"#1d1d1d");text.ForeColor=light?Color.FromArgb(25,25,25):Color.WhiteSmoke;}
            if(control is ListBox list){list.BackColor=ColorTranslator.FromHtml(light?"#ffffff":"#1d1d1d");list.ForeColor=control.ForeColor;}
            if(control is NumericUpDown number){number.BackColor=ColorTranslator.FromHtml(light?"#ffffff":"#1d1d1d");number.ForeColor=control.ForeColor;}
            if(control is ComboBox select)
            {
                select.BackColor=ColorTranslator.FromHtml(light?"#ffffff":"#242424");select.ForeColor=control.ForeColor;
                if(select.DrawMode!=DrawMode.OwnerDrawFixed){select.DrawMode=DrawMode.OwnerDrawFixed;select.ItemHeight=Math.Max(28,select.Font.Height+8);select.DrawItem+=(sender,args)=>{var box=(ComboBox)sender;bool isLight=IsLight(box.FindForm()),selected=(args.State&DrawItemState.Selected)!=0;Color bg=selected?Gold:ColorTranslator.FromHtml(isLight?"#ffffff":"#242424"),fg=selected||isLight?Color.FromArgb(20,20,20):Color.WhiteSmoke;using(var fill=new SolidBrush(bg))args.Graphics.FillRectangle(fill,args.Bounds);string itemCaption=args.Index>=0?box.GetItemText(box.Items[args.Index]):box.Text;TextRenderer.DrawText(args.Graphics,itemCaption,box.Font,args.Bounds,fg,TextFormatFlags.VerticalCenter|TextFormatFlags.RightToLeft|TextFormatFlags.EndEllipsis);};}
            }
            if(control is DataGridView table){table.ColumnHeadersDefaultCellStyle.SelectionBackColor=Color.FromArgb(50,50,50);table.ColumnHeadersDefaultCellStyle.SelectionForeColor=Color.White;}
            if(control is TabControl tabs)tabs.Invalidate();
        }
        public static DataGridView Grid(DataTable table)
        {
            var g=new DataGridView{Dock=DockStyle.Fill,DataSource=table,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders};
            g.RowTemplate.Height=42;g.DefaultCellStyle.Padding=new Padding(4);g.ColumnHeadersHeight=56;g.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;g.ColumnHeadersDefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;
            g.CellFormatting+=(s,e)=>{string key=g.Columns[e.ColumnIndex].DataPropertyName;if(new[]{"created_at","shift_start_at","start_at","end_at"}.Contains(key)&&DateTime.TryParse(Convert.ToString(e.Value),out DateTime date)){e.Value=date.ToString("dd/MM/yyyy\nHH:mm");e.FormattingApplied=true;}if(key=="role"){e.Value=Convert.ToString(e.Value)=="manager"?"مدير":"موظف";e.FormattingApplied=true;}};
            g.DataBindingComplete+=(s,e)=>Localize(g);Localize(g);return g;
        }
        private static readonly Dictionary<string,string> Captions=new Dictionary<string,string>{{"order_id","رقم الأوردر"},{"shift_id","رقم الوردية"},{"shift_start_at","بداية الوردية"},{"created_at","وقت الإنشاء"},{"order_type","نوع الطلب"},{"customer_name","العميل"},{"customer_phone","هاتف العميل"},{"customer_address","العنوان"},{"customer_notes","ملاحظات العميل"},{"driver_id","رقم الطيار"},{"driver_name","الطيار"},{"driver_phone","هاتف الطيار"},{"total","الإجمالي"},{"paid","المدفوع"},{"payment_method","طريقة الدفع"},{"status","الحالة"},{"cashier_name","الكاشير"},{"table_no","رقم الطاولة"},{"name","الاسم"},{"phone","الهاتف"},{"address","العناوين"},{"notes","ملاحظات"},{"price","السعر"},{"category","الفئة"},{"barcode","الباركود"},{"stock_qty","المخزون"},{"low_stock_threshold","حد التنبيه"},{"image_path","الصورة"},{"variant_name","التنويع"},{"item_name","اسم الصنف"},{"option","الخيار"},{"label","الاسم"},{"role","الصلاحية"},{"active","مفعّل"},{"when","الوقت"},{"action","العملية"},{"detail","التفاصيل"},{"who","المستلم"},{"amount_egp","المبلغ"},{"note","ملاحظات"},{"item","الصنف"},{"qty","الكمية"},{"line_total","إجمالي الصنف"},{"orders","عدد الأوردرات"},{"orders_count","عدد الأوردرات"},{"items_count","عدد الأصناف"},{"delivery_fees","دليفري للسائق"},{"delivery_fee_egp","قيمة الدليفري"},{"delivery_fee_total","دليفري للسائق"},{"discounts","الخصومات"},{"discounts_total","الخصومات"},{"discount_egp","خصم الأوردر"},{"gross_sales","قبل الخصم"},{"net_after_discount","صافي المطعم"},{"vault_total","الخزنة"},{"net_after_vault","بعد الخزنة"},{"start_at","بداية الوردية"},{"end_at","نهاية الوردية"},{"items","الأصناف"},{"date","التاريخ"},{"company_name","الشركة"},{"order_notes","ملاحظات الأوردر"},{"station","قسم الطباعة"}};
        public static string Caption(string name){return Captions.ContainsKey(name)?Captions[name]:name;}
        public static void Localize(DataGridView grid){foreach(DataGridViewColumn column in grid.Columns){column.HeaderText=Caption(column.DataPropertyName);if(column.DataPropertyName=="_rowid")column.Visible=false;if(new[]{"created_at","shift_start_at","start_at","end_at"}.Contains(column.DataPropertyName)){column.MinimumWidth=130;column.FillWeight=160;column.DefaultCellStyle.WrapMode=DataGridViewTriState.True;}if(column.ValueType==typeof(decimal)||column.ValueType==typeof(double))column.DefaultCellStyle.Format="N2";}}
        public static Dictionary<string,string> Input(IWin32Window owner,string title,Dictionary<string,string> fields,params string[] passwords)
        {
            using(var f=Form(title,570,Math.Min(720,170+fields.Count*65)))
            {
                var outer=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Padding=new Padding(12)};outer.RowStyles.Add(new RowStyle(SizeType.Percent,100));outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var edits=new Dictionary<string,TextBox>();foreach(var field in fields)edits[field.Key]=new TextBox{Text=field.Value??"",UseSystemPasswordChar=passwords.Contains(field.Key)};
                var form=Fields(fields.Keys.ToArray(),edits.Values.Cast<Control>().ToArray());var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};scroll.Controls.Add(form);
                var ok=Button("حفظ",()=>f.DialogResult=DialogResult.OK,"success");outer.Controls.Add(scroll,0,0);outer.Controls.Add(Row(ok,Button("إلغاء",()=>f.DialogResult=DialogResult.Cancel,"danger")),0,1);f.Controls.Add(outer);f.AcceptButton=ok;
                if(f.ShowDialog(owner)!=DialogResult.OK)return null;return edits.ToDictionary(k=>k.Key,k=>passwords.Contains(k.Key)?k.Value.Text:k.Value.Text.Trim());
            }
        }
        public static decimal? Amount(IWin32Window owner,string title,decimal value)
        {
            using(var f=Form(title,470,270))
            {
                var n=new NumericUpDown{DecimalPlaces=2,Minimum=0,Maximum=1000000,Value=Math.Min(1000000,Math.Max(0,value)),Dock=DockStyle.Top,Font=new Font("Segoe UI",20)};
                var p=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),RowCount=2,ColumnCount=1};p.Controls.Add(n,0,0);p.Controls.Add(Row(Button("تأكيد",()=>f.DialogResult=DialogResult.OK,"success"),Button("إلغاء",()=>f.DialogResult=DialogResult.Cancel,"danger")),0,1);f.Controls.Add(p);
                return f.ShowDialog(owner)==DialogResult.OK?(decimal?)n.Value:null;
            }
        }
        public static DataRow Pick(IWin32Window owner,string title,DataTable table,Action create=null)
        {
            using(var f=Form(title))
            {
                DataRow selected=null;var g=Grid(table);var p=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};p.RowStyles.Add(new RowStyle(SizeType.Percent,100));p.RowStyles.Add(new RowStyle(SizeType.AutoSize));p.Controls.Add(g,0,0);
                Action choose=()=>{if(g.CurrentRow?.DataBoundItem is DataRowView row){selected=row.Row;f.DialogResult=DialogResult.OK;}};
                var rowButtons=Row(Button("اختيار",choose,"success"),Button("إغلاق",()=>f.Close(),"default"));
                if(create!=null)rowButtons.Controls.Add(Button("إضافة جديد",()=>{create();f.DialogResult=DialogResult.Retry;}));
                p.Controls.Add(rowButtons,0,1);g.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)choose();};f.Controls.Add(p);
                return f.ShowDialog(owner)==DialogResult.OK?selected:null;
            }
        }
    }

    internal sealed class TouchButton : Button
    {
        public TouchButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if(Width<22||Height<22)return;
            using(var path=new System.Drawing.Drawing2D.GraphicsPath())
            {
                const int d=20;path.AddArc(0,0,d,d,180,90);path.AddArc(Width-d,0,d,d,270,90);path.AddArc(Width-d,Height-d,d,d,0,90);path.AddArc(0,Height-d,d,d,90,90);path.CloseFigure();
                var previous=Region;Region=new Region(path);previous?.Dispose();
            }
            Parent?.Invalidate(Bounds,true);
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Rounded corners belong to the parent, not to the button's fill.
            e.Graphics.Clear(Parent==null?SystemColors.Control:Parent.BackColor);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent==null?SystemColors.Control:Parent.BackColor);
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect=new Rectangle(1,1,Math.Max(1,Width-3),Math.Max(1,Height-3));const int radius=20;
            using(var path=new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddArc(rect.Left,rect.Top,radius,radius,180,90);path.AddArc(rect.Right-radius,rect.Top,radius,radius,270,90);path.AddArc(rect.Right-radius,rect.Bottom-radius,radius,radius,0,90);path.AddArc(rect.Left,rect.Bottom-radius,radius,radius,90,90);path.CloseFigure();
                using(var brush=new SolidBrush(BackColor))e.Graphics.FillPath(brush,path);
                using(var pen=new Pen(FlatAppearance.BorderColor.IsEmpty?BackColor:FlatAppearance.BorderColor,Math.Max(1,FlatAppearance.BorderSize)))e.Graphics.DrawPath(pen,path);
            }
            var textRect=new Rectangle(6,5,Width-12,Height-10);
            if(Image!=null)
            {
                int y=Math.Max(8,(Height-Image.Height-Font.Height*2)/2);e.Graphics.DrawImage(Image,(Width-Image.Width)/2,y,Image.Width,Image.Height);textRect=new Rectangle(6,y+Image.Height+5,Width-12,Math.Max(20,Height-y-Image.Height-12));
            }
            TextRenderer.DrawText(e.Graphics,Text,Font,textRect,Enabled?ForeColor:Color.Gray,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-6,-6));
        }
    }
}

