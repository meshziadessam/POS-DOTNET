using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace RestaurantPos
{
    internal static class ReceiptLayout
    {
        public static Bitmap Render(DataRow order,bool kitchen,PosSettings settings,string root)
        {
            int width=settings.PaperWidth==58?384:576;float scale=width/576f;int pad=(int)(12*scale);int usable=width-pad*2;
            var lines=Store.ParseLines(Convert.ToString(order["items_json"]));
            using(var small=new Font("Arial",30*scale,FontStyle.Regular,GraphicsUnit.Pixel))using(var medium=new Font("Arial",36*scale,FontStyle.Bold,GraphicsUnit.Pixel))using(var large=new Font("Arial",44*scale,FontStyle.Bold,GraphicsUnit.Pixel))
            using(var probe=new Bitmap(1,1))using(var measure=Graphics.FromImage(probe))
            {
                var heights=lines.Select(l=>Math.Max(52*scale,measure.MeasureString(l.ItemName+(l.Variant!=""?" ("+l.Variant+")":"")+(l.Options.Count>0?" + "+string.Join(" + ",l.Options):""),small,(int)(usable*(kitchen?.8:.48))-8).Height+12)).ToList();
                var customer=new List<string>();if(!kitchen&&Convert.ToString(order["order_type"])==Cart.Delivery)foreach(var pair in new[]{Tuple.Create("customer_name","العميل"),Tuple.Create("customer_phone","الموبايل"),Tuple.Create("customer_address","العنوان"),Tuple.Create("customer_notes","ملاحظات العميل"),Tuple.Create("driver_name","السواق"),Tuple.Create("driver_phone","رقم السواق")})if(!string.IsNullOrWhiteSpace(Convert.ToString(order[pair.Item1])))customer.Add(pair.Item2+": "+order[pair.Item1]);
                string notes=Convert.ToString(order["order_notes"]);var extras=lines.Where(l=>l.Extras.Count>0||l.LineDiscount>0).Select(l=>l.ItemName+": "+(l.Extras.Count>0?"إضافات "+string.Join(" + ",l.Extras):"")+(l.LineDiscount>0?" | خصم السطر "+l.LineDiscount.ToString("0.00"):"")).ToList();
                float deliveryH=customer.Count==0?0:52*scale+customer.Sum(s=>measure.MeasureString(s,small,usable-12).Height+8);
                float notesH=string.IsNullOrWhiteSpace(notes)?0:52*scale+measure.MeasureString(notes,small,usable-12).Height+14;
                float extraH=extras.Sum(s=>measure.MeasureString(s,small,usable-12).Height+8);int maxHeight=(int)(1200*scale+heights.Sum()+deliveryH+notesH+extraH);
                if(maxHeight>60000)throw new InvalidOperationException("الإيصال طويل جدًا للطباعة");
                using(var canvas=new Bitmap(width,Math.Max(300,maxHeight),PixelFormat.Format24bppRgb))using(var g=Graphics.FromImage(canvas))using(var pen=new Pen(Color.Black,Math.Max(1,scale)))
                {
                    g.Clear(Color.White);g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;float y=8;
                    Action<string,Font,float,float,float,float> text=(s,font,x,top,w,h)=>{using(var fmt=new StringFormat(StringFormatFlags.DirectionRightToLeft){Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString(s,font,Brushes.Black,new RectangleF(x+3,top,w-6,h),fmt);};
                    Action<string> full=s=>{float h=measure.MeasureString(s,small,usable-8).Height+8;text(s,small,pad,y,usable,h);y+=h;};
                    string logo=Path.Combine(root,"data","restaurant_logo.png");if(!kitchen&&File.Exists(logo))using(var image=Image.FromFile(logo)){float ratio=Math.Min(1,110*scale/image.Width);float w=image.Width*ratio,h=Math.Min(150*scale,image.Height*ratio);g.DrawImage(image,(width-w)/2,y,w,h);y+=h+8;}
                    full(settings.ReceiptPhones);g.DrawLine(pen,pad,y,width-pad,y);y+=8;
                    float row=78*scale;g.DrawRectangle(pen,pad,y,usable,row);float half=usable/2f;g.DrawLine(pen,pad+half,y,pad+half,y+row);text("رقم الأوردر",medium,pad,y,half,row/2);text("#"+order["order_id"],large,pad,y+row/2,half,row/2);string type=Convert.ToString(order["order_type"]);text(type==Cart.Takeaway?"takeaway":type,large,pad+half,y,half,row);y+=row+8;
                    DateTime created;DateTime.TryParse(Convert.ToString(order["created_at"]),out created);foreach(var meta in new[]{Tuple.Create("الساعة",created==DateTime.MinValue?"":created.ToString("hh:mm tt")),Tuple.Create("التاريخ",created==DateTime.MinValue?Convert.ToString(order["date"]):created.ToString("M/d/yyyy"))})
                    {row=46*scale;float split=usable*.58f;g.DrawRectangle(pen,pad,y,usable,row);g.DrawLine(pen,pad+split,y,pad+split,y+row);text(meta.Item2,medium,pad,y,split,row);text(meta.Item1,small,pad+split,y,usable-split,row);y+=row;}y+=8;
                    if(deliveryH>0){g.DrawRectangle(pen,pad,y,usable,deliveryH);text("بيانات الدليفري",medium,pad,y,usable,52*scale);y+=52*scale;foreach(string value in customer)full(value);y+=8;}
                    float[] fractions=kitchen?new[]{.8f,1f}:new[]{.48f,.64f,.8f,1f};string[] headers=kitchen?new[]{"الصنف","الكمية"}:new[]{"الصنف","الكمية","السعر","الإجمالي"};
                    Action<string[],float> cells=(values,h)=>{float previous=0;for(int i=0;i<values.Length;i++){float x=pad+usable*previous,w=usable*(fractions[i]-previous);g.DrawRectangle(pen,x,y,w,h);text(values[i],i==0?small:small,x,y,w,h);previous=fractions[i];}y+=h;};
                    cells(headers,50*scale);for(int i=0;i<lines.Count;i++){var line=lines[i];string item=line.ItemName+(line.Variant!=""?" ("+line.Variant+")":"")+(line.Options.Count>0?" + "+string.Join(" + ",line.Options):"");cells(kitchen?new[]{item,line.Quantity.ToString()}:new[]{item,line.Quantity.ToString(),line.UnitPrice.ToString("0.##"),(line.UnitPrice*line.Quantity).ToString("0.##")},heights[i]);}
                    if(extras.Count>0){y+=8;foreach(string extra in extras)full(extra);}
                    if(notesH>0){y+=8;g.DrawRectangle(pen,pad,y,usable,notesH);text("ملاحظات",medium,pad,y,usable,52*scale);y+=52*scale;full(notes);}
                    if(!kitchen)
                    {
                        y+=8;decimal[] values={lines.Sum(l=>l.Subtotal),Convert.ToDecimal(order["delivery_fee_egp"]),Convert.ToDecimal(order["discount_egp"]),Convert.ToDecimal(order["total"])};string[] titles={"الإجمالي","خدمة التوصيل","الخصم","الصافي"};for(int i=0;i<4;i++){float split=usable*.58f;row=45*scale;g.DrawRectangle(pen,pad,y,usable,row);g.DrawLine(pen,pad+split,y,pad+split,y+row);text(values[i].ToString("0.##"),small,pad,y,split,row);text(titles[i],medium,pad+split,y,usable-split,row);y+=row;}y+=10;
                    }
                    full(settings.ReceiptAddress);full("شكراً");full(settings.ReceiptFooter);y+=12;
                    var result=canvas.Clone(new Rectangle(0,0,width,Math.Min(canvas.Height,(int)Math.Ceiling(y))),PixelFormat.Format24bppRgb);result.SetResolution(203,203);return result;
                }
            }
        }
    }
}
