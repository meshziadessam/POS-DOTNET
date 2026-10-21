using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;

namespace RestaurantPos
{
    internal static class ShiftReceiptLayout
    {
        private sealed class Row
        {
            public string[] Values;
            public float[] Widths;
            public bool Bold, Border;
            public float Height;
        }
        public static Bitmap Render(ReportService reports,long shift,bool includeOrders,PosSettings settings,string root)
        {
            int width=settings.PaperWidth==58?384:576;
            float scale=width/576f,pad=12*scale,usable=width-2*pad;
            var rows=new List<Row>();
            Action<string,bool> heading=(value,bold)=>rows.Add(new Row{Values=new[]{value},Widths=new[]{1f},Bold=bold});
            Action<string,string> pair=(label,value)=>rows.Add(new Row{Values=new[]{label,value},Widths=new[]{.62f,.38f},Border=true});
            Action<string,DataTable,string[],float[]> table=(title,data,columns,widths)=>
            {
                heading(title,true);
                rows.Add(new Row{Values=columns.Select(column=>column=="orders"?"العدد":column=="total"&&title!="تفاصيل الطلبات"?"صافي المبيعات":column=="line_total"?"الإجمالي":column=="created_at"?"الوقت":column=="order_id"?"الطلب":column=="delivery_fees"?"الدليفري":Ui.Caption(column)).ToArray(),Widths=widths,Bold=true,Border=true});
                if(data.Rows.Count==0)rows.Add(new Row{Values=new[]{"لا توجد بيانات"},Widths=new[]{1f},Border=true});
                foreach(DataRow record in data.Rows)
                    rows.Add(new Row{Values=columns.Select(column=>
                    {
                        object value=record[column];
                        if(value==DBNull.Value)return "—";
                        if(value is decimal||value is double)return Convert.ToDecimal(value).ToString("0.00",CultureInfo.InvariantCulture);
                        DateTime date;if(column=="created_at"&&DateTime.TryParse(Convert.ToString(value),out date))return date.ToString("dd/MM/yyyy\nHH:mm");
                        return Convert.ToString(value);
                    }).ToArray(),Widths=widths,Border=true});
            };
            heading(settings.BusinessName,true);
            if(!string.IsNullOrWhiteSpace(settings.ReceiptPhones))heading(settings.ReceiptPhones,false);
            heading("تقرير الوردية #"+shift,true);
            foreach(DataRow metadata in reports.ShiftMetadata(shift).Rows)
            {
                pair("بداية الوردية",Convert.ToString(metadata["start_at"]));
                pair("نهاية الوردية",string.IsNullOrWhiteSpace(Convert.ToString(metadata["end_at"]))?"مفتوحة":Convert.ToString(metadata["end_at"]));
            }
            pair("وقت طباعة التقرير",DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            table("مبيعات الأصناف",reports.ByItem(shift),new[]{"item","qty","line_total"},new[]{.5f,.18f,.32f});
            table("حسب نوع الطلب",reports.ByType(shift),new[]{"order_type","orders","total","delivery_fees"},new[]{.31f,.15f,.27f,.27f});
            table("حسب الطيار",reports.ByDriver(shift),new[]{"driver_name","orders","total","delivery_fees"},new[]{.31f,.15f,.27f,.27f});
            if(includeOrders)table("تفاصيل الطلبات",reports.Brief(shift),new[]{"order_id","created_at","total"},new[]{.23f,.43f,.34f});
            var summary=reports.Summary(shift);
            heading("إجماليات الوردية",true);
            pair("عدد الطلبات",summary.Orders.ToString());pair("عدد الأصناف",summary.Items.ToString());
            foreach(var total in new[]{Tuple.Create("قبل الخصم",summary.Gross),Tuple.Create("الخصومات",summary.Discounts),Tuple.Create("دليفري للسائق",summary.Delivery),Tuple.Create("صافي المطعم",summary.Net),Tuple.Create("مصروفات الخزنة",summary.Expenses),Tuple.Create("الصافي بعد الخزنة",summary.AfterExpenses)})pair(total.Item1,total.Item2.ToString("0.00",CultureInfo.InvariantCulture));
            heading("جميع المبالغ بالجنيه المصري",false);
            heading("نهاية التقرير",true);heading("Powered by Push",false);
            using(var regular=new Font("Arial",26*scale,FontStyle.Regular,GraphicsUnit.Pixel))
            using(var bold=new Font("Arial",28*scale,FontStyle.Bold,GraphicsUnit.Pixel))
            using(var probe=new Bitmap(1,1))using(var measure=Graphics.FromImage(probe))
            using(var format=new StringFormat(StringFormatFlags.DirectionRightToLeft){Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
            {
                string logoPath=Branding.LogoPath(root);
                using(var logo=File.Exists(logoPath)?Image.FromFile(logoPath):null)
                {
                    float logoHeight=logo==null?0:Math.Min(120*scale,logo.Height*Math.Min(1,120*scale/logo.Width));
                    float height=pad*2+logoHeight;
                    foreach(var row in rows)
                    {
                        row.Height=Math.Max(44*scale,row.Values.Select((value,i)=>measure.MeasureString(value,row.Bold?bold:regular,(int)Math.Max(1,usable*row.Widths[i]-12*scale),format).Height+16*scale).Max());
                        height+=row.Height;
                    }
                    if(height>60000)throw new InvalidOperationException("التقرير طويل جدًا؛ اطبع بدون تفاصيل الطلبات");
                    var bitmap=new Bitmap(width,(int)Math.Ceiling(height),PixelFormat.Format24bppRgb);
                    try
                    {
                        bitmap.SetResolution(203,203);
                        using(var draw=Graphics.FromImage(bitmap))using(var pen=new Pen(Color.Black,1))
                        {
                            draw.Clear(Color.White);draw.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                            float y=pad;
                            if(logo!=null){float logoWidth=logo.Width*logoHeight/logo.Height;draw.DrawImage(logo,(width-logoWidth)/2,y,logoWidth,logoHeight);y+=logoHeight;}
                            foreach(var row in rows)
                            {
                                float right=width-pad;
                                for(int i=0;i<row.Values.Length;i++)
                                {
                                    float cellWidth=usable*row.Widths[i];right-=cellWidth;
                                    if(row.Bold)draw.FillRectangle(Brushes.Gainsboro,right,y,cellWidth,row.Height);
                                    if(row.Border)draw.DrawRectangle(pen,right,y,cellWidth,row.Height);
                                    draw.DrawString(row.Values[i],row.Bold?bold:regular,Brushes.Black,new RectangleF(right+6*scale,y+4*scale,cellWidth-12*scale,row.Height-8*scale),format);
                                }
                                y+=row.Height;
                            }
                        }
                        return bitmap;
                    }
                    catch{bitmap.Dispose();throw;}
                }
            }
        }
    }
}
