using System.Drawing;
using System.Windows.Forms;

namespace RestaurantPos
{
    internal sealed class OrderGrid : DataGridView
    {
        public OrderGrid()
        {
            Dock=DockStyle.Fill;RightToLeft=RightToLeft.Yes;ReadOnly=true;
            AllowUserToAddRows=false;AllowUserToDeleteRows=false;AllowUserToResizeRows=false;
            RowHeadersVisible=false;MultiSelect=false;SelectionMode=DataGridViewSelectionMode.FullRowSelect;
            AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;BackgroundColor=Color.FromArgb(18,18,18);
            BorderStyle=BorderStyle.None;CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
            ColumnHeadersHeight=42;ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            RowTemplate.Height=48;Font=new Font("Segoe UI",11,FontStyle.Bold);DoubleBuffered=true;
            string[] names={"اسم الصنف","العدد","السعر","الإجمالي"};int[] weights={46,14,20,20};int[] minimum={90,42,65,70};
            for(int i=0;i<names.Length;i++)Columns.Add(new DataGridViewTextBoxColumn{Name="cart"+i,HeaderText=names[i],FillWeight=weights[i],MinimumWidth=minimum[i],SortMode=DataGridViewColumnSortMode.NotSortable});
            Columns[2].DefaultCellStyle.Format=Columns[3].DefaultCellStyle.Format="N2";
            DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;Columns[0].DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleLeft;
            DefaultCellStyle.Padding=new Padding(5);ColumnHeadersDefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;
        }
        public int SelectedIndex
        {
            get{return CurrentCell==null?-1:CurrentCell.RowIndex;}
            set{if(value>=0&&value<Rows.Count){CurrentCell=Rows[value].Cells[0];Rows[value].Selected=true;}}
        }
    }
}
