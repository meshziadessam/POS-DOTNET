using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace RestaurantPos
{
    // Keep native controls and thumbnails proportional to the viewport, not the catalog.
    internal sealed class ProductPanel : Panel
    {
        private sealed class Tile
        {
            internal Button Button;
            internal DataRow Item;
            internal string ImageKey;
        }
        private readonly List<Tile> tiles=new List<Tile>();
        private DataTable items=new DataTable();
        private Action<DataRow> select;
        private string dataRoot;
        private bool light,layingOut;
        private readonly VScrollBar scroll=new VScrollBar{Dock=DockStyle.Right,Visible=false};
        private readonly Label empty=new Label{Text="أضف الأصناف من إعدادات ← البيانات ← الأصناف",AutoSize=true,MaximumSize=new Size(430,0)};
        internal ProductPanel()
        {
            Dock=DockStyle.Fill;AutoScroll=false;RightToLeft=RightToLeft.No;
            DoubleBuffered=true;Controls.Add(empty);Controls.Add(scroll);scroll.ValueChanged+=(s,e)=>ArrangeTiles();
        }
        internal void SetItems(DataTable table,Action<DataRow> onSelect,string root,bool isLight)
        {
            items=table;select=onSelect;dataRoot=root;light=isLight;
            ScrollToPixel(0);ArrangeTiles();
        }
        internal void Style(bool isLight)
        {
            light=isLight;
            foreach(var tile in tiles){tile.Button.BackColor=ColorTranslator.FromHtml(light?"#ebe3d4":"#242424");tile.Button.ForeColor=light?Color.FromArgb(29,29,29):Color.WhiteSmoke;}
            empty.ForeColor=ForeColor;
        }
        protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);ArrangeTiles();}
        internal void ScrollToPixel(int top){scroll.Value=Math.Max(0,Math.Min(top,Math.Max(0,scroll.Maximum-scroll.LargeChange+1)));ArrangeTiles();}
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if(e is HandledMouseEventArgs handled)handled.Handled=true;
            int lines=SystemInformation.MouseWheelScrollLines;
            ScrollToPixel(scroll.Value-e.Delta/120*(lines<0?ClientSize.Height:Math.Max(1,lines)*20));
        }
        internal void ArrangeTiles()
        {
            if(layingOut||items==null||tiles==null||empty==null||scroll==null||IsDisposed)return;
            layingOut=true;SuspendLayout();
            try
            {
                float scale=DeviceDpi/96f;
                // Reserve the scrollbar width consistently, avoiding a width/layout feedback loop.
                int width=Math.Max(1,ClientSize.Width-SystemInformation.VerticalScrollBarWidth-6);
                int columns=Math.Max(1,(int)Math.Round(width/(145.0*scale)));
                int tileWidth=Math.Max(1,width/columns-6);
                int tileHeight=Math.Max(Font.Height*2+(int)(24*scale),(int)(tileWidth*.78));
                // Leave room for image thumbnails, whether or not the first visible row has images.
                bool images=items.Columns.Contains("image_path");
                if(images)foreach(DataRow item in items.Rows)if(Convert.ToString(item["image_path"])!=""){tileHeight=Math.Max(tileHeight,Font.Height*2+60+(int)(24*scale));break;}
                int pitch=tileHeight+6,rows=(items.Rows.Count+columns-1)/columns;
                int contentHeight=rows*pitch+3;
                scroll.LargeChange=Math.Max(1,ClientSize.Height);scroll.SmallChange=20;scroll.Maximum=Math.Max(0,contentHeight-1);scroll.Visible=contentHeight>ClientSize.Height;
                int top=Math.Min(scroll.Value,Math.Max(0,contentHeight-ClientSize.Height));
                if(scroll.Value!=top)scroll.Value=top;
                int firstRow=Math.Min(Math.Max(0,rows-1),top/pitch);
                int count=Math.Min(Math.Max(0,items.Rows.Count-firstRow*columns),columns*((ClientSize.Height+pitch-1)/pitch+1));
                while(tiles.Count<count)
                {
                    var tile=new Tile();tile.Button=Ui.Button("",()=>{if(tile.Item!=null)select(tile.Item);},"default");
                    tile.Button.AutoSize=false;tile.Button.MinimumSize=Size.Empty;tile.Button.TextImageRelation=TextImageRelation.ImageAboveText;
                    tile.Button.MouseWheel+=(s,e)=>OnMouseWheel(e);
                    tile.Button.FlatAppearance.BorderColor=Ui.Gold;tile.Button.FlatAppearance.BorderSize=2;
                    tiles.Add(tile);Controls.Add(tile.Button);
                }
                empty.Visible=items.Rows.Count==0;empty.Location=new Point(12,12);
                for(int i=0;i<tiles.Count;i++)
                {
                    var tile=tiles[i];bool visible=i<count;tile.Button.Visible=visible;
                    if(!visible){tile.Item=null;ReleaseImage(tile);continue;}
                    int index=firstRow*columns+i;var item=items.Rows[index];tile.Item=item;
                    tile.Button.Text=Convert.ToString(item["name"])+"\n"+Convert.ToDecimal(item["price"]).ToString("0.00")+" ج";
                    int col=index%columns,row=index/columns;
                    tile.Button.SetBounds(width-(col+1)*(tileWidth+6)+3,row*pitch+3-top,tileWidth,tileHeight);
                    tile.Button.TabIndex=i;
                    string path=images?Convert.ToString(item["image_path"]):"";
                    string resolved=path==""?"":Path.IsPathRooted(path)?path:Path.Combine(dataRoot,"data","item_images",path);
                    string key=resolved!=""&&File.Exists(resolved)?resolved+"|"+File.GetLastWriteTimeUtc(resolved).Ticks:"";
                    if(tile.ImageKey!=key)
                    {
                        ReleaseImage(tile);tile.ImageKey=key;
                        if(key!="")try{using(var image=Image.FromFile(resolved))tile.Button.Image=new Bitmap(image,new Size(60,60));}
                        catch(Exception error)when(error is ArgumentException||error is IOException||error is OutOfMemoryException){AppLog.Error(dataRoot,error);}
                    }
                }
                Style(light);
            }
            finally{ResumeLayout(false);layingOut=false;}
        }
        private static void ReleaseImage(Tile tile){var image=tile.Button.Image;tile.Button.Image=null;image?.Dispose();tile.ImageKey=null;}
        protected override void Dispose(bool disposing){if(disposing)foreach(var tile in tiles)ReleaseImage(tile);base.Dispose(disposing);}
    }
}
