using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace RestaurantPos
{
    public sealed class PosForm : Form
    {
        private readonly Store store;
        private readonly PosSettings settings;
        private readonly string cashier;
        private readonly UserSession session;
        private Cart cart=new Cart();
        private readonly OrderGrid lines=new OrderGrid();
        private readonly ProductPanel products=new ProductPanel();
        private readonly Panel categories=new Panel{Dock=DockStyle.Fill,AutoScroll=true,RightToLeft=RightToLeft.No,Margin=Padding.Empty};
        private readonly Label modeLabel=new Label{AutoSize=true,Font=new Font("Segoe UI",15,FontStyle.Bold)};
        private readonly Label shiftLabel=new Label{AutoSize=true};
        private readonly Label customerLabel=new Label{AutoSize=true};
        private readonly Label notesLabel=new Label{AutoSize=true};
        private readonly Label totalLabel=new Label{AutoSize=true,Font=new Font("Segoe UI",16,FontStyle.Bold)};
        private readonly TextBox barcode=new TextBox{Dock=DockStyle.Fill,Font=new Font("Segoe UI",14)};
        private readonly Button waitingButton;
        private readonly PictureBox businessLogo=new PictureBox{Name="BusinessLogo",Size=new Size(48,48),SizeMode=PictureBoxSizeMode.Zoom,Margin=new Padding(8,0,0,0),Visible=false};
        private readonly Label credit=new Label{Text="Powerd by Ziad Essam",AutoSize=false,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.Gray,Font=new Font("Segoe UI",9),RightToLeft=RightToLeft.No};
        private FlowLayoutPanel actionRow,quantityRow,doneRow,serviceRow;
        private Button deliveryFeeButton,deliveryCustomerButton,deliveryDriverButton,parkButton;
        private string category="";
        private bool busy;
        private readonly List<Button> modeButtons=new List<Button>();
        public PosForm(Store store,string cashier):this(store,new UserSession(cashier,"cashier")){}
        public PosForm(Store store,UserSession session)
        {
            this.store=store;this.session=session??throw new UnauthorizedAccessException("سجّل الدخول أولاً");this.cashier=session.Name;settings=new PosSettings(store.DataRoot);
            Text="Push";Size=new Size(1366,768);MinimumSize=new Size(1024,720);FormBorderStyle=FormBorderStyle.None;WindowState=FormWindowState.Maximized;StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",11,FontStyle.Bold);AutoScaleMode=AutoScaleMode.Dpi;
            KeyPreview=true;KeyDown+=(s,e)=>{if(e.KeyCode==Keys.F11){bool full=FormBorderStyle==FormBorderStyle.None;WindowState=FormWindowState.Normal;FormBorderStyle=full?FormBorderStyle.Sizable:FormBorderStyle.None;if(!full)WindowState=FormWindowState.Maximized;e.Handled=true;}};
            Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=2};root.RowStyles.Add(new RowStyle(SizeType.Absolute,64));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            waitingButton=Ui.Button("انتظار",()=>History(true));
            var header=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=false,ColumnCount=2,RowCount=1};header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var headerButtons=Ui.Row(Ui.Button("خروج",Close,"danger"),Ui.Button("الطلبات السابقة",()=>History(false)),Ui.Button("إدارة العملاء",()=>{using(var f=new ContactsForm(store,"customers",settings.Theme=="light"))f.ShowDialog(this);}),waitingButton);if(session.IsManager)headerButtons.Controls.Add(Ui.Button("إعدادات",OpenSettings));foreach(Control b in headerButtons.Controls)b.MinimumSize=new Size(90,50);
            var heading=new FlowLayoutPanel{AutoSize=true,WrapContents=false,FlowDirection=FlowDirection.RightToLeft,Dock=DockStyle.Fill,Margin=Padding.Empty};heading.Controls.Add(businessLogo);heading.Controls.Add(new Label{Text="شاشة الكاشير",AutoSize=true,Font=new Font("Segoe UI",20,FontStyle.Bold),Margin=new Padding(6,8,6,8)});
            header.Controls.Add(headerButtons,0,0);header.Controls.Add(heading,1,0);
            headerButtons.AutoSize=false;headerButtons.WrapContents=false;foreach(Control b in headerButtons.Controls){b.AutoSize=false;b.Height=50;}
            root.Controls.Add(header,0,0);
            var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));
            body.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var left=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=10,Padding=new Padding(4)};
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            for(int i=0;i<10;i++)left.RowStyles.Add(new RowStyle(i==5?SizeType.Percent:SizeType.AutoSize,i==5?100:0));
            left.Controls.Add(modeLabel,0,0);left.Controls.Add(shiftLabel,0,1);left.Controls.Add(notesLabel,0,2);left.Controls.Add(customerLabel,0,3);
            left.Controls.Add(Ui.Row(Ui.Button("خصم",()=>{var v=Ui.Amount(this,"خصم الأوردر",cart.Discount);if(v.HasValue){cart.Discount=v.Value;RefreshCart();}}),Ui.Button("ملاحظات الأوردر",()=>{var v=Ui.Input(this,"ملاحظات الأوردر",new Dictionary<string,string>{{"الملاحظات",cart.Notes}});if(v!=null){cart.Notes=v["الملاحظات"];RefreshCart();}},"default"),Ui.Button("قيمة الدليفري",()=>{var v=Ui.Amount(this,"قيمة الدليفري",cart.DeliveryFee);if(v.HasValue){cart.DeliveryFee=v.Value;RefreshCart();}}),Ui.Button("عميل دليفري",PickCustomer,"default"),Ui.Button("طيار دليفري",PickDriver)),0,4);
            actionRow=(FlowLayoutPanel)left.GetControlFromPosition(0,4);actionRow.FlowDirection=FlowDirection.RightToLeft;deliveryFeeButton=(Button)actionRow.Controls[2];deliveryCustomerButton=(Button)actionRow.Controls[3];deliveryDriverButton=(Button)actionRow.Controls[4];foreach(Control b in actionRow.Controls)b.MinimumSize=new Size(90,52);actionRow.SizeChanged+=(s,e)=>StretchRow(actionRow);
            left.Controls.Add(lines,0,5);lines.DoubleClick+=(s,e)=>EditSelected();
            left.Controls.Add(Ui.Row(Ui.Button("كمية / خيارات السطر",EditSelected),Ui.Button("+ كمية",()=>Quantity(1)),Ui.Button("- كمية",()=>Quantity(-1),"default"),Ui.Button("خصم السطر",()=>{int i=SelectedIndex();var v=Ui.Amount(this,"خصم السطر",cart.Lines[i].LineDiscount);if(v.HasValue){cart.Lines[i].LineDiscount=v.Value;RefreshCart();}},"default"),Ui.Button("حذف سطر",()=>{cart.Lines.RemoveAt(SelectedIndex());RefreshCart();},"danger")),0,6);
            left.Controls.Add(totalLabel,0,7);left.Controls.Add(Ui.Row(Ui.Button("إنتظار",()=>Save(true),"default"),Ui.Button("مسح الأوردر",()=>{if(cart.Lines.Count==0||Dialogs.Show(this,"مسح الأوردر الحالي؟","تأكيد",MessageBoxButtons.YesNo)==DialogResult.Yes){if(cart.TableNumber.HasValue)store.ReleaseTable(cart.TableNumber.Value);cart=new Cart{OrderType=cart.OrderType,DineSubtype=cart.DineSubtype};RefreshCart();}},"danger"),Ui.Button("إتمام ودفع",()=>Save(false),"success")),0,8);
            quantityRow=(FlowLayoutPanel)left.GetControlFromPosition(0,6);quantityRow.FlowDirection=FlowDirection.RightToLeft;quantityRow.SizeChanged+=(s,e)=>StretchRow(quantityRow);
            doneRow=(FlowLayoutPanel)left.GetControlFromPosition(0,8);doneRow.FlowDirection=FlowDirection.RightToLeft;parkButton=(Button)doneRow.Controls[0];doneRow.SizeChanged+=(s,e)=>StretchRow(doneRow,true);
            actionRow.AutoSize=quantityRow.AutoSize=doneRow.AutoSize=false;left.RowStyles[4]=new RowStyle(SizeType.Absolute,58);left.RowStyles[6]=new RowStyle(SizeType.Absolute,70);left.RowStyles[8]=new RowStyle(SizeType.Absolute,70);
            left.RowStyles[9]=new RowStyle(SizeType.Absolute,30);credit.Dock=DockStyle.Fill;left.Controls.Add(credit,0,9);
            Action alignCredit=()=>{var clear=doneRow.Controls[1];credit.Margin=new Padding(Math.Max(0,clear.Left),0,Math.Max(0,doneRow.ClientSize.Width-clear.Right),0);};doneRow.Layout+=(s,e)=>alignCredit();left.Layout+=(s,e)=>alignCredit();
            foreach(Control button in quantityRow.Controls)button.MinimumSize=new Size(60,72);foreach(Control button in actionRow.Controls)button.MinimumSize=new Size(60,52);left.RowStyles[6]=new RowStyle(SizeType.Absolute,78);
            foreach(var label in new[]{modeLabel,shiftLabel,customerLabel,notesLabel,totalLabel}){label.RightToLeft=RightToLeft.Yes;label.Anchor=AnchorStyles.Right;label.TextAlign=ContentAlignment.MiddleRight;}
            left.SizeChanged+=(s,e)=>{int available=Math.Max(200,left.ClientSize.Width-16);foreach(var label in new[]{modeLabel,shiftLabel,customerLabel,notesLabel,totalLabel})label.MaximumSize=new Size(available,0);};
            var right=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(4)};for(int i=0;i<6;i++)right.RowStyles.Add(new RowStyle(i==5?SizeType.Percent:SizeType.AutoSize,i==5?100:0));
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            right.Controls.Add(new Label{Text="الخدمة",AutoSize=true},0,0);
            var services=Ui.Row();serviceRow=services;services.FlowDirection=FlowDirection.RightToLeft;services.SizeChanged+=(s,e)=>StretchRow(services);foreach(string service in new[]{"دليفري","تيك اواي","شركات","طاولات"}){string name=service;var b=Ui.Button(name,()=>SetMode(name));b.MinimumSize=new Size(90,58);modeButtons.Add(b);services.Controls.Add(b);}right.Controls.Add(services,0,1);
            services.AutoSize=false;right.RowStyles[1]=new RowStyle(SizeType.Absolute,64);right.RowStyles[4]=new RowStyle(SizeType.Absolute,50);
            right.SizeChanged+=(s,e)=>LayoutCategories();categories.SizeChanged+=(s,e)=>LayoutCategories();
            var scan=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,AutoSize=true};scan.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));scan.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));scan.Controls.Add(barcode,0,0);scan.Controls.Add(new Label{Text="باركود:",AutoSize=true},1,0);right.Controls.Add(scan,0,2);
            barcode.KeyDown+=(s,e)=>{if(e.KeyCode!=Keys.Enter)return;e.SuppressKeyPress=true;try{var found=store.Barcode(barcode.Text.Trim());if(found.Rows.Count==0)throw new ArgumentException("الباركود غير موجود");if(found.Rows.Count>1)throw new ArgumentException("يوجد أكثر من صنف بنفس الباركود");AddItem(found.Rows[0]);barcode.Clear();}catch(Exception error){Dialogs.Show(this,error.Message);}};
            right.Controls.Add(new Label{Text="الأصناف",AutoSize=true,Anchor=AnchorStyles.Right},0,3);right.Controls.Add(categories,0,4);right.Controls.Add(products,0,5);body.Controls.Add(left,1,0);body.Controls.Add(right,0,0);root.Controls.Add(body,0,1);Controls.Add(root);
            headerButtons.FlowDirection=FlowDirection.LeftToRight;
            products.SizeChanged+=(s,e)=>ResizeTiles();FormClosing+=(s,e)=>{if(busy){e.Cancel=true;return;}if(cart.Lines.Count>0&&Dialogs.Show(this,"يوجد أوردر لم يُحفظ. إغلاق البرنامج؟","تأكيد",MessageBoxButtons.YesNo)!=DialogResult.Yes){e.Cancel=true;return;}if(cart.TableNumber.HasValue)try{store.ReleaseTable(cart.TableNumber.Value);}catch(Exception error){AppLog.Error(store.DataRoot,error);}};Reload();
            Shown+=(s,e)=>{foreach(Control b in headerButtons.Controls)b.Width=Math.Max(90,TextRenderer.MeasureText(b.Text,b.Font).Width+24);RefreshCart();ResizeTiles();var low=store.LowStock();if(low.Rows.Count>0)Dialogs.Show(this,"الأصناف التالية أوشكت على النفاد:\n\n"+string.Join("\n",low.Rows.Cast<DataRow>().Take(15).Select(r=>"- "+r["name"]+": متبقي "+Convert.ToDecimal(r["stock_qty"]).ToString("0"))),"تنبيه مخزون منخفض",MessageBoxButtons.OK,MessageBoxIcon.Warning);};
        }
        private void OpenSettings(){session.RequireManager(store);using(var f=new SettingsForm(store,settings,Reload,()=>cart.Lines.Count==0&&!busy))f.ShowDialog(this);Reload();}
        private void Reload()
        {
            while(categories.Controls.Count>0)categories.Controls[0].Dispose();categories.Controls.Add(Ui.Button("الكل",()=>{category="";Populate();}));
            foreach(DataRow row in store.Categories().Rows){string name=Convert.ToString(row["name"]);categories.Controls.Add(Ui.Button(name,()=>{category=name;Populate();}));}
            foreach(Control button in categories.Controls){button.AutoSize=false;button.MinimumSize=new Size(75,44);button.Size=new Size(Math.Max(75,Math.Min(190,TextRenderer.MeasureText(button.Text,button.Font).Width+30)),44);}
            LayoutCategories();Populate();RefreshCart();Ui.Theme(this,settings.Theme=="light");StyleTiles();credit.ForeColor=Color.Gray;RefreshLogo();
        }
        private void RefreshLogo()
        {
            Image next=null;string path=Branding.LogoPath(store.DataRoot);
            if(File.Exists(path))try{using(var image=Image.FromFile(path))next=new Bitmap(image);}catch(Exception error)when(error is ArgumentException||error is IOException||error is OutOfMemoryException){AppLog.Error(store.DataRoot,error);}
            var old=businessLogo.Image;businessLogo.Image=next;businessLogo.Visible=next!=null;old?.Dispose();
        }
        protected override void Dispose(bool disposing){if(disposing){var image=businessLogo.Image;businessLogo.Image=null;image?.Dispose();}base.Dispose(disposing);}
        private bool sizingCategories;
        private void LayoutCategories()
        {
            if(sizingCategories||!(categories.Parent is TableLayoutPanel host))return;sizingCategories=true;
            try
            {
                categories.SuspendLayout();categories.AutoScrollPosition=Point.Empty;categories.AutoScroll=false;categories.AutoScrollMinSize=Size.Empty;
                int width=Math.Max(1,categories.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-8),x=width,y=3;
                foreach(Control button in categories.Controls)
                {
                    int w=Math.Min(width,button.Width);if(x<w){x=width;y+=50;}
                    button.SetBounds(x-w,y,w,44);x-=w+6;
                }
                int contentHeight=y+47,maxHeight=Math.Max(100,host.ClientSize.Height/3);
                host.RowStyles[4].Height=Math.Min(contentHeight,Math.Max(100,maxHeight/50*50));
                categories.AutoScroll=true;categories.AutoScrollMinSize=new Size(0,contentHeight);
                categories.ResumeLayout();
            }
            finally{sizingCategories=false;}
            if(IsHandleCreated&&!IsDisposed&&categories.ClientSize.Width!=categoryLayoutWidth){categoryLayoutWidth=categories.ClientSize.Width;BeginInvoke((Action)LayoutCategories);}
        }
        private int categoryLayoutWidth;
        private void Populate()
        {
            products.SetItems(store.Menu(category,""),AddItem,store.DataRoot,settings.Theme=="light");
        }
        private void StyleTiles(){products.Style(settings.Theme=="light");}
        private void ResizeTiles(){products.ArrangeTiles();}
        private void StretchRow(FlowLayoutPanel row,bool weighted=false)
        {
            var controls=row.Controls.Cast<Control>().Where(c=>c.Visible).ToList();if(controls.Count==0)return;row.SuspendLayout();row.WrapContents=false;int weight=controls.Count+(weighted?1:0);int unit=Math.Max(60,(row.ClientSize.Width-8)/weight-6);foreach(var control in controls){control.AutoSize=false;control.Size=new Size(unit*(weighted&&Convert.ToString(control.Tag)=="success"?2:1),control.MinimumSize.Height);}row.ResumeLayout();
        }
        private void SetMode(string name)
        {
            int? previousTable=cart.TableNumber;
            if(name=="شركات"){var row=PickContact("companies");if(row==null)return;cart.CompanyName=Convert.ToString(row["name"]);cart.OrderType="صالة";cart.DineSubtype="شركات";}
            else if(name=="طاولات"){using(var f=new TablesForm(store,settings.Theme=="light",true)){if(f.ShowDialog(this)!=DialogResult.OK)return;store.ReserveTable(f.SelectedTable.Value);cart.TableNumber=f.SelectedTable;}cart.OrderType="صالة";cart.DineSubtype="طاولات (أرضية)";}
            else cart.OrderType=name;
            if(cart.OrderType!=Cart.Delivery){cart.CustomerName="";cart.CustomerPhone="";cart.CustomerAddress="";cart.DriverName="";cart.DriverPhone="";cart.DriverId=null;}
            if(name!="طاولات")cart.TableNumber=null;if(previousTable.HasValue&&previousTable!=cart.TableNumber)store.ReleaseTable(previousTable.Value);if(name!="شركات")cart.CompanyName="";RefreshCart();
        }
        private DataRow PickContact(string table){using(var f=new ContactsForm(store,table,settings.Theme=="light",true))return f.ShowDialog(this)==DialogResult.OK?f.SelectedRecord:null;}
        private void PickCustomer()
        {
            var row=PickContact("customers");if(row==null)return;
            string address=Convert.ToString(row["address"]);if(address.StartsWith("["))
            {
                var addresses=new JavaScriptSerializer().Deserialize<string[]>(address);var data=new DataTable();data.Columns.Add("العنوان");foreach(string a in addresses)data.Rows.Add(a);var selected=Ui.Pick(this,"عنوان التوصيل",data);if(selected==null)return;address=Convert.ToString(selected[0]);
            }
            SetMode(Cart.Delivery);cart.CustomerName=Convert.ToString(row["name"]);cart.CustomerPhone=Convert.ToString(row["phone"]);cart.CustomerAddress=address;cart.CustomerNotes=Convert.ToString(row["notes"]);RefreshCart();
        }
        private void PickDriver(){var row=PickContact("drivers");if(row==null)return;SetMode(Cart.Delivery);cart.DriverName=Convert.ToString(row["name"]);cart.DriverPhone=Convert.ToString(row["phone"]);cart.DriverId=Convert.ToInt32(row["driver_id"]);RefreshCart();}
        private int SelectedIndex(){if(lines.SelectedIndex<0||lines.SelectedIndex>=cart.Lines.Count)throw new ArgumentException("اختر سطرًا من الأوردر");return lines.SelectedIndex;}
        private void Quantity(int delta){int i=SelectedIndex();cart.Lines[i].Quantity=Math.Max(1,Math.Min(999,cart.Lines[i].Quantity+delta));RefreshCart();lines.SelectedIndex=i;}
        private void AddItem(DataRow item){var line=new CartLine{ItemName=Convert.ToString(item["name"]),UnitPrice=Convert.ToDecimal(item["price"])};if(LineDialog(line)){cart.Lines.Add(line);RefreshCart();}}
        private void EditSelected(){int i=SelectedIndex();LineDialog(cart.Lines[i]);RefreshCart();lines.SelectedIndex=i;}
        private bool LineDialog(CartLine line)
        {
            using(var f=Ui.Form("صنف: "+line.ItemName,560,670))
            {
                var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(12)};for(int i=0;i<5;i++)root.RowStyles.Add(new RowStyle(i==3?SizeType.Percent:SizeType.AutoSize,i==3?100:0));
                root.Controls.Add(new Label{Text=line.ItemName+" — "+line.UnitPrice.ToString("0.00")+" ج للواحدة",AutoSize=true},0,0);
                var qty=new NumericUpDown{Minimum=1,Maximum=999,Value=line.Quantity,Font=new Font("Segoe UI",20),Width=180};var variants=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=200};var options=store.Variants(line.ItemName);
                variants.Items.Add("السعر الأساسي");foreach(DataRow row in options.Rows)variants.Items.Add(row["variant_name"]);variants.SelectedIndex=line.Variant==""?0:variants.Items.IndexOf(line.Variant);if(variants.SelectedIndex<0)variants.SelectedIndex=0;
                root.Controls.Add(Ui.Row(new Label{Text="الكمية",AutoSize=true},qty,variants),0,1);
                var keypad=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=3,RowCount=4};for(int c=0;c<3;c++)keypad.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));int[] digits={7,8,9,4,5,6,1,2,3,0};int index=0;string buffer="";
                foreach(int digit in digits){int n=digit;var b=Ui.Button(n.ToString(),()=>{buffer=(buffer+n).TrimStart('0');if(buffer.Length>3)buffer=n.ToString();int value;if(int.TryParse(buffer,out value)&&value>=1&&value<=999)qty.Value=value;});keypad.Controls.Add(b,index%3,index/3);index++;}
                keypad.Controls.Add(Ui.Button("⌫ حذف",()=>{buffer=buffer.Length>0?buffer.Substring(0,buffer.Length-1):"";qty.Value=buffer==""?1:int.Parse(buffer);},"default"),1,3);keypad.Controls.Add(Ui.Button("مسح",()=>{buffer="";qty.Value=1;},"default"),2,3);root.Controls.Add(keypad,0,2);
                foreach(Control key in keypad.Controls){key.Dock=DockStyle.Fill;key.AutoSize=false;key.MinimumSize=new Size(70,54);}
                var checks=new CheckedListBox{Dock=DockStyle.Fill,CheckOnClick=true};foreach(DataRow row in store.Options().Rows){string option=Convert.ToString(row["option"]);checks.Items.Add(option,line.Options.Contains(option));}root.Controls.Add(checks,0,3);if(checks.Items.Count==0){checks.Visible=false;root.RowStyles[3]=new RowStyle(SizeType.Absolute,0);f.Height=490;}
                root.Controls.Add(Ui.Row(Ui.Button("تأكيد",()=>f.DialogResult=DialogResult.OK,"success"),Ui.Button("إلغاء",()=>f.DialogResult=DialogResult.Cancel,"danger")),0,4);f.Controls.Add(root);Ui.Theme(f,settings.Theme=="light");if(f.ShowDialog(this)!=DialogResult.OK)return false;
                line.Quantity=(int)qty.Value;line.Options=checks.CheckedItems.Cast<string>().ToList();if(variants.SelectedIndex>0){var v=options.Rows[variants.SelectedIndex-1];line.Variant=Convert.ToString(v["variant_name"]);line.UnitPrice=Convert.ToDecimal(v["price"]);}else{var baseRow=store.Query("SELECT price FROM catalog_items WHERE catalog_type='menu' AND name=@p0",line.ItemName);line.Variant="";if(baseRow.Rows.Count==1)line.UnitPrice=Convert.ToDecimal(baseRow.Rows[0][0]);}return true;
            }
        }
        private void RefreshCart()
        {
            int selected=lines.SelectedIndex;lines.Rows.Clear();foreach(var line in cart.Lines)lines.Rows.Add(line.ItemName+(line.Variant!=""?" ("+line.Variant+")":"")+(line.Options.Count>0?" — "+string.Join("، ",line.Options):""),line.Quantity,line.UnitPrice,line.Subtotal);if(lines.Rows.Count>0)lines.SelectedIndex=Math.Min(Math.Max(0,selected),lines.Rows.Count-1);
            totalLabel.Text="بعد خصم الأصناف: "+cart.Subtotal.ToString("0.00")+" ج | خصم الأوردر: "+cart.Discount.ToString("0.00")+" ج"+(cart.OrderType==Cart.Delivery?" | الدليفري: "+cart.DeliveryFee.ToString("0.00")+" ج":"")+"\nالإجمالي: "+cart.Total.ToString("0.00")+" ج";
            modeLabel.Text=cart.EditingOrder.HasValue?"تعديل أوردر #"+cart.EditingOrder:"الخدمة الحالية: "+(cart.OrderType=="صالة"?cart.DineSubtype+(cart.CompanyName!=""?" — "+cart.CompanyName:cart.TableNumber.HasValue?" — "+cart.TableNumber:""):cart.OrderType);shiftLabel.Text="الوردية الحالية: #"+store.ActiveShift+(cashier!=""?" — الكاشير: "+cashier:"");
            customerLabel.Text=cart.CustomerName+(cart.CustomerPhone!=""?" — "+cart.CustomerPhone:"")+(cart.DriverName!=""?" | الطيار: "+cart.DriverName:"");notesLabel.Text="ملاحظات الأوردر: "+(cart.Notes!=""?cart.Notes:"لا توجد");
            customerLabel.Visible=customerLabel.Text!="";
            waitingButton.Text="انتظار "+store.Query("SELECT COUNT(*) FROM orders WHERE status='waiting' AND shift_id=@p0",store.ActiveShift).Rows[0][0];
            foreach(var b in modeButtons){bool active=b.Text==cart.OrderType||cart.OrderType=="صالة"&&(b.Text=="شركات"&&cart.DineSubtype=="شركات"||b.Text=="طاولات"&&cart.DineSubtype!="شركات");b.Tag=active?"primary":"default";Ui.Theme(b,settings.Theme=="light");b.FlatAppearance.BorderColor=active?Ui.Gold:Color.FromArgb(63,63,63);b.FlatAppearance.BorderSize=active?2:1;b.Invalidate();}
            deliveryFeeButton.Visible=deliveryCustomerButton.Visible=deliveryDriverButton.Visible=parkButton.Visible=cart.OrderType==Cart.Delivery;StretchRow(actionRow);StretchRow(quantityRow);StretchRow(doneRow,true);StretchRow(serviceRow);
        }
        private async void Save(bool waiting)
        {
            if(busy)return;if(cart.Lines.Count==0){Dialogs.Show(this,"الأوردر فاضي");return;}
            string method="كاش";decimal paid=cart.Total;
            if(!waiting&&!PaymentDialog(ref method,ref paid))return;
            busy=true;Enabled=false;DataRow order=null;
            try
            {
                Cart snapshot=cart;order=await Task.Run(()=>store.SaveOrder(snapshot,cashier,method,paid,waiting));
                cart=new Cart{OrderType=snapshot.OrderType,DineSubtype=snapshot.DineSubtype};RefreshCart();
                try { await Task.Run(()=>new ReceiptPrinter(settings,store.DataRoot).Print(order,waiting)); }
                catch(Exception error){try{store.Audit("print_fail","order="+order["order_id"]+" / "+error.Message);}catch{}Dialogs.Show(this,"تم حفظ الأوردر #"+order["order_id"]+" لكن الطباعة لم تكتمل:\n"+error.Message+"\nأعد الطباعة من الطلبات السابقة. لا تعِد حفظ الأوردر.","الطباعة",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
            }
            catch(Exception error){Dialogs.Show(this,(order==null?"":"تم حفظ الأوردر #"+order["order_id"]+". حدث خطأ بعد الحفظ؛ لا تعِد تسجيله.\n")+error.Message,order==null?"تعذر حفظ الأوردر":"خطأ بعد الحفظ",MessageBoxButtons.OK,MessageBoxIcon.Error);}
            finally{busy=false;Enabled=true;Activate();}
        }
        private bool PaymentDialog(ref string method,ref decimal paid)
        {
            using(var f=Ui.Form("الدفع",570,540))
            {
                string selectedMethod=method;decimal sum=cart.Total;var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(20)};
                layout.Controls.Add(new Label{Text="إتمام الدفع — الإجمالي: "+sum.ToString("0.00")+" ج",AutoSize=true,Font=new Font("Segoe UI",20,FontStyle.Bold)});
                var choose=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=470,DataSource=new[]{"كاش","فودافون كاش / تحويل"}};layout.Controls.Add(choose);
                layout.Controls.Add(Ui.Button("تقسيم الفاتورة على أكثر من شخص",()=>{var n=Ui.Amount(f,"عدد الأشخاص",2);if(n.HasValue&&n.Value>=2)Dialogs.Show(f,"نصيب الشخص: "+(sum/(int)n.Value).ToString("0.00")+" ج — السداد كإجمالي واحد");},"default"));
                var amount=new NumericUpDown{Maximum=1000000,DecimalPlaces=2,Value=sum,Width=470,Font=new Font("Segoe UI",20)};layout.Controls.Add(new Label{Text="المدفوع",AutoSize=true});layout.Controls.Add(amount);var quick=Ui.Row();foreach(int v in new[]{20,50,100,200}){int n=v;var b=Ui.Button(v.ToString(),()=>amount.Value=n,"default");b.Enabled=v>=sum;quick.Controls.Add(b);}layout.Controls.Add(quick);
                var remaining=new Label{AutoSize=true,Text="الباقي: 0.00 ج"};amount.ValueChanged+=(s,e)=>remaining.Text=amount.Value>=sum?"الباقي: "+(amount.Value-sum).ToString("0.00")+" ج":"المتبقي للسداد: "+(sum-amount.Value).ToString("0.00")+" ج";layout.Controls.Add(remaining);
                layout.Controls.Add(Ui.Row(Ui.Button("إلغاء",()=>f.DialogResult=DialogResult.Cancel,"danger"),Ui.Button("تأكيد",()=>{if(amount.Value<sum)throw new ArgumentException("المبلغ المدفوع أقل من الإجمالي");f.DialogResult=DialogResult.OK;},"success")));f.Controls.Add(layout);Ui.Theme(f,settings.Theme=="light");if(f.ShowDialog(this)!=DialogResult.OK)return false;method=choose.Text;paid=amount.Value;return true;
            }
        }
        private void LoadCart(DataRow row)
        {
            if(Convert.ToString(row["status"])!="waiting")session.RequireManager(store);
            cart=new Cart{OrderType=Convert.ToString(row["order_type"]),DineSubtype=Convert.ToString(row["dine_subtype"]),CompanyName=Convert.ToString(row["company_name"]),CustomerName=Convert.ToString(row["customer_name"]),CustomerPhone=Convert.ToString(row["customer_phone"]),CustomerAddress=Convert.ToString(row["customer_address"]),CustomerNotes=Convert.ToString(row["customer_notes"]),DriverName=Convert.ToString(row["driver_name"]),DriverPhone=Convert.ToString(row["driver_phone"]),Discount=Convert.ToDecimal(row["discount_egp"]),DeliveryFee=Convert.ToDecimal(row["delivery_fee_egp"]),Notes=Convert.ToString(row["order_notes"]),EditingOrder=Convert.ToInt64(row["order_id"]),EditingShift=Convert.ToInt64(row["shift_id"])};
            if(cart.DineSubtype!="")cart.OrderType="صالة";if(int.TryParse(Convert.ToString(row["driver_id"]),out int driver))cart.DriverId=driver;if(int.TryParse(Convert.ToString(row["table_no"]),out int table))cart.TableNumber=table;cart.Lines.AddRange(Store.ParseLines(Convert.ToString(row["items_json"])));RefreshCart();
        }
        private void History(bool waiting)
        {
            using(var f=Ui.Form(waiting?"أوردرات الانتظار":"الطلبات السابقة",1100,700))
            {
                var rows=store.Orders(waiting);if(waiting)rows.DefaultView.RowFilter="shift_id = '"+store.ActiveShift+"'";
                var grid=Ui.Grid(rows);grid.MultiSelect=waiting;
                var visible=waiting?new[]{"order_id","shift_id","created_at","order_type","customer_name","driver_name","total","table_no"}:new[]{"order_id","shift_id","shift_start_at","created_at","order_type","customer_name","total"};
                grid.DataBindingComplete+=(s,e)=>{foreach(DataGridViewColumn column in grid.Columns)column.Visible=visible.Contains(column.DataPropertyName);};
                var p=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1};p.RowStyles.Add(new RowStyle(SizeType.AutoSize));p.RowStyles.Add(new RowStyle(SizeType.Percent,100));p.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var search=new TextBox{Width=300};search.TextChanged+=(s,e)=>{string q=search.Text.Replace("'","''").Replace("[","[[]").Replace("%","[%]").Replace("*","[*]");rows.DefaultView.RowFilter=(waiting?"shift_id = '"+store.ActiveShift+"' AND ":"")+"(CONVERT(order_id,'System.String') LIKE '%"+q+"%' OR customer_phone LIKE '%"+q+"%' OR customer_name LIKE '%"+q+"%')";};p.Controls.Add(Ui.Row(new Label{Text="بحث: رقم الأوردر / العميل / الهاتف",AutoSize=true},search),0,0);p.Controls.Add(grid,0,1);
                Func<DataRow> selected=()=>{if(!(grid.CurrentRow?.DataBoundItem is DataRowView r))throw new ArgumentException("اختر أوردرًا");return r.Row;};
                var buttons=Ui.Row();
                if(waiting||session.IsManager)buttons.Controls.Add(Ui.Button(waiting?"استكمال الأوردر":"تعديل الأوردر",()=>{if(!waiting)session.RequireManager(store);var r=selected();if(Convert.ToInt64(r["shift_id"])!=store.ActiveShift)throw new ArgumentException("التعديل متاح للوردية الحالية فقط");if(cart.Lines.Count>0)throw new ArgumentException("أتمم أو امسح الأوردر الحالي أولاً");LoadCart(r);f.Close();}));
                buttons.Controls.Add(Ui.Button("إغلاق",f.Close,"default"));
                buttons.Controls.Add(Ui.Button("إعادة طباعة",()=>{var r=selected();new ReceiptPrinter(settings,store.DataRoot).Print(r,Convert.ToString(r["status"])=="waiting");store.Audit("reprint","order="+r["order_id"]);}));
                if(waiting)
                {
                    buttons.Controls.Add(Ui.Button("إلغاء الأوردر",()=>{var r=selected();if(Dialogs.Show(f,"إلغاء أوردر الانتظار؟","تأكيد",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;store.DeleteWaiting(Convert.ToInt64(r["shift_id"]),Convert.ToInt64(r["order_id"]));rows.Rows.Remove(r);RefreshCart();},"danger"));
                    buttons.Controls.Add(Ui.Button("دمج الأوردرات المحددة",()=>{if(cart.Lines.Count>0)throw new ArgumentException("السلة الحالية بها أصناف");var selectedRows=grid.SelectedRows.Cast<DataGridViewRow>().Select(r=>((DataRowView)r.DataBoundItem).Row).ToList();if(selectedRows.Count<2)throw new ArgumentException("اختر أوردرين على الأقل");LoadCart(selectedRows[0]);cart.EditingOrder=null;cart.EditingShift=null;cart.Lines.Clear();foreach(var r in selectedRows){cart.Lines.AddRange(Store.ParseLines(Convert.ToString(r["items_json"])));cart.MergedWaiting.Add(Convert.ToInt64(r["order_id"]));}RefreshCart();f.Close();}));
                }
                p.Controls.Add(buttons,0,2);f.Controls.Add(p);Ui.Theme(f,settings.Theme=="light");f.ShowDialog(this);
            }
        }
    }
}
