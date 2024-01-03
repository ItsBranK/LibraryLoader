using System;
using System.Diagnostics;
using System.Windows.Forms;
using LibraryLoader.Framework;

namespace LibraryLoader.Forms
{
    public partial class AboutFrm : Form
    {
        public AboutFrm()
        {
            InitializeComponent();
        }

        private void AboutFrm_Load(object sender, EventArgs e)
        {
            this.Text = (Assembly.GetTitle() + " - About");
            ArchetypeLbl.Text = (Assembly.Is64Bit() ? "x64" : "x86");
            VersionLbl.Text = ("v" + Assembly.GetVersion());
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
