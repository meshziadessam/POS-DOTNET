using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace RestaurantPos
{
    public sealed partial class Store
    {
        private SQLiteConnection OpenWrite()
        {
            if(!writable)throw new InvalidOperationException("هذه النسخة مفتوحة للقراءة فقط");
            var c=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=path,FailIfMissing=true,Pooling=false,DefaultTimeout=10}.ConnectionString);
            try { c.Open(); using(var cmd=new SQLiteCommand("PRAGMA foreign_keys=ON; PRAGMA busy_timeout=10000;",c))cmd.ExecuteNonQuery(); return c; } catch { c.Dispose(); throw; }
        }
        private static SQLiteCommand Command(SQLiteConnection c,SQLiteTransaction tx,string sql,params object[] values)
        {
            var command=new SQLiteCommand(sql,c,tx); for(int i=0;i<values.Length;i++)command.Parameters.AddWithValue("@p"+i,values[i]??DBNull.Value); return command;
        }
        private static int Execute(SQLiteConnection c,SQLiteTransaction tx,string sql,params object[] values) { using(var cmd=Command(c,tx,sql,values))return cmd.ExecuteNonQuery(); }
        private static object Scalar(SQLiteConnection c,SQLiteTransaction tx,string sql,params object[] values) { using(var cmd=Command(c,tx,sql,values))return cmd.ExecuteScalar(); }
        internal static string Now { get { return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture); } }
        public void Audit(string action,string detail) { using(var c=OpenWrite())Execute(c,null,"INSERT INTO audit(\"when\",action,detail) VALUES(@p0,@p1,@p2)",Now,action,detail); }
        public long ActiveShift { get { return Convert.ToInt64(Query("SELECT shift_id FROM shifts WHERE status='open' ORDER BY shift_id DESC LIMIT 1").Rows[0][0]); } }
        public DataTable Orders(bool waitingOnly=false) { return Query("SELECT rowid AS _rowid,* FROM orders WHERE (@p0=0 OR status='waiting') ORDER BY CAST(shift_id AS INTEGER) DESC,CAST(order_id AS INTEGER) DESC",waitingOnly?1:0); }
        public DataRow Order(long shift,long id) { var t=Query("SELECT * FROM orders WHERE shift_id=@p0 AND order_id=@p1",shift,id); return t.Rows.Count==1?t.Rows[0]:null; }
        public DataTable Variants(string name) { return Query("SELECT variant_name,price FROM item_variants WHERE item_name=@p0 ORDER BY sort_order",name); }
        public DataTable Options() { return Query("SELECT option FROM item_options ORDER BY sort_order"); }
        public DataTable ShiftReport() { return Query("SELECT s.shift_id,s.start_at,s.end_at,s.status,COUNT(o.order_id) AS orders_count,COALESCE(SUM(CAST(o.total AS REAL)-CAST(COALESCE(o.delivery_fee_egp,0) AS REAL)),0) AS net_after_discount,COALESCE(SUM(CAST(o.discount_egp AS REAL)),0) AS discounts_total,COALESCE(SUM(CAST(o.delivery_fee_egp AS REAL)),0) AS delivery_fee_total FROM shifts s LEFT JOIN orders o ON s.shift_id=o.shift_id GROUP BY s.shift_id ORDER BY s.shift_id DESC"); }
        public DataTable ByType() { return Query("SELECT order_type,COUNT(*) AS orders_count,SUM(CAST(total AS REAL)-CAST(delivery_fee_egp AS REAL)) AS sales,SUM(CAST(delivery_fee_egp AS REAL)) AS delivery_fees FROM orders GROUP BY order_type"); }
        public void StartShift()
        {
            using(var c=OpenWrite())using(var tx=c.BeginTransaction())
            {
                if(Convert.ToInt64(Scalar(c,tx,"SELECT COUNT(*) FROM orders WHERE status='waiting' AND shift_id IN (SELECT shift_id FROM shifts WHERE status='open')"))>0)throw new InvalidOperationException("أتمم أو ألغِ أوردرات الانتظار قبل إنهاء الوردية");
                Execute(c,tx,"UPDATE shifts SET status='closed',end_at=@p0 WHERE status='open'",Now);
                long id=Convert.ToInt64(Scalar(c,tx,"SELECT COALESCE(MAX(shift_id),0)+1 FROM shifts"));
                Execute(c,tx,"INSERT INTO shifts(shift_id,start_at,end_at,status) VALUES(@p0,@p1,'','open')",id,Now);
                Execute(c,tx,"INSERT INTO audit(\"when\",action,detail) VALUES(@p0,'shift_start',@p1)",Now,id.ToString()); tx.Commit();
            }
        }
        public DataRow SaveOrder(Cart cart,string cashier,string payment,decimal paid,bool waiting)
        {
            if(cart.Lines.Count==0)throw new ArgumentException("الأوردر فاضي");
            if(cart.Discount<0||cart.DeliveryFee<0||cart.Lines.Any(l=>l.Quantity<1||l.Quantity>999||l.UnitPrice<0||l.LineDiscount<0||l.ExtrasPrices.Any(p=>p<0)))throw new ArgumentException("راجع الأسعار والكميات والخصومات");
            if(cart.OrderType==Cart.Delivery&&(string.IsNullOrWhiteSpace(cart.CustomerPhone)||string.IsNullOrWhiteSpace(cart.CustomerAddress)))throw new ArgumentException("الدليفري يتطلب عميل وعنوان");
            if(!waiting&&cart.OrderType==Cart.Delivery&&(string.IsNullOrWhiteSpace(cart.DriverName)||string.IsNullOrWhiteSpace(cart.DriverPhone)))throw new ArgumentException("اختار الطيار ورقم تليفونه");
            if(!waiting&&(paid<cart.Total||(payment!="كاش"&&payment!="فودافون كاش / تحويل")))throw new ArgumentException("راجع طريقة الدفع؛ المدفوع أقل من الإجمالي");
            long sid,id;
            using(var c=OpenWrite())using(var tx=c.BeginTransaction())
            {
                using(var cmd=Command(c,tx,"SELECT shift_id,order_id FROM save_tokens WHERE token=@p0",cart.SaveToken))using(var r=cmd.ExecuteReader())
                    if(r.Read()){sid=Convert.ToInt64(r[0]);id=Convert.ToInt64(r[1]);r.Close();tx.Rollback();return Order(sid,id)??throw new InvalidOperationException("تمت معالجة هذا الطلب سابقًا ثم إلغاؤه");}
                sid=Convert.ToInt64(Scalar(c,tx,"SELECT shift_id FROM shifts WHERE status='open' ORDER BY shift_id DESC LIMIT 1"));
                if(cart.EditingShift.HasValue&&cart.EditingShift!=sid)throw new InvalidOperationException("لا يمكن تعديل أوردر وردية سابقة");
                if(cart.EditingOrder.HasValue)id=cart.EditingOrder.Value;
                else
                {
                    string key="last_order_id_shift_"+sid;
                    long last=Convert.ToInt64(Scalar(c,tx,"SELECT COALESCE((SELECT CAST(value AS INTEGER) FROM session WHERE key=@p0),0)",key));
                    last=Math.Max(last,Convert.ToInt64(Scalar(c,tx,"SELECT COALESCE(MAX(CAST(order_id AS INTEGER)),0) FROM orders WHERE shift_id=@p0",sid)));
                    last=Math.Max(last,Convert.ToInt64(Scalar(c,tx,"SELECT COALESCE(MAX(order_id),0) FROM save_tokens WHERE shift_id=@p0",sid)));
                    id=last+1;Execute(c,tx,"INSERT OR REPLACE INTO session(key,value) VALUES(@p0,@p1)",key,id.ToString());
                }
                var previous=new Dictionary<string,decimal>(); bool previousCompleted=false; string created=Now;
                if(cart.EditingOrder.HasValue)
                {
                    using(var cmd=Command(c,tx,"SELECT status,items_json,created_at FROM orders WHERE shift_id=@p0 AND order_id=@p1",sid,id))using(var r=cmd.ExecuteReader())
                    {
                        if(!r.Read())throw new InvalidOperationException("الأوردر لم يعد موجودًا");
                        previousCompleted=Convert.ToString(r[0])=="completed"; created=Convert.ToString(r[2]);
                        if(previousCompleted)foreach(var line in ParseLines(Convert.ToString(r[1]))) { if(!previous.ContainsKey(line.ItemName))previous[line.ItemName]=0;previous[line.ItemName]+=line.Quantity; }
                    }
                    Execute(c,tx,"DELETE FROM orders WHERE shift_id=@p0 AND order_id=@p1",sid,id);
                }
                string orderType=cart.OrderType=="صالة"?(cart.DineSubtype=="شركات"&&cart.CompanyName!=""?cart.CompanyName:cart.DineSubtype):cart.OrderType;
                var values=new Dictionary<string,object>{{"order_id",id},{"shift_id",sid},{"shift_start_at",Scalar(c,tx,"SELECT start_at FROM shifts WHERE shift_id=@p0",sid)},
                    {"date",created.Substring(0,10)},{"hour",created.Substring(11,2)},{"created_at",created},{"order_type",orderType},{"dine_subtype",cart.OrderType=="صالة"?cart.DineSubtype:""},
                    {"items_json",SerializeLines(cart.Lines)},{"discount_egp",cart.Discount},{"delivery_fee_egp",cart.OrderType==Cart.Delivery?cart.DeliveryFee:0},{"payment_method",waiting?"":payment},{"paid",waiting?0:paid},{"total",cart.Total},
                    {"customer_phone",cart.CustomerPhone},{"customer_name",cart.CustomerName},{"customer_address",cart.CustomerAddress},{"customer_notes",cart.CustomerNotes},{"order_notes",cart.Notes},{"company_name",cart.CompanyName},
                    {"driver_id",cart.DriverId},{"driver_name",cart.DriverName},{"driver_phone",cart.DriverPhone},{"notes_options_json","[]"},{"driver_notified","False"},{"status",waiting?"waiting":"completed"},{"cashier_name",cashier},{"table_no",cart.TableNumber}};
                Insert(c,tx,"orders",values);
                foreach(long merged in cart.MergedWaiting)
                {
                    object mergedTable=Scalar(c,tx,"SELECT table_no FROM orders WHERE shift_id=@p0 AND order_id=@p1 AND status='waiting'",sid,merged);
                    if(Execute(c,tx,"DELETE FROM orders WHERE shift_id=@p0 AND order_id=@p1 AND status='waiting'",sid,merged)!=1)throw new InvalidOperationException("أحد أوردرات الدمج تغيّر. أعد تحميل الانتظار.");
                    Execute(c,tx,"UPDATE restaurant_tables SET status='free' WHERE table_no=@p0",mergedTable);
                }
                if(!waiting||previousCompleted)
                {
                    var next=waiting?new Dictionary<string,decimal>():cart.Lines.GroupBy(l=>l.ItemName).ToDictionary(g=>g.Key,g=>g.Sum(l=>(decimal)l.Quantity));
                    foreach(string item in previous.Keys.Union(next.Keys))
                    { decimal oldQty=previous.ContainsKey(item)?previous[item]:0,newQty=next.ContainsKey(item)?next[item]:0;
                        Execute(c,tx,"UPDATE catalog_items SET stock_qty=stock_qty+@p0 WHERE catalog_type='menu' AND name=@p1 AND stock_qty IS NOT NULL",oldQty-newQty,item); }
                }
                if(cart.TableNumber.HasValue)Execute(c,tx,"UPDATE restaurant_tables SET status=@p0 WHERE table_no=@p1",waiting?"occupied":"free",cart.TableNumber.Value);
                Execute(c,tx,"INSERT INTO audit(\"when\",action,detail) VALUES(@p0,@p1,@p2)",Now,waiting?"order_waiting":"order_save","shift="+sid+" / order="+id);
                Execute(c,tx,"INSERT INTO save_tokens(token,shift_id,order_id) VALUES(@p0,@p1,@p2)",cart.SaveToken,sid,id); tx.Commit();
            }
            return Order(sid,id);
        }
        private static void Insert(SQLiteConnection c,SQLiteTransaction tx,string table,Dictionary<string,object> values)
        { string columns=string.Join(",",values.Keys.Select(k=>"\""+k+"\"")); string parameters=string.Join(",",Enumerable.Range(0,values.Count).Select(i=>"@p"+i)); Execute(c,tx,"INSERT INTO "+table+"("+columns+") VALUES("+parameters+")",values.Values.ToArray()); }
        public static string SerializeLines(IEnumerable<CartLine> lines) { return new JavaScriptSerializer().Serialize(lines.Select(l=>new {item=l.ItemName,price=l.UnitPrice,qty=l.Quantity,extras=l.Extras,extras_prices=l.ExtrasPrices,options=l.Options,variant=l.Variant,line_discount_egp=l.LineDiscount}).ToArray()); }
        public static List<CartLine> ParseLines(string json)
        {
            var rows=new JavaScriptSerializer().Deserialize<List<Dictionary<string,object>>>(json);var lines=new List<CartLine>();
            foreach(var row in rows)
            {
                var line=new CartLine{ItemName=Convert.ToString(row["item"]),UnitPrice=Convert.ToDecimal(row["price"],CultureInfo.InvariantCulture),Quantity=Convert.ToInt32(row["qty"])};
                if(row.ContainsKey("variant"))line.Variant=Convert.ToString(row["variant"]);
                if(row.ContainsKey("line_discount_egp"))line.LineDiscount=Convert.ToDecimal(row["line_discount_egp"],CultureInfo.InvariantCulture);
                if(row.ContainsKey("options")&&row["options"]!=null)line.Options=((System.Collections.IEnumerable)row["options"]).Cast<object>().Select(Convert.ToString).ToList();
                if(row.ContainsKey("extras")&&row["extras"]!=null)line.Extras=((System.Collections.IEnumerable)row["extras"]).Cast<object>().Select(Convert.ToString).ToList();
                if(row.ContainsKey("extras_prices")&&row["extras_prices"]!=null)line.ExtrasPrices=((System.Collections.IEnumerable)row["extras_prices"]).Cast<object>().Select(v=>Convert.ToDecimal(v,CultureInfo.InvariantCulture)).ToList();
                lines.Add(line);
            }return lines;
        }
        public void DeleteWaiting(long sid,long id)
        {
            using(var c=OpenWrite())using(var tx=c.BeginTransaction())
            {
                object table=Scalar(c,tx,"SELECT table_no FROM orders WHERE shift_id=@p0 AND order_id=@p1 AND status='waiting'",sid,id);
                if(Execute(c,tx,"DELETE FROM orders WHERE shift_id=@p0 AND order_id=@p1 AND status='waiting'",sid,id)!=1)throw new InvalidOperationException("أوردر الانتظار غير موجود");
                Execute(c,tx,"UPDATE restaurant_tables SET status='free' WHERE table_no=@p0",table);Execute(c,tx,"INSERT INTO audit(\"when\",action,detail) VALUES(@p0,'waiting_cancel',@p1)",Now,id.ToString());tx.Commit();
            }
        }
        private static readonly string[] EditableTables={"catalog_items","categories","item_options","item_variants","customers","drivers","companies","restaurant_tables","expenses"};
        public DataTable Records(string table)
        {
            if(!EditableTables.Contains(table)&&table!="users"&&table!="audit"&&table!="shift_batch_archives")throw new ArgumentException("جدول غير مدعوم");
            return Query(table=="users"?"SELECT rowid AS _rowid,user_id,name,role,active FROM users ORDER BY user_id":"SELECT rowid AS _rowid,* FROM \""+table+"\"");
        }
        public void SaveRecord(string table,Dictionary<string,object> values,long? rowid)
        {
            if(!EditableTables.Contains(table))throw new ArgumentException("جدول غير مدعوم");
            var columns=Query("PRAGMA table_info(\""+table+"\")").Rows.Cast<DataRow>().Select(r=>Convert.ToString(r["name"])).ToArray();
            if(values.Keys.Any(k=>!columns.Contains(k)))throw new ArgumentException("حقل غير مدعوم");
            if(values.ContainsKey("name")&&string.IsNullOrWhiteSpace(Convert.ToString(values["name"])))throw new ArgumentException("الاسم مطلوب");
            if(table=="customers"||table=="drivers"||table=="companies")
            {
                if(values.ContainsKey("name")){string name=Convert.ToString(values["name"]);if(name.Length<2||name.Any(char.IsDigit))throw new ArgumentException("الاسم لا يقل عن حرفين ولا يحتوي أرقام");}
                if(values.ContainsKey("phone"))
                {
                    string phone=System.Text.RegularExpressions.Regex.Replace(Convert.ToString(values["phone"]),@"\D","");
                    if(!phone.StartsWith("0")&&phone.Length==12&&phone.StartsWith("20"))phone=phone.Substring(2);
                    if(phone.Length==10&&phone.StartsWith("1"))phone="0"+phone;
                    if(phone.Length==11&&phone.StartsWith("1"))phone="0"+phone.Substring(1);
                    if(phone!=""&&!System.Text.RegularExpressions.Regex.IsMatch(phone,@"^01[0-9]{9}$")||table=="customers"&&phone=="")throw new ArgumentException("رقم التليفون غير صحيح؛ أدخل رقمًا مصريًا يبدأ بـ01");
                    values["phone"]=phone;
                }
            }
            foreach(string key in new[]{"price","low_stock_threshold","amount_egp"})if(values.ContainsKey(key)&&Convert.ToDecimal(values[key]??0)<0)throw new ArgumentException("القيمة لا يمكن أن تكون سالبة");
            if(table=="expenses"&&(!values.ContainsKey("who")||string.IsNullOrWhiteSpace(Convert.ToString(values["who"]))||!values.ContainsKey("amount_egp")||Convert.ToDecimal(values["amount_egp"])<=0))throw new ArgumentException("المستلم مطلوب والمبلغ يجب أن يكون أكبر من صفر");
            if(table=="item_options"&&(!values.ContainsKey("option")||string.IsNullOrWhiteSpace(Convert.ToString(values["option"]))))throw new ArgumentException("اكتب اسم الخيار");
            if(table=="item_variants"&&(!values.ContainsKey("variant_name")||string.IsNullOrWhiteSpace(Convert.ToString(values["variant_name"]))))throw new ArgumentException("اكتب اسم التنويع");
            if(table=="restaurant_tables"&&values.ContainsKey("table_no")&&(Convert.ToInt32(values["table_no"])<1||Convert.ToInt32(values["table_no"])>999))throw new ArgumentException("رقم الطاولة بين 1 و999");
            if(table=="restaurant_tables"&&values.ContainsKey("status")&&!new[]{"free","occupied"}.Contains(Convert.ToString(values["status"])))throw new ArgumentException("حالة الطاولة غير صحيحة");
            using(var c=OpenWrite())using(var tx=c.BeginTransaction())
            {
                if(table=="item_variants"&&Convert.ToInt64(Scalar(c,tx,"SELECT COUNT(*) FROM catalog_items WHERE catalog_type='menu' AND name=@p0",values.ContainsKey("item_name")?values["item_name"]:""))!=1)throw new ArgumentException("الصنف غير موجود");
                if((table=="drivers"||table=="companies")&&values.ContainsKey("name")&&Convert.ToInt64(Scalar(c,tx,"SELECT COUNT(*) FROM "+table+" WHERE name=@p0 AND rowid<>@p1",values["name"],rowid??-1))>0)throw new ArgumentException("الاسم مسجل بالفعل");
                if((table=="customers"||table=="drivers"||table=="companies")&&values.ContainsKey("phone")&&Convert.ToString(values["phone"])!=""&&Convert.ToInt64(Scalar(c,tx,"SELECT COUNT(*) FROM "+table+" WHERE phone=@p0 AND rowid<>@p1",values["phone"],rowid??-1))>0)throw new ArgumentException("رقم التليفون مسجل بالفعل");
                if(!rowid.HasValue)
                {
                    if(columns.Contains("sort_order")&&!values.ContainsKey("sort_order"))values["sort_order"]=Convert.ToInt64(Scalar(c,tx,"SELECT COALESCE(MAX(sort_order),-1)+1 FROM "+table));
                    if(table=="drivers"||table=="companies") {string key=table=="drivers"?"driver_id":"company_id"; values[key]=Convert.ToInt64(Scalar(c,tx,"SELECT COALESCE(MAX("+key+"),0)+1 FROM "+table));}
                    if(table=="catalog_items")values["catalog_type"]="menu";
                    if(table=="item_variants")values["catalog_type"]="menu";
                    if(table=="expenses"){values["shift_id"]=ActiveShift;values["when"]=Now;}
                    Insert(c,tx,table,values);
                }
                else
                {
                    if((table=="catalog_items"||table=="categories")&&values.ContainsKey("name"))
                    {
                        object oldName=Scalar(c,tx,"SELECT name FROM "+table+" WHERE rowid=@p0",rowid.Value);
                        if(table=="catalog_items")Execute(c,tx,"UPDATE item_variants SET item_name=@p0 WHERE item_name=@p1",values["name"],oldName);
                        else Execute(c,tx,"UPDATE catalog_items SET category=@p0 WHERE category=@p1",values["name"],oldName);
                    }
                    var all=values.Values.Concat(new object[]{rowid.Value}).ToArray();
                    string sets=string.Join(",",values.Keys.Select((k,i)=>"\""+k+"\"=@p"+i));
                    Execute(c,tx,"UPDATE \""+table+"\" SET "+sets+" WHERE rowid=@p"+values.Count,all);
                }
                if(table=="catalog_items"&&values.ContainsKey("category")&&!string.IsNullOrWhiteSpace(Convert.ToString(values["category"])))Execute(c,tx,"INSERT OR IGNORE INTO categories(name,sort_order,station) VALUES(@p0,0,'')",values["category"]);
                Execute(c,tx,"INSERT INTO audit(\"when\",action,detail) VALUES(@p0,'data_save',@p1)",Now,table);tx.Commit();
            }
        }
        public void DeleteRecord(string table,long rowid)
        {
            if(!EditableTables.Contains(table)&&table!="users")throw new ArgumentException("جدول غير مدعوم");
            using(var c=OpenWrite())using(var tx=c.BeginTransaction())
            {
                if(table=="catalog_items"){object name=Scalar(c,tx,"SELECT name FROM catalog_items WHERE rowid=@p0",rowid);Execute(c,tx,"DELETE FROM item_variants WHERE catalog_type='menu' AND item_name=@p0",name);}
                if(table=="users")ProtectLastManager(c,tx,rowid,false,"cashier");
                if(table=="categories"){object name=Scalar(c,tx,"SELECT name FROM categories WHERE rowid=@p0",rowid);Execute(c,tx,"UPDATE catalog_items SET category='' WHERE category=@p0",name);}
                if(table=="restaurant_tables"&&Convert.ToString(Scalar(c,tx,"SELECT status FROM restaurant_tables WHERE rowid=@p0",rowid))=="occupied")throw new ArgumentException("لا يمكن حذف طاولة مشغولة؛ أنهِ الأوردر أو حرّر الطاولة أولاً");
                Execute(c,tx,"DELETE FROM \""+table+"\" WHERE rowid=@p0",rowid);Execute(c,tx,"INSERT INTO audit(\"when\",action,detail) VALUES(@p0,'data_delete',@p1)",Now,table);tx.Commit();
            }
        }
        public void ReserveTable(int table)
        {using(var c=OpenWrite())if(Execute(c,null,"UPDATE restaurant_tables SET status='occupied' WHERE table_no=@p0 AND status='free'",table)!=1)throw new InvalidOperationException("هذه الطاولة مشغولة بالفعل أو لم تعد موجودة");}
        public void ReleaseTable(int table)
        {using(var c=OpenWrite())Execute(c,null,"UPDATE restaurant_tables SET status='free' WHERE table_no=@p0 AND NOT EXISTS(SELECT 1 FROM orders WHERE table_no=@p0 AND status='waiting')",table);}
        public void SaveUser(string name,string pin,string role,bool active,long? rowid)
        {
            if(pin.Length>0&&string.IsNullOrWhiteSpace(pin))throw new ArgumentException("كلمة المرور لا يمكن أن تكون مسافات فقط");
            if(role!="cashier"&&role!="manager")throw new ArgumentException("الصلاحية تكون cashier أو manager");
            if(string.IsNullOrWhiteSpace(name)||(!rowid.HasValue&&pin.Length<4)||pin.Length>0&&pin.Length<4)throw new ArgumentException("الاسم مطلوب والرقم السري 4 أحرف أو أرقام على الأقل");
            var values=new Dictionary<string,object>{{"name",name},{"role",role},{"active",active?1:0}};
            if(pin.Length>0)
            {
                var salt=new byte[16]; using(var rng=System.Security.Cryptography.RandomNumberGenerator.Create())rng.GetBytes(salt);
                using(var kdf=new System.Security.Cryptography.Rfc2898DeriveBytes(pin,salt,240000,System.Security.Cryptography.HashAlgorithmName.SHA256))
                { values["pin_salt"]=Convert.ToBase64String(salt);values["pin_hash"]=Convert.ToBase64String(kdf.GetBytes(32));values["pin_iterations"]=240000; }
            }
            using(var c=OpenWrite())using(var tx=c.BeginTransaction())
            {
                if(rowid.HasValue)ProtectLastManager(c,tx,rowid.Value,active,role);
                if(rowid.HasValue){string sets=string.Join(",",values.Keys.Select((k,i)=>"\""+k+"\"=@p"+i));Execute(c,tx,"UPDATE users SET "+sets+" WHERE rowid=@p"+values.Count,values.Values.Concat(new object[]{rowid.Value}).ToArray());}
                else Insert(c,tx,"users",values);
                Execute(c,tx,"INSERT INTO audit(\"when\",action,detail) VALUES(@p0,'user_save',@p1)",Now,name);tx.Commit();
            }
        }
        private static void ProtectLastManager(SQLiteConnection c,SQLiteTransaction tx,long id,bool active,string role)
        {
            if(active&&role=="manager")return;
            if(Convert.ToInt64(Scalar(c,tx,"SELECT COUNT(*) FROM users WHERE rowid=@p0 AND role='manager' AND active=1",id))>0&&Convert.ToInt64(Scalar(c,tx,"SELECT COUNT(*) FROM users WHERE role='manager' AND active=1"))<=1)throw new InvalidOperationException("لا يمكن حذف أو تعطيل آخر مدير مفعّل أو تغيير صلاحيته؛ أضف مديرًا آخر أولاً");
        }
        public void CreateInitialManager(string name,string password)
        {
            if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(password)||password.Length<4)throw new ArgumentException("اسم المدير مطلوب وكلمة المرور 4 أحرف أو أرقام على الأقل");
            var salt=new byte[16];using(var rng=System.Security.Cryptography.RandomNumberGenerator.Create())rng.GetBytes(salt);
            using(var kdf=new System.Security.Cryptography.Rfc2898DeriveBytes(password,salt,240000,System.Security.Cryptography.HashAlgorithmName.SHA256))
            using(var c=OpenWrite())using(var tx=c.BeginTransaction())
            {
                if(Convert.ToInt64(Scalar(c,tx,"SELECT COUNT(*) FROM users"))!=0)throw new InvalidOperationException("تم إعداد الحسابات بالفعل؛ سجّل الدخول بحساب المدير");
                Execute(c,tx,"INSERT INTO users(name,pin_salt,pin_hash,pin_iterations,role,active) VALUES(@p0,@p1,@p2,240000,'manager',1)",name.Trim(),Convert.ToBase64String(salt),Convert.ToBase64String(kdf.GetBytes(32)));
                Execute(c,tx,"INSERT INTO audit(\"when\",action,detail) VALUES(@p0,'initial_manager',@p1)",Now,name.Trim());tx.Commit();
            }
        }
    }
}
