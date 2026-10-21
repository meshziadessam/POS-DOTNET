using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace RestaurantPos
{
    public sealed class SalesSummary
    {
        public int Orders { get; set; }
        public int Items { get; set; }
        public decimal Net { get; set; }
        public decimal Discounts { get; set; }
        public decimal Delivery { get; set; }
        public decimal Expenses { get; set; }
        public decimal Gross { get { return Net+Discounts; } }
        public decimal AfterExpenses { get { return Net-Expenses; } }
        public override string ToString(){return "عدد الأوردرات: "+Orders+" | عدد الأصناف: "+Items+" | قبل الخصم: "+Gross.ToString("0.00")+" ج | الخصومات: "+Discounts.ToString("0.00")+" ج | دليفري للسائق: "+Delivery.ToString("0.00")+" ج\nصافي المطعم: "+Net.ToString("0.00")+" ج | الخزنة: "+Expenses.ToString("0.00")+" ج | بعد الخزنة: "+AfterExpenses.ToString("0.00")+" ج";}
    }

    public sealed class ReportService
    {
        private readonly Store store;
        public ReportService(Store store){this.store=store;}
        private static decimal Money(DataRow row,string column){return row[column]==DBNull.Value||Convert.ToString(row[column])==""?0:Convert.ToDecimal(row[column],CultureInfo.InvariantCulture);}
        public DataTable Orders(long? shift=null,string from=null,string to=null)
        {return store.Query("SELECT * FROM orders WHERE (@p0 IS NULL OR shift_id=@p0) AND (@p1 IS NULL OR date>=@p1) AND (@p2 IS NULL OR date<=@p2) ORDER BY CAST(order_id AS INTEGER) DESC",shift,from,to);}
        public SalesSummary Summary(long? shift=null,string from=null,string to=null)
        {
            var result=new SalesSummary();foreach(DataRow row in Orders(shift,from,to).Rows){result.Orders++;result.Items+=Store.ParseLines(Convert.ToString(row["items_json"])).Sum(l=>l.Quantity);result.Net+=Math.Max(0,Money(row,"total")-Money(row,"delivery_fee_egp"));result.Discounts+=Money(row,"discount_egp");result.Delivery+=Money(row,"delivery_fee_egp");}
            result.Expenses=Convert.ToDecimal(store.Query("SELECT COALESCE(SUM(CAST(amount_egp AS REAL)),0) AS total FROM expenses WHERE (@p0 IS NULL OR shift_id=@p0) AND (@p1 IS NULL OR substr(\"when\",1,10)>=@p1) AND (@p2 IS NULL OR substr(\"when\",1,10)<=@p2)",shift,from,to).Rows[0][0]);return result;
        }
        public DataTable ByItem(long? shift=null,string from=null,string to=null)
        {
            var result=new DataTable();result.Columns.Add("item");result.Columns.Add("qty",typeof(int));result.Columns.Add("line_total",typeof(decimal));var groups=new Dictionary<string,Tuple<int,decimal>>();
            foreach(DataRow row in Orders(shift,from,to).Rows)foreach(var line in Store.ParseLines(Convert.ToString(row["items_json"])))
            {if(string.IsNullOrWhiteSpace(line.ItemName))continue;Tuple<int,decimal> old;groups.TryGetValue(line.ItemName,out old);groups[line.ItemName]=Tuple.Create((old?.Item1??0)+line.Quantity,(old?.Item2??0)+(line.UnitPrice+line.ExtrasPrices.Sum())*line.Quantity);}
            foreach(var item in groups.OrderByDescending(g=>g.Value.Item1).ThenByDescending(g=>g.Value.Item2))result.Rows.Add(item.Key,item.Value.Item1,item.Value.Item2);return result;
        }
        public DataTable ByType(long? shift=null,string from=null,string to=null)
        {
            var result=new DataTable();result.Columns.Add("order_type");result.Columns.Add("orders",typeof(int));result.Columns.Add("total",typeof(decimal));result.Columns.Add("discounts",typeof(decimal));result.Columns.Add("delivery_fees",typeof(decimal));
            foreach(var group in Orders(shift,from,to).Rows.Cast<DataRow>().GroupBy(r=>Convert.ToString(r["order_type"])))result.Rows.Add(group.Key,group.Count(),group.Sum(r=>Math.Max(0,Money(r,"total")-Money(r,"delivery_fee_egp"))),group.Sum(r=>Money(r,"discount_egp")),group.Sum(r=>Money(r,"delivery_fee_egp")));return result;
        }
        public DataTable ByDriver(long? shift=null,string from=null,string to=null)
        {
            var result=new DataTable();result.Columns.Add("driver_id",typeof(long));result.Columns.Add("driver_name");result.Columns.Add("orders",typeof(int));result.Columns.Add("total",typeof(decimal));result.Columns.Add("delivery_fees",typeof(decimal));
            var rows=Orders(shift,from,to).Rows.Cast<DataRow>().Where(r=>long.TryParse(Convert.ToString(r["driver_id"]),out long id));
            foreach(var group in rows.GroupBy(r=>Tuple.Create(Convert.ToInt64(r["driver_id"]),Convert.ToString(r["driver_name"]))))result.Rows.Add(group.Key.Item1,group.Key.Item2,group.Count(),group.Sum(r=>Math.Max(0,Money(r,"total")-Money(r,"delivery_fee_egp"))),group.Sum(r=>Money(r,"delivery_fee_egp")));return result;
        }
        public DataTable Brief(long shift)
        {
            var result=new DataTable();result.Columns.Add("order_id",typeof(long));result.Columns.Add("created_at");result.Columns.Add("items");result.Columns.Add("total",typeof(decimal));foreach(DataRow row in Orders(shift).Rows)result.Rows.Add(Convert.ToInt64(row["order_id"]),row["created_at"],string.Join("، ",Store.ParseLines(Convert.ToString(row["items_json"])).Select(l=>l.ItemName)),Money(row,"total"));return result;
        }
        public DataTable SalesByDay(int days,DateTime? today=null)
        {
            if(days<1||days>366)throw new ArgumentOutOfRangeException(nameof(days));var end=(today??DateTime.Today).Date;var start=end.AddDays(1-days);var rows=Orders(null,start.ToString("yyyy-MM-dd")).Rows.Cast<DataRow>().GroupBy(r=>Convert.ToString(r["date"])).ToDictionary(g=>g.Key,g=>g.Sum(r=>Math.Max(0,Money(r,"total")-Money(r,"delivery_fee_egp"))));
            var table=new DataTable();table.Columns.Add("date");table.Columns.Add("total",typeof(decimal));for(int i=0;i<days;i++){string day=start.AddDays(i).ToString("yyyy-MM-dd");table.Rows.Add(day,rows.ContainsKey(day)?rows[day]:0);}return table;
        }
        public DataTable SalesByCategory(int days,DateTime? today=null)
        {
            string start=(today??DateTime.Today).Date.AddDays(1-days).ToString("yyyy-MM-dd");var categories=store.Query("SELECT name,category FROM catalog_items WHERE catalog_type='menu'").Rows.Cast<DataRow>().ToDictionary(r=>Convert.ToString(r["name"]),r=>string.IsNullOrWhiteSpace(Convert.ToString(r["category"]))?"غير مصنّف":Convert.ToString(r["category"]));var totals=new Dictionary<string,decimal>();
            foreach(DataRow order in Orders(null,start).Rows)foreach(var line in Store.ParseLines(Convert.ToString(order["items_json"])))
            {string category=categories.ContainsKey(line.ItemName)?categories[line.ItemName]:"غير مصنّف";if(!totals.ContainsKey(category))totals[category]=0;totals[category]+=line.UnitPrice*line.Quantity;}
            var table=new DataTable();table.Columns.Add("category");table.Columns.Add("total",typeof(decimal));foreach(var total in totals.OrderByDescending(p=>p.Value))table.Rows.Add(total.Key,total.Value);return table;
        }
        public List<string> ShiftLines(long shift,bool includeOrders)
        {
            var s=Summary(shift);var metadata=store.Query("SELECT start_at,end_at FROM shifts WHERE shift_id=@p0",shift);var lines=new List<string>{"تقرير وردية","رقم الوردية: "+shift};if(metadata.Rows.Count>0){lines.Add("وقت البداية: "+metadata.Rows[0][0]);lines.Add("وقت النهاية: "+metadata.Rows[0][1]);}
            lines.AddRange(new[]{"عدد الأوردرات: "+s.Orders,"عدد الأصناف: "+s.Items,"إجمالي قبل الخصم: "+s.Gross.ToString("0.00"),"مجموع الخصومات: "+s.Discounts.ToString("0.00"),"رسوم الدليفري للسائق: "+s.Delivery.ToString("0.00"),"إيرادات المطعم: "+s.Net.ToString("0.00"),"الخزنة: "+s.Expenses.ToString("0.00"),"الصافي بعد الخزنة: "+s.AfterExpenses.ToString("0.00"),"────────────────────","الأكثر مبيعًا"});
            foreach(DataRow row in ByItem(shift).Rows)lines.Add(row["item"]+" ×"+row["qty"]+" = "+Money(row,"line_total").ToString("0.00")+" ج");lines.Add("حسب نوع الطلب");foreach(DataRow row in ByType(shift).Rows)lines.Add(row["order_type"]+" | عدد "+row["orders"]+" | مبيعات "+Money(row,"total").ToString("0.00")+" | دليفري "+Money(row,"delivery_fees").ToString("0.00"));
            lines.Add("حسب السائق");foreach(DataRow row in ByDriver(shift).Rows)lines.Add(row["driver_name"]+" | عدد "+row["orders"]+" | مبيعات "+Money(row,"total").ToString("0.00")+" | دليفري "+Money(row,"delivery_fees").ToString("0.00"));
            if(includeOrders){lines.Add("قائمة مختصرة بالأوردرات");foreach(DataRow row in Brief(shift).Rows)lines.Add("#"+row["order_id"]+" | "+row["created_at"]+" | "+Money(row,"total").ToString("0.00")+" ج");}lines.Add("نهاية التقرير");return lines;
        }
        public List<string> DriverLines(long shift,long driver)
        {
            var rows=Orders(shift).Rows.Cast<DataRow>().Where(r=>Convert.ToString(r["order_type"])==Cart.Delivery&&Convert.ToString(r["driver_id"])==driver.ToString()).ToList();var lines=new List<string>{"إيصال طيار","الوردية: "+shift,"الطيار: "+(rows.Count>0?Convert.ToString(rows[0]["driver_name"]):driver.ToString()),"عدد الأوردرات: "+rows.Count,"إجمالي تكلفة الأوردرات: "+rows.Sum(r=>Math.Max(0,Money(r,"total")-Money(r,"delivery_fee_egp"))).ToString("0.00"),"إجمالي تكلفة الدليفري: "+rows.Sum(r=>Money(r,"delivery_fee_egp")).ToString("0.00")};foreach(var row in rows)lines.Add("#"+row["order_id"]+" | "+row["created_at"]+" | "+Money(row,"total").ToString("0.00"));lines.Add("نهاية الإيصال");return lines;
        }
    }
}
