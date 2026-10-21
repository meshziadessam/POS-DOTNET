using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace RestaurantPos
{
    public sealed partial class Store
    {
        // Preserve an existing manager password when upgrading the old settings-only gate.
        // The password hash is copied; no password is decrypted or reset.
        public bool MigrateLegacyManager()
        {
            string file=Path.Combine(DataRoot,"settings","manager_password.json");if(!File.Exists(file)||Query("SELECT user_id FROM users WHERE role='manager' LIMIT 1").Rows.Count>0)return false;
            var values=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(file));
            string salt=Convert.ToString(values["salt"]),digest=Convert.ToString(values["digest"]);int iterations=Convert.ToInt32(values["iterations"]);
            if(Convert.FromBase64String(salt).Length<16||Convert.FromBase64String(digest).Length!=32||iterations<10000||iterations>1000000)throw new InvalidDataException("ملف كلمة سر المدير القديمة غير صالح؛ استرجع نسخة احتياطية سليمة");
            using(var c=OpenWrite())using(var tx=c.BeginTransaction())
            {
                if(Convert.ToInt64(Scalar(c,tx,"SELECT COUNT(*) FROM users WHERE role='manager'"))>0)return false;
                string name="المدير";int suffix=1;while(Convert.ToInt64(Scalar(c,tx,"SELECT COUNT(*) FROM users WHERE name=@p0",name))>0)name="المدير "+(++suffix);
                Execute(c,tx,"INSERT INTO users(name,pin_salt,pin_hash,pin_iterations,role,active) VALUES(@p0,@p1,@p2,@p3,'manager',1)",name,salt,digest,iterations);
                Execute(c,tx,"INSERT INTO audit(\"when\",action,detail) VALUES(@p0,'legacy_manager_migrated',@p1)",Now,name);tx.Commit();return true;
            }
        }
    }
}
