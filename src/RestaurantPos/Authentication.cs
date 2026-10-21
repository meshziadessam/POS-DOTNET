using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace RestaurantPos
{
    public sealed class UserSession
    {
        public long UserId { get; private set; }
        public string Name { get; private set; }
        public string Role { get; private set; }
        public bool IsManager { get { return Role=="manager"; } }
        public UserSession(string name,string role,long userId=0)
        {if(role!="manager"&&role!="cashier")throw new ArgumentException("صلاحية غير صالحة");Name=name;Role=role;UserId=userId;}
        public static UserSession FromUser(DataRow user)
        {if(user==null)throw new UnauthorizedAccessException("سجّل الدخول أولاً");return new UserSession(Convert.ToString(user["name"]),Convert.ToString(user["role"]),Convert.ToInt64(user["user_id"]));}
        public void RequireManager(Store store)
        {if(!IsManager||(UserId>0&&store.Query("SELECT user_id FROM users WHERE user_id=@p0 AND active=1 AND role='manager'",UserId).Rows.Count!=1))throw new UnauthorizedAccessException("هذه العملية متاحة للمدير فقط؛ أعد تسجيل الدخول إذا تغيرت الصلاحية");}
    }

    public sealed class InitialManagerForm : Form
    {
        public InitialManagerForm(Store store)
        {
            Text="إعداد حساب المدير";ClientSize=new Size(570,410);MinimumSize=new Size(520,440);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",11);RightToLeft=RightToLeft.Yes;AutoScaleMode=AutoScaleMode.Dpi;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=3};root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(new Label{Text="أنشئ حساب المدير لبدء استخدام البرنامج.\nيمكن للمدير إضافة الموظفين لاحقًا من الإعدادات ← المستخدمون.",AutoSize=true,MaximumSize=new Size(520,0),Margin=new Padding(0,0,0,20)},0,0);
            var name=new TextBox{Dock=DockStyle.Fill};var password=new TextBox{Dock=DockStyle.Fill,UseSystemPasswordChar=true};var confirm=new TextBox{Dock=DockStyle.Fill,UseSystemPasswordChar=true};
            var fields=Ui.Fields(new[]{"اسم المدير","كلمة المرور","تأكيد كلمة المرور"},new Control[]{name,password,confirm});root.Controls.Add(fields,0,1);
            var save=Ui.Button("إنشاء حساب المدير",()=>{if(password.Text!=confirm.Text)throw new ArgumentException("كلمتا المرور غير متطابقتين");store.CreateInitialManager(name.Text.Trim(),password.Text);DialogResult=DialogResult.OK;},"success");root.Controls.Add(Ui.Row(save,Ui.Button("إلغاء",()=>DialogResult=DialogResult.Cancel,"default")),0,2);Controls.Add(root);AcceptButton=save;Ui.Theme(this,false);
        }
    }

    public sealed class LoginForm : Form
    {
        public UserSession Session { get; private set; }
        public LoginForm(Store store)
        {
            Text="تسجيل الدخول — Restaurant POS";ClientSize=new Size(510,340);MinimumSize=new Size(480,360);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",12);AutoScaleMode=AutoScaleMode.Dpi;RightToLeft=RightToLeft.Yes;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(26),ColumnCount=1,RowCount=4};root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(new Label{Text="مرحبًا بك\nاختر حسابك وأدخل كلمة المرور",AutoSize=true,Font=new Font(Font.FontFamily,17,FontStyle.Bold),Margin=new Padding(0,0,0,20)},0,0);
            var users=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,DataSource=store.Users(),DisplayMember="name"};var password=new TextBox{Dock=DockStyle.Fill,UseSystemPasswordChar=true};root.Controls.Add(Ui.Fields(new[]{"المستخدم","كلمة المرور"},new Control[]{users,password}),0,1);
            var error=new Label{AutoSize=true,ForeColor=Color.Salmon,MaximumSize=new Size(450,0),Margin=new Padding(0,8,0,8)};root.Controls.Add(error,0,2);
            var enter=Ui.Button("دخول",()=>{},"success");FormClosing+=(s,e)=>{if(!enter.Enabled&&Session==null)e.Cancel=true;};enter.Click+=async(s,e)=>{string loginName=users.Text,loginPassword=password.Text;enter.Enabled=false;try{var user=await System.Threading.Tasks.Task.Run(()=>store.Authenticate(loginName,loginPassword));if(user==null){error.Text="اسم المستخدم أو كلمة المرور غير صحيحة";password.Clear();password.Focus();return;}Session=UserSession.FromUser(user);store.Audit("login",Session.Name);DialogResult=DialogResult.OK;}catch(Exception ex){error.Text=ex.Message;}finally{enter.Enabled=true;}};
            root.Controls.Add(Ui.Row(enter,Ui.Button("خروج",Close,"default")),0,3);Controls.Add(root);AcceptButton=enter;Ui.Theme(this,new PosSettings(store.DataRoot).Theme=="light");error.ForeColor=Color.Salmon;
        }
    }
}
