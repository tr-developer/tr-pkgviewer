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
        readonly PictureBox imageView = new PictureBox();
        readonly ToolStripStatusLabel status = new ToolStripStatusLabel();
        readonly ToolStripMenuItem textDecryptionMenu = new ToolStripMenuItem("Text Decryption");
        TextDecryptionKey selectedTextKey;

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

            foreach (var key in TextDecryptionKeys.All)
            {
                var keyItem = new ToolStripMenuItem(key.Name);
                keyItem.Tag = key;
                keyItem.Click += SelectTextDecryptionKey;
                textDecryptionMenu.DropDownItems.Add(keyItem);
            }
            menu.Items.Add(textDecryptionMenu);
            SetTextDecryptionKey(TextDecryptionKeys.All.Count == 0
                ? null
                : TextDecryptionKeys.All[TextDecryptionKeys.All.Count - 1]);


            MainMenuStrip = menu;
            Controls.Add(menu);

            var split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Panel1MinSize = 120;
            split.SplitterWidth = 5;
            split.Panel1.Controls.Add(tree); split.Panel2.Controls.Add(view); split.Panel2.Controls.Add(imageView);
            tree.Dock = DockStyle.Fill; view.Dock = DockStyle.Fill;
            imageView.Dock = DockStyle.Fill; imageView.SizeMode = PictureBoxSizeMode.Zoom; imageView.Visible = false;
            tree.Font = new Font(FontFamily.GenericSansSerif, 8.0f);
            view.Multiline = true; view.ScrollBars = ScrollBars.Both;
            view.Font = new Font(FontFamily.GenericMonospace, 9); view.WordWrap = false;
            tree.AfterSelect += (s, e) => ShowNode(e.Node);
            tree.NodeMouseDoubleClick += (s, e) => PreviewEntry(e.Node);

            var ss = new StatusStrip(); ss.Items.Add(status);
            Controls.Add(ss); Controls.Add(split);
            ss.Dock = DockStyle.Bottom; menu.Dock = DockStyle.Top; split.BringToFront();

            Shown += (s, e) =>
            {
                int target = Math.Max(split.Panel1MinSize, Math.Min(180, split.ClientSize.Width / 5));
                if (split.ClientSize.Width > target + split.SplitterWidth)
                    split.SplitterDistance = target;
            };
        }

        void SelectTextDecryptionKey(object sender, EventArgs e)
        {
            var item = sender as ToolStripMenuItem;
            SetTextDecryptionKey(item == null ? null : item.Tag as TextDecryptionKey);
        }

        void SetTextDecryptionKey(TextDecryptionKey key)
        {
            selectedTextKey = key;
            foreach (ToolStripItem child in textDecryptionMenu.DropDownItems)
            {
                var menuItem = child as ToolStripMenuItem;
                var menuKey = menuItem == null ? null : menuItem.Tag as TextDecryptionKey;
                if (menuKey != null)
                {
                    bool isSelected = ReferenceEquals(menuKey, key);
                    menuItem.Checked = isSelected;
                    menuItem.Text = menuKey.Name + (isSelected ? " [Selected]" : "");
                }
            }
        }

        string GetSelectedCryptSeed()
        {
            if (selectedTextKey == null)
                throw new InvalidOperationException("No Text Decryption key is selected.");
            return selectedTextKey.Key;
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
            ShowTextView();
            if (pkg == null) return;
            if (n.Tag is PkgIndexEntry)
            {
                var e = (PkgIndexEntry)n.Tag;
                view.Text = "#" + e.Index + "\\r\\n" + e.Name +
                    "\\r\\nEncoded info: " + e.EncodedInfoSize + " bytes" +
                    "\\r\\nDecoded info: " + e.DecodedInfo.Length + " bytes" +
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
                var sb = new StringBuilder();
                sb.AppendLine("Offset    UInt32(hex)  UInt32(dec)");
                foreach (var w in pkg.Words)
                    sb.AppendFormat("0x{0:X4}    0x{1:X8}   {2}\\r\\n", w.Offset, w.Value, w.Value);
                view.Text = sb.ToString();
            }
            else if (t == "strings")
            {
                var sb = new StringBuilder();
                foreach (var x in pkg.Strings)
                    sb.AppendFormat("0x{0:X8}  {1}\\r\\n", x.Offset, x.Text);
                view.Text = sb.ToString();
            }
            else
                view.Text = "File: " + pkg.Path + "\\r\\nSize: " + pkg.Length.ToString("N0") + " bytes";
        }

        void PreviewEntry(TreeNode node)
        {
            if (pkg == null || node == null || !(node.Tag is PkgIndexEntry)) return;
            var entry = (PkgIndexEntry)node.Tag;
            try
            {
                byte[] payload = entryReader.Read(pkg.Path, entry, GetSelectedCryptSeed());
                string ext = Path.GetExtension(entry.Name);
                string imageKind;
                if (TryPreviewImage(payload, out imageKind))
                {
                    status.Text = "Preview " + entry.Name + " [" + imageKind + "] (" + payload.Length.ToString("N0") + " bytes)";
                    return;
                }
                if (string.Equals(ext, ".txt", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".xml", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".py", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".lua", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".ini", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".cfg", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".csv", StringComparison.OrdinalIgnoreCase))
                {
                    view.Text = DecodeText(payload);
                    status.Text = "Preview " + entry.Name + " (" + payload.Length.ToString("N0") + " bytes)";
                }
                else
                    view.Text = PkgReader.Hex(payload.Length > 65536 ? Slice(payload, 65536) : payload);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Preview failed"); }
        }

        void ShowTextView()
        {
            if (imageView.Image != null) { var old = imageView.Image; imageView.Image = null; old.Dispose(); }
            imageView.Visible = false; view.Visible = true; view.BringToFront();
        }

        bool TryPreviewImage(byte[] data, out string kind)
        {
            kind = null;
            try
            {
                Image image = null;
                if (DdsDecoder.IsDds(data)) { image = DdsDecoder.Decode(data); kind = "DDS"; }
                else if (IsStandardImage(data))
                {
                    using (var ms = new MemoryStream(data, false))
                    using (var loaded = Image.FromStream(ms)) image = new Bitmap(loaded);
                    kind = IsPng(data) ? "PNG" : IsJpeg(data) ? "JPEG" : IsBmp(data) ? "BMP" : "GIF";
                }
                if (image == null) return false;
                if (imageView.Image != null) imageView.Image.Dispose();
                imageView.Image = image; view.Visible = false; imageView.Visible = true; imageView.BringToFront();
                return true;
            }
            catch { return false; }
        }

        static bool IsStandardImage(byte[] b) { return IsPng(b) || IsJpeg(b) || IsBmp(b) || IsGif(b); }
        static bool IsPng(byte[] b) { return b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A; }
        static bool IsJpeg(byte[] b) { return b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF; }
        static bool IsBmp(byte[] b) { return b.Length >= 2 && b[0] == 0x42 && b[1] == 0x4D; }
        static bool IsGif(byte[] b) { return b.Length >= 6 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x38 && (b[4] == 0x37 || b[4] == 0x39) && b[5] == 0x61; }

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
            try
            {
                return new UTF8Encoding(false, true).GetString(data);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(949).GetString(data);
            }
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
                    byte[] payload = entryReader.Read(pkg.Path, entry, GetSelectedCryptSeed());
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
                        byte[] payload = entryReader.Read(pkg.Path, entry, GetSelectedCryptSeed());

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
