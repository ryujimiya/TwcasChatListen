using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Configuration;

namespace TwcasChatter
{
    /// <summary>
    /// 設定フォーム
    /// </summary>
    public partial class SettingsForm : Form
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public SettingsForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// フォームがロードされた
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SettingsForm_Load(object sender, EventArgs e)
        {
            string topMost = ConfigurationSettings.AppSettings["TopMost"];
            TopMostCheckBox.Checked = (topMost == "1");
        }

        /// <summary>
        /// フォームが閉じられた
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SettingsForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            string topMost = TopMostCheckBox.Checked? "1":"0";
            ConfigurationSettings.AppSettings["TopMost"] = topMost;
        }
    }
}
