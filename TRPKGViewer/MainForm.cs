using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using TRPKGViewer.Core;

namespace TRPKGViewer
{
    public sealed class MainForm : Form
    {
        readonly PkgReader reader = new PkgReader();
        readonly PkgEntryReader entryReader = new PkgEntryReader();
        PkgInfo pkg;
        readonly TreeView tree = new TreeView();
        readonly TextBox view = new TextBox();
        readonly ToolStripStatusLabel status = new ToolStripStatusLabel();

        public MainForm()
        {
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
            var extract = new ToolStripMenuItem("Extract selected...");
            extract.Click += (s, e) => ExtractSelected();
            file.DropDownItems.Add(extract);
            var extractAll = new ToolStripMenuItem("Extract all...");
            extractAll.Click += (s, e) => ExtractAll();
            file.DropDownItems.Add(extractAll);
            menu.Items.Add(file);


            MainMenuStrip = menu;
            Controls.Add(menu);

            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 350 };
            split.Panel1.Controls.Add(tree); split.Panel2.Controls.Add(view);
            tree.Dock = DockStyle.Fill; view.Dock = DockStyle.Fill;
            view.Multiline = true; view.ScrollBars = ScrollBars.Both;
            view.Font = new Font(FontFamily.GenericMonospace, 9); view.WordWrap = false;
            tree.AfterSelect += (s, e) => ShowNode(e.Node);
            tree.NodeMouseDoubleClick += (s, e) => PreviewEntry(e.Node);

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
            root.Nodes.Add("Printable strings (diagnostic)").Tag = "strings";
            var files = root.Nodes.Add("Files (" + pkg.Entries.Count + ")");
            foreach (var entry in pkg.Entries) AddEntryNode(files, entry);
            root.Expand(); files.Expand(); tree.SelectedNode = root;
        }

        static void AddEntryNode(TreeNode filesRoot, PkgIndexEntry entry)
        {
            string[] parts = entry.Name.Replace('/', '\\').Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            TreeNode parent = filesRoot;
            for (int i = 0; i < parts.Length; i++)
            {
                bool leaf = i == parts.Length - 1;
                if (leaf)
                {
                    parent.Nodes.Add(parts[i]).Tag = entry;
                    continue;
                }

                TreeNode next = null;
                foreach (TreeNode child in parent.Nodes)
                    if (!(child.Tag is PkgIndexEntry) && string.Equals(child.Text, parts[i], StringComparison.OrdinalIgnoreCase))
                    {
                        next = child;
                        break;
                    }
                if (next == null) next = parent.Nodes.Add(parts[i]);
                parent = next;
            }
        }

        void ShowNode(TreeNode n)
        {
            if (pkg == null) return;
            if (n.Tag is PkgIndexEntry)
            {
                var e = (PkgIndexEntry)n.Tag;
                view.Text = "#" + e.Index + "\\r\\n" + e.Name +
                    "\\r\\nEncoded info: " + e.EncodedInfoSize + " bytes\r
Decoded info: " + e.DecodedInfo.Length + " bytes" +
                    "\\r\\n\\r\\nDecoded +0x404: " + e.LogicalSize +
                    "\\r\\nDecoded +0x408: " + e.Decoded408 +
                    "\\r\\nDecoded +0x40C: 0x" + e.Decoded40C.ToString("X8") +
                    "\\r\\nDecoded +0x410: " + e.Decoded410 +
                    "\\r\\nDecoded +0x414: 0x" + e.DataOffset.ToString("X8");
                return;
            }
            string t = n.Tag as string;
            if (t == "header") view.Text = PkgReader.Hex(pkg.Header);
            else if (t == "words")
            {
                var sb = new StringBuilder(); sb.AppendLine("Offset    UInt32(hex)  UInt32(dec)");
                foreach (var w in pkg.Words) sb.AppendFormat("0x{0:X4}    0x{1:X8}   {2}\\r\\n", w.Offset, w.Value, w.Value);
                view.Text = sb.ToString();
            }
            else if (t == "strings")
            {
                var sb = new StringBuilder();
                foreach (var x in pkg.Strings) sb.AppendFormat("0x{0:X8}  {1}\\r\\n", x.Offset, x.Text);
                view.Text = sb.ToString();
            }
            else view.Text = "File: " + pkg.Path + "\\r\\nSize: " + pkg.Length.ToString("N0") + " bytes";
        }

