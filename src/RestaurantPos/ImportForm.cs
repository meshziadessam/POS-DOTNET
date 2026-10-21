using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace RestaurantPos
{
    public sealed class ImportForm : Form
    {
        private readonly TextBox source = new TextBox { Dock = DockStyle.Fill };
        private readonly Label message = new Label { AutoSize = true, MaximumSize = new Size(570, 0), ForeColor = Color.DarkRed };
        public ImportForm(string destination)
        {
            Text = "اختيار بيانات المطعم"; Size = new Size(650, 330); MinimumSize = new Size(600, 330);
            StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 11); AutoScaleMode = AutoScaleMode.Dpi;
            RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = "اختر مجلد برنامج Python أو مجلد data أو ملف pos.db. سيتم إنشاء نسخة مستقلة من البيانات.", AutoSize = true, MaximumSize = new Size(570, 0) }, 0, 0);
            source.RightToLeft = RightToLeft.No; layout.Controls.Add(source, 0, 1);
            var browse = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var folder = new Button { Text = "اختيار مجلد", AutoSize = true, Height = 40 };
            folder.Click += (s, e) => { using (var picker = new FolderBrowserDialog { Description = "اختر مجلد المشروع أو مجلد data" }) { if (picker.ShowDialog(this) == DialogResult.OK) { source.Text = picker.SelectedPath; message.Text = ""; } } };
            var file = new Button { Text = "اختيار ملف pos.db", AutoSize = true, Height = 40 };
            file.Click += (s, e) => { using (var picker = new OpenFileDialog { Title = "اختر قاعدة بيانات المطعم pos.db", Filter = "POS database (pos.db)|pos.db|SQLite database (*.db)|*.db", CheckFileExists = true }) { if (picker.ShowDialog(this) == DialogResult.OK) { source.Text = picker.FileName; message.Text = ""; } } };
            browse.Controls.Add(folder); browse.Controls.Add(file); layout.Controls.Add(browse, 0, 2); layout.Controls.Add(message, 0, 3);
            var import = new Button { Text = "فتح نسخة من البيانات", AutoSize = true, Height = 44 };
            import.Click += async (s, e) =>
            {
                string selection = source.Text; import.Enabled = false; browse.Enabled = false; source.Enabled = false;
                try { await System.Threading.Tasks.Task.Run(() => PreviewImport.Create(selection, destination)); DialogResult = DialogResult.OK; }
                catch (Exception error) { message.Text = error.Message; }
                finally { if (!IsDisposed) { import.Enabled = true; browse.Enabled = true; source.Enabled = true; } }
            };
            layout.Controls.Add(import, 0, 4); Controls.Add(layout); AcceptButton = import;
        }
    }
}
