namespace Yamster.Native
{
    partial class YammerLoginForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(YammerLoginForm));
            this.ctlWebBrowser = new System.Windows.Forms.WebBrowser();
            this.txtLoginUser = new System.Windows.Forms.TextBox();
            this.btnLogin = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.ctlStatusStrip = new System.Windows.Forms.StatusStrip();
            this.txtStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.ctlStatusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // ctlWebBrowser
            // 
            this.ctlWebBrowser.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ctlWebBrowser.Location = new System.Drawing.Point(12, 38);
            this.ctlWebBrowser.MinimumSize = new System.Drawing.Size(20, 20);
            this.ctlWebBrowser.Name = "ctlWebBrowser";
            this.ctlWebBrowser.Size = new System.Drawing.Size(760, 493);
            this.ctlWebBrowser.TabIndex = 3;
            this.ctlWebBrowser.Navigated += new System.Windows.Forms.WebBrowserNavigatedEventHandler(this.ctlWebBrowser_Navigated);
            // 
            // txtLoginUser
            // 
            this.txtLoginUser.ForeColor = System.Drawing.Color.DarkGray;
            this.txtLoginUser.Location = new System.Drawing.Point(56, 12);
            this.txtLoginUser.Name = "txtLoginUser";
            this.txtLoginUser.Size = new System.Drawing.Size(247, 20);
            this.txtLoginUser.TabIndex = 1;
            this.txtLoginUser.Text = "alias@example.com";
            this.txtLoginUser.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtLoginUser_KeyPress);
            this.txtLoginUser.MouseDown += new System.Windows.Forms.MouseEventHandler(this.txtLoginUser_MouseDown);
            // 
            // btnLogin
            // 
            this.btnLogin.Location = new System.Drawing.Point(309, 10);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(92, 22);
            this.btnLogin.TabIndex = 2;
            this.btnLogin.Text = "&Login";
            this.btnLogin.UseVisualStyleBackColor = true;
            this.btnLogin.Click += new System.EventHandler(this.btnLogin_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(38, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "E-mail:";
            // 
            // ctlStatusStrip
            // 
            this.ctlStatusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.txtStatus});
            this.ctlStatusStrip.Location = new System.Drawing.Point(0, 540);
            this.ctlStatusStrip.Name = "ctlStatusStrip";
            this.ctlStatusStrip.Size = new System.Drawing.Size(784, 22);
            this.ctlStatusStrip.TabIndex = 4;
            this.ctlStatusStrip.Text = "statusStrip1";
            // 
            // txtStatus
            // 
            this.txtStatus.Name = "txtStatus";
            this.txtStatus.Size = new System.Drawing.Size(769, 17);
            this.txtStatus.Spring = true;
            this.txtStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(407, 10);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(92, 22);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "&Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // YammerLoginForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 562);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.ctlStatusStrip);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnLogin);
            this.Controls.Add(this.txtLoginUser);
            this.Controls.Add(this.ctlWebBrowser);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "YammerLoginForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Yammer Single Sign-on";
            this.ctlStatusStrip.ResumeLayout(false);
            this.ctlStatusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.WebBrowser ctlWebBrowser;
        private System.Windows.Forms.TextBox txtLoginUser;
        private System.Windows.Forms.Button btnLogin;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.StatusStrip ctlStatusStrip;
        private System.Windows.Forms.ToolStripStatusLabel txtStatus;
        private System.Windows.Forms.Button btnCancel;
    }
}