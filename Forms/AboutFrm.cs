using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace LibraryLoader.Forms
{
    public partial class AboutFrm : Form
    {
        private static bool _x64Bit = true;
        private static string _version = "v1.0";

        private string GetTitleName()
        {
            if (_x64Bit)
            {
                return "ItsBranK's Library Loader (x64) - About";
            }

            return "ItsBranK's Library Loader (x32) - About";
        }

        public AboutFrm()
        {
            InitializeComponent();
        }

        private void AboutFrm_Load(object sender, EventArgs e)
        {
            this.Text = GetTitleName();
            VersionLbl.Text = _version;
        }

        private void GitHubLbl_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo(GitHubLbl.Text) { UseShellExecute = true });
        }

        private void IconsLbl_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo(IconsLbl.Text) { UseShellExecute = true });
        }
    }
}
