using System.Drawing;
using System.Windows.Forms;

namespace RestaurantPos
{
    internal sealed class ThemeTabs : TabControl
    {
        public ThemeTabs(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        protected override void OnPaint(PaintEventArgs e)
        {
            bool light=Ui.IsLight(FindForm());Color background=ColorTranslator.FromHtml(light?"#f5f1e8":"#121212"),surface=ColorTranslator.FromHtml(light?"#e5dfd2":"#242424");e.Graphics.Clear(background);
            for(int i=0;i<TabPages.Count;i++)
            {
                Rectangle bounds=GetTabRect(i);using(var fill=new SolidBrush(i==SelectedIndex?Ui.Gold:surface))e.Graphics.FillRectangle(fill,bounds);
                TextRenderer.DrawText(e.Graphics,TabPages[i].Text,Font,bounds,i==SelectedIndex?Color.Black:light?Color.FromArgb(25,25,25):Color.WhiteSmoke,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.RightToLeft);
            }
        }
        protected override void OnSelectedIndexChanged(System.EventArgs e){base.OnSelectedIndexChanged(e);Invalidate();}
    }
}
