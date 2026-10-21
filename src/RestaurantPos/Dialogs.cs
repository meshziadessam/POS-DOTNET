using System;
using System.Drawing;
using System.Windows.Forms;

namespace RestaurantPos
{
    internal static class Dialogs
    {
        public static DialogResult Show(string text,string title="تنبيه",MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None)
        {return Show(Form.ActiveForm,text,title,buttons,icon);}
        public static DialogResult Show(IWin32Window owner,string text,string title="تنبيه",MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None)
        {
            using(var form=Create(text,title,buttons,Ui.IsLight(owner as Control)))return form.ShowDialog(owner);
        }
        internal static Form Create(string text,string title,MessageBoxButtons buttons,bool light)
        {
            var form=Ui.Form(title,610,300);form.MinimumSize=new Size(450,250);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=2};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var message=new TextBox{Text=text,Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,ScrollBars=ScrollBars.Vertical,RightToLeft=RightToLeft.Yes,Font=new Font("Segoe UI",12)};root.Controls.Add(message,0,0);
            var actions=Ui.Row();Action<string,DialogResult,string> add=(caption,result,variant)=>{var button=Ui.Button(caption,()=>form.DialogResult=result,variant);actions.Controls.Add(button);if(result==DialogResult.OK||result==DialogResult.Yes)form.AcceptButton=button;if(result==DialogResult.Cancel||result==DialogResult.No)form.CancelButton=button;};
            if(buttons==MessageBoxButtons.YesNo||buttons==MessageBoxButtons.YesNoCancel){add("لا",DialogResult.No,"default");if(buttons==MessageBoxButtons.YesNoCancel)add("إلغاء",DialogResult.Cancel,"default");add("نعم، متابعة",DialogResult.Yes,"primary");}
            else{if(buttons==MessageBoxButtons.OKCancel)add("إلغاء",DialogResult.Cancel,"default");add("موافق",DialogResult.OK,"primary");}
            root.Controls.Add(actions,0,1);form.Controls.Add(root);Ui.Theme(form,light);message.BackColor=form.BackColor;
            return form;
        }
    }
}
