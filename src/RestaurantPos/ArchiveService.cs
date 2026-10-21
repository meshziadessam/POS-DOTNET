using System;
using System.Data;
using System.Linq;

namespace RestaurantPos
{
    internal static class ArchiveService { public static void Update(Store store){store.UpdateArchives();} }
    public sealed partial class Store
    {
        public void UpdateArchives()
        {
            var shifts=Query("SELECT * FROM shifts WHERE status='closed' ORDER BY shift_id").Rows.Cast<DataRow>().ToList();
            for(int offset=0;offset+15<=shifts.Count;offset+=15)
            {
                var batch=shifts.Skip(offset).Take(15).ToList();string key=Convert.ToInt64(batch[0]["shift_id"]).ToString("000")+"-"+Convert.ToInt64(batch[14]["shift_id"]).ToString("000");
                using(var c=OpenWrite())using(var tx=c.BeginTransaction())
                {
                    if(Convert.ToInt64(Scalar(c,tx,"SELECT COUNT(*) FROM shift_batch_archives WHERE archive_key=@p0",key))>0){tx.Rollback();continue;}
                    foreach(var shift in batch)
                    {
                        long sid=Convert.ToInt64(shift["shift_id"]);decimal net=0,discount=0,delivery=0;int orders=0,items=0;
                        using(var cmd=Command(c,tx,"SELECT total,discount_egp,delivery_fee_egp,items_json FROM orders WHERE shift_id=@p0",sid))using(var r=cmd.ExecuteReader())
                            while(r.Read()){net+=Math.Max(0,Convert.ToDecimal(r[0])-Convert.ToDecimal(r[2]));discount+=Convert.ToDecimal(r[1]);delivery+=Convert.ToDecimal(r[2]);orders++;items+=ParseLines(Convert.ToString(r[3])).Sum(l=>l.Quantity);}
                        decimal vault=Convert.ToDecimal(Scalar(c,tx,"SELECT COALESCE(SUM(CAST(amount_egp AS REAL)),0) FROM expenses WHERE shift_id=@p0",sid));
                        Execute(c,tx,"INSERT INTO shift_batch_archives(archive_key,shift_id,start_at,end_at,orders_count,items_count,gross_sales,discounts_total,delivery_fee_total,net_after_discount,vault_total,net_after_vault) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11)",key,sid,shift["start_at"],shift["end_at"],orders,items,net+discount,discount,delivery,net,vault,net-vault);
                    }
                    tx.Commit();
                }
            }
        }
    }
}
