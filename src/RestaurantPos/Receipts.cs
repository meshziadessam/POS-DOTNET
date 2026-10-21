using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Drawing.Imaging;
using System.IO;

namespace RestaurantPos
{
    public sealed class ReceiptPrinter
    {
        private readonly PosSettings settings;
        private readonly string root;
        public ReceiptPrinter(PosSettings settings,string root) { this.settings=settings;this.root=root; }
        public Bitmap RenderText(IEnumerable<string> text)
        {
            int width=settings.PaperWidth==58?384:576;var lines=new List<string>(text);
            using(var font=new Font("Arial",settings.PaperWidth==58?21:24,FontStyle.Bold,GraphicsUnit.Pixel))
            using(var measure=new Bitmap(1,1))using(var g=Graphics.FromImage(measure))using(var format=new StringFormat(StringFormatFlags.DirectionRightToLeft))
            {
                float height=30;foreach(string line in lines)height+=g.MeasureString(line,font,width-32,format).Height+10;
                if(height>60000)throw new InvalidOperationException("التقرير طويل جدًا؛ اختر فترة أقصر للطباعة");
                var bitmap=new Bitmap(width,(int)Math.Ceiling(height)+20);bitmap.SetResolution(203,203);
                using(var draw=Graphics.FromImage(bitmap)){draw.Clear(Color.White);float y=15;foreach(string line in lines){float h=draw.MeasureString(line,font,width-32,format).Height+10;draw.DrawString(line,font,Brushes.Black,new RectangleF(16,y,width-32,h),format);y+=h;}}return bitmap;
            }
        }
        public void PrintText(IEnumerable<string> text,string title,string outputFile=null){using(var bitmap=RenderText(text))PrintBitmap(bitmap,title,outputFile);}
        public void PrintTest(){PrintText(new[]{"Restaurant POS","اختبار طباعة — ليست فاتورة بيع","TEST ONLY — NOT A SALE",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),"الطابعة: "+settings.PrinterName,"اختبار عربي: الاسم — الكمية — السعر","صنف تجريبي × 2 = 25.00 ج","العربي والأرقام: ١٢٣٤٥٦٧٨٩٠","لا توجد مبيعات أو حركة مخزون","نهاية اختبار الطباعة"},"TEST ONLY — ليست فاتورة بيع");}
        public Bitmap Render(DataRow order,bool kitchen)
        {return ReceiptLayout.Render(order,kitchen,settings,root);}
        private Bitmap RenderPlain(DataRow order,bool kitchen)
        {
            int width=settings.PaperWidth==58?384:576;
            var lines=new List<string>{kitchen?"*** نسخة مطبخ ***":"*** نسخة عميل ***","رقم الأوردر "+order["order_id"],"التاريخ والوقت "+order["created_at"],"نوع الطلب: "+order["order_type"]};
            foreach(string column in new[]{"customer_name","customer_phone","customer_address","customer_notes","driver_name"})
                if(!string.IsNullOrWhiteSpace(Convert.ToString(order[column])))lines.Add((column=="customer_name"?"اسم العميل: ":column=="customer_phone"?"الموبايل: ":column=="customer_address"?"العنوان: ":column=="driver_name"?"السائق: ":"ملاحظات العميل: ")+order[column]);
            if(!string.IsNullOrWhiteSpace(Convert.ToString(order["table_no"])))lines.Add("طاولة: "+order["table_no"]);
            lines.Add("────────────────────");
            decimal subtotal=0;
            foreach(var line in Store.ParseLines(Convert.ToString(order["items_json"])))
            {
                lines.Add(line.Quantity+" × "+line.ItemName+(line.Variant!=""?" ("+line.Variant+")":""));
                if(!kitchen)lines.Add("السعر: "+line.UnitPrice.ToString("0.00")+"    الإجمالي: "+line.Subtotal.ToString("0.00")+" ج");
                if(line.Options.Count>0)lines.Add("خيارات: "+string.Join(" + ",line.Options));
                if(line.Extras.Count>0)lines.Add("إضافات: "+string.Join(" + ",line.Extras));
                if(line.LineDiscount>0&&!kitchen)lines.Add("خصم السطر: "+line.LineDiscount.ToString("0.00"));
                subtotal+=line.Subtotal;
            }
            lines.Add("────────────────────");
            if(!kitchen)
            {
                lines.Add("الإجمالي: "+subtotal.ToString("0.00")+" ج");
                lines.Add("الخصم: "+Convert.ToDecimal(order["discount_egp"]).ToString("0.00")+" ج");
                if(Convert.ToDecimal(order["delivery_fee_egp"])>0)lines.Add("خدمة التوصيل: "+Convert.ToDecimal(order["delivery_fee_egp"]).ToString("0.00")+" ج");
                lines.Add("الصافي: "+Convert.ToDecimal(order["total"]).ToString("0.00")+" ج");
                lines.Add(Convert.ToString(order["payment_method"])+" — المدفوع: "+Convert.ToDecimal(order["paid"]).ToString("0.00"));
                lines.Add("الباقي: "+Math.Max(0,Convert.ToDecimal(order["paid"])-Convert.ToDecimal(order["total"])).ToString("0.00"));
            }
            if(!string.IsNullOrWhiteSpace(Convert.ToString(order["order_notes"])))lines.Add("ملاحظات الأوردر: "+order["order_notes"]);
            if(!kitchen)lines.Add("شكراً لزيارتكم");
            using(var font=new Font("Arial",settings.PaperWidth==58?22:25,FontStyle.Bold,GraphicsUnit.Pixel))
            using(var measure=new Bitmap(1,1))using(var graphics=Graphics.FromImage(measure))
            using(var format=new StringFormat(StringFormatFlags.DirectionRightToLeft){Alignment=StringAlignment.Near})
            {
                int logoHeight=0;string logoPath=Path.Combine(root,"data","restaurant_logo.png");
                if(!kitchen&&File.Exists(logoPath))logoHeight=110;
                float height=32+logoHeight; foreach(var line in lines)height+=graphics.MeasureString(line,font,width-32,format).Height+9;
                var bitmap=new Bitmap(width,(int)Math.Ceiling(height)+25,PixelFormat.Format24bppRgb);bitmap.SetResolution(203,203);
                using(var draw=Graphics.FromImage(bitmap))
                {
                    draw.Clear(Color.White);draw.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;float y=16;
                    if(logoHeight>0)using(var logo=Image.FromFile(logoPath)){float w=Math.Min(width-40,logo.Width*100f/logo.Height);draw.DrawImage(logo,(width-w)/2,y,w,100);y+=logoHeight;}
                    foreach(var line in lines){float h=draw.MeasureString(line,font,width-32,format).Height+9;draw.DrawString(line,font,Brushes.Black,new RectangleF(16,y,width-32,h),format);y+=h;}
                }
                return bitmap;
            }
        }
        public void Print(DataRow order,bool kitchenOnly=false,string outputFile=null)
        {
            if(kitchenOnly){using(var bitmap=Render(order,true))PrintBitmap(bitmap,"مطبخ #"+order["order_id"],outputFile);return;}
            if(settings.PrintMode=="both")using(var bitmap=Render(order,true))PrintBitmap(bitmap,"مطبخ #"+order["order_id"],outputFile==null?null:outputFile+".kitchen.prn");
            int copies=settings.PrintMode=="both"&&Convert.ToString(order["order_type"])==Cart.Delivery?2:1;
            for(int i=0;i<copies;i++)using(var bitmap=Render(order,false))PrintBitmap(bitmap,"عميل #"+order["order_id"],i==0||outputFile==null?outputFile:outputFile+".customer2.prn");
        }
        public void PrintBitmap(Bitmap bitmap,string title,string outputFile=null)
        {
            if(settings.PrinterTransport=="escpos")
            {
                var bytes=EscPosRaster.Encode(bitmap,settings.CutPaper);if(outputFile!=null)File.WriteAllBytes(outputFile,bytes);else WindowsRawPrinter.Send(settings.PrinterName,title,bytes);return;
            }
            using(var document=new PrintDocument())
            {
                if(!string.IsNullOrWhiteSpace(settings.PrinterName))document.PrinterSettings.PrinterName=settings.PrinterName;
                if(!document.PrinterSettings.IsValid)throw new InvalidOperationException("لم يتم العثور على الطابعة. افتح إعدادات ← النظام ← الطباعة واختر طابعة Windows مثبتة.");
                if(outputFile!=null){document.PrinterSettings.PrintToFile=true;document.PrinterSettings.PrintFileName=outputFile;}
                document.DocumentName="Restaurant POS — "+title;document.PrintController=new StandardPrintController();
                int paperWidth=(int)Math.Round(settings.PaperWidth/25.4*100);
                int height=(int)Math.Ceiling(bitmap.Height/(double)bitmap.Width*paperWidth)+20;
                document.DefaultPageSettings.PaperSize=new PaperSize(settings.PaperWidth+"mm receipt",paperWidth,Math.Min(32000,Math.Max(100,height)));
                document.DefaultPageSettings.Margins=new Margins(0,0,0,0);
                int offset=0;
                document.PrintPage+=(sender,e)=>
                {
                    float targetWidth=Math.Min(paperWidth,e.PageBounds.Width)-8;
                    int pixels=(int)Math.Min(bitmap.Height-offset,(e.PageBounds.Height-12)*bitmap.Width/targetWidth);
                    if(pixels<=0)throw new InvalidOperationException("مقاس الورق غير صحيح في تعريف الطابعة");
                    e.Graphics.DrawImage(bitmap,new RectangleF(2-e.PageSettings.HardMarginX,2-e.PageSettings.HardMarginY,targetWidth,pixels*targetWidth/bitmap.Width),new RectangleF(0,offset,bitmap.Width,pixels),GraphicsUnit.Pixel);
                    offset+=pixels;e.HasMorePages=offset<bitmap.Height;
                };
                document.Print();
            }
        }
    }
}