        void PreviewEntry(TreeNode node)
        {
            if (pkg == null || node == null || !(node.Tag is PkgIndexEntry)) return;
            var entry = (PkgIndexEntry)node.Tag;
            try
            {
                byte[] payload = entryReader.Read(pkg.Path, entry);
                string ext = Path.GetExtension(entry.Name);
                if (string.Equals(ext, ".txt", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".xml", StringComparison.OrdinalIgnoreCase))
                {
                    view.Text = DecodeText(payload);
                    status.Text = "Preview " + entry.Name + " (" + payload.Length.ToString("N0") + " bytes)";
                }
                else
                    view.Text = PkgReader.Hex(payload.Length > 65536 ? Slice(payload, 65536) : payload);
            }
            catch (PackageCryptRequiredException ex)
            {
                view.Text = ex.Message + "\\r\\n\\r\\nPackage crypt is required before this entry can be previewed.";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Preview failed"); }
        }

        static byte[] Slice(byte[] data, int count)
        {
            byte[] b = new byte[Math.Min(data.Length, count)];
            Buffer.BlockCopy(data, 0, b, 0, b.Length);
            return b;
        }

        static string DecodeText(byte[] data)
        {
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                return Encoding.UTF8.GetString(data, 3, data.Length - 3);
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
                return Encoding.Unicode.GetString(data, 2, data.Length - 2);
            try { return new UTF8Encoding(false, true).GetString(data); }\n            catch (DecoderFallbackException) { return Encoding.GetEncoding(949).GetString(data); }
        }

        void ExtractSelected()
        {
            if (pkg == null || tree.SelectedNode == null || !(tree.SelectedNode.Tag is PkgIndexEntry)) return;
            var entry = (PkgIndexEntry)tree.SelectedNode.Tag;
            using (var d = new SaveFileDialog { FileName = Path.GetFileName(entry.Name), Filter = "All files (*.*)|*.*" })
            {
                if (d.ShowDialog() != DialogResult.OK) return;
                try
                {
                    byte[] payload = entryReader.Read(pkg.Path, entry);
                    // Package-level chunk crypt is handled by PkgEntryReader. Encrypted TXT
                    // must additionally pass through the selected text decrypt type.
                    File.WriteAllBytes(d.FileName, payload);
                    status.Text = "Extracted " + entry.Name + " (" + payload.Length.ToString("N0") + " bytes)";
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Extract failed"); }
            }
        }

        void ExtractAll()
        {
            if (pkg == null) return;
            using (var d = new FolderBrowserDialog { Description = "Select extraction folder" })
            {
                if (d.ShowDialog() != DialogResult.OK) return;
                try
                {
                    string root = Path.GetFullPath(d.SelectedPath);
                    int done = 0;
                    foreach (var entry in pkg.Entries)
                    {
                        byte[] payload = entryReader.Read(pkg.Path, entry);

                        string relative = entry.Name.Replace('/', Path.DirectorySeparatorChar)
                                                    .Replace('\\', Path.DirectorySeparatorChar);
                        string target = Path.GetFullPath(Path.Combine(root, relative));
                        string rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                          + Path.DirectorySeparatorChar;
                        if (!target.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Unsafe package path: " + entry.Name);

                        string dir = Path.GetDirectoryName(target);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        File.WriteAllBytes(target, payload);
                        done++;
                        status.Text = "Extracting " + done + "/" + pkg.Entries.Count + ": " + entry.Name;
                        Application.DoEvents();
                    }
                    status.Text = "Extracted all " + done + " files to " + root;
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Extract all failed"); }
            }
        }

        void ExportHeader()
        {
            if (pkg == null) return;
            using (var d = new SaveFileDialog { FileName = Path.GetFileName(pkg.Path) + ".header.bin", Filter = "Binary|*.bin" })
                if (d.ShowDialog() == DialogResult.OK) File.WriteAllBytes(d.FileName, pkg.Header);
        }

    }
}
