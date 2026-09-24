using System;
using System.Windows.Forms;

namespace TRPKGViewer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "TR PKG Viewer - Startup failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}