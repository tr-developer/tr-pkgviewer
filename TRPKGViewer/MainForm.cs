using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using TRPKGViewer.Core;
using TRPKGViewer.Crypto;

namespace TRPKGViewer
{
    public sealed class MainForm : Form
    {
        readonly PkgReader reader = new PkgReader();
        readonly TextDecryptorRegistry decryptors = new TextDecryptorRegistry();
        readonly TextPayloadPipeline textPipeline;
        PkgInfo pkg;
        readonly TreeView tree = new TreeView();
        readonly TextBox view = new TextBox();
        readonly ToolStripStatusLabel status = new ToolStripStatusLabel();

        public MainForm()
        {
            textPipeline = new TextPayloadPipeline(decryptors);
            Text = "TR PKG Viewer";
            Width = 1200; Height = 760;

            var menu = new MenuStrip();
            var file = new ToolStripMenuItem("File");
            var open = new ToolStripMenuItem("Open PKG...");
            open.Click += (s, e) => OpenPkg();
            file.DropDownItems.Add(open);
            var export = new ToolStripMenuItem("Export header...");
            export.Click += (s, e) => ExportHeader();
            file.DropDownItems.Add(export);
            menu.Items.Add(file);

            var tools = new ToolStripMenuItem("Tools");
            var aes = new ToolStripMenuItem("Set aeskey...");
            aes.Click += (s, e) => RegisterAes();
            tools.DropDownItems.Add(aes);
            menu.Items.Add(tools);

            MainMenuStrip = menu;
            Controls.Add(menu);

            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 350 };
            split.Panel1.Controls.Add(tree); split.Panel2.Controls.Add(view);
            tree.Dock = DockStyle.Fill; view.Dock = DockStyle.Fill;
            view.Multiline = true; view.ScrollBars = ScrollBars.Both;
            view.Font = new Font(FontFamily.GenericMonospace, 9); view.WordWrap = false;
            tree.AfterSelect += (s, e) => ShowNode(e.Node);

            var ss = new StatusStrip(); ss.Items.Add(status);
            Controls.Add(ss); Controls.Add(split);
            ss.Dock = DockStyle.Bottom; menu.Dock = DockStyle.Top; split.BringToFront();
        }

        void OpenPkg()
        {
            using (var d = new OpenFileDialog { Filter = "TR package (*.pkg)|*.pkg|All files (*.*)|*.*" })
                if (d.ShowDialog() == DialogResult.OK)
                    try { pkg = reader.Open(d.FileName); BuildTree(); status.Text = Path.GetFileName(d.FileName) + "  " + pkg.Length.ToString("N0") + " bytes"; }
                    catch (Exception ex) { MessageBox.Show(ex.ToString(), "Open failed"); }
        }

        void BuildTree()
        {
            tree.Nodes.Clear();
            var root = tree.Nodes.Add(Path.GetFileName(pkg.Path)); root.Tag = "summary";
            root.Nodes.Add("Header / first 4 KiB").Tag = "header";
            root.Nodes.Add("Header DWORD view").Tag = "words";
            root.Nodes.Add("Printable strings (diagnostic)").Tag = "strings";\n            var files = root.Nodes.Add("Files (" + pkg.Entries.Count + ")");\n            foreach (var entry in pkg.Entries) files.Nodes.Add(entry.Name).Tag = entry;
            root.Expand(); tree.SelectedNode = root;
        }

        void ShowNode(TreeNode n)
        {
            if (pkg == null) return;
            if (n.Tag is PkgIndexEntry)\n            {\n                var e = (PkgIndexEntry)n.Tag;\n                view.Text = "#" + e.Index + "\\r\\n" + e.Name + "\\r\\nEncoded info: " + e.EncodedInfoSize + " bytes\\r\\nDecoded info: " + e.DecodedInfo.Length + " bytes";\n                return;\n            }\n            string t = n.Tag as string;
            if (t == "header") view.Text = PkgReader.Hex(pkg.Header);
            else if (t == "words")
            {
                var sb = new StringBuilder(); sb.AppendLine("Offset    UInt32(hex)  UInt32(dec)");
                foreach (var w in pkg.Words) sb.AppendFormat("0x{0:X4}    0x{1:X8}   {2}\r\n", w.Offset, w.Value, w.Value);
                view.Text = sb.ToString();
            }
            else if (t == "strings")
            {
                var sb = new StringBuilder();
                foreach (var x in pkg.Strings) sb.AppendFormat("0x{0:X8}  {1}\r\n", x.Offset, x.Text);
                view.Text = sb.ToString();
            }
            else view.Text = "File: " + pkg.Path + "\r\nSize: " + pkg.Length.ToString("N0") + " bytes";
        }

        void ExportHeader()
        {
            if (pkg == null) return;
            using (var d = new SaveFileDialog { FileName = Path.GetFileName(pkg.Path) + ".header.bin", Filter = "Binary|*.bin" })
                if (d.ShowDialog() == DialogResult.OK) File.WriteAllBytes(d.FileName, pkg.Header);
        }

        // The eventual entry reader/exporter must call this gate for payloads.
        byte[] ProcessEntryPayload(string entryName, bool encrypted, byte[] payload)
        {
            return textPipeline.Process(entryName, encrypted, payload);
        }

        void RegisterAes()
        {
            using (var f = new AesKeyForm())
                if (f.ShowDialog(this) == DialogResult.OK)
                    try { decryptors.Register(AesTextDecryptor.FromKeyString(f.KeyText), true); MessageBox.Show("Active text decrypt type: aeskey"); }
                    catch (Exception ex) { MessageBox.Show(ex.Message, "Invalid key"); }
        }
    }

    sealed class AesKeyForm : Form
    {
        readonly TextBox key = new TextBox();
        public string KeyText { get { return key.Text.Trim(); } }

        public AesKeyForm()
        {
            Text = "aeskey"; Width = 520; Height = 180;
            var l1 = new Label { Text = "Decrypt type", Left = 15, Top = 20, Width = 80 };
            var type = new TextBox { Text = "aeskey", ReadOnly = true }; type.SetBounds(100, 17, 380, 24);
            var l2 = new Label { Text = "AES key", Left = 15, Top = 55, Width = 80 }; key.SetBounds(100, 52, 380, 24);
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 320, Top = 90, Width = 75 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 405, Top = 90, Width = 75 };
            Controls.AddRange(new Control[] { l1, type, l2, key, ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
        }
    }
}