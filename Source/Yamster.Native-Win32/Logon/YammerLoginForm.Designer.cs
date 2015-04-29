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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(YammerLoginForm));
            this.ctlWebBrowser = new System.Windows.Forms.WebBrowser();
            this.ctlStatusStrip = new System.Windows.Forms.StatusStrip();
            this.txtStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.panTop = new System.Windows.Forms.Panel();
            this.btnRetry = new System.Windows.Forms.Button();
            this.txtAddress = new System.Windows.Forms.TextBox();
            this.panEdge = new System.Windows.Forms.Panel();
            this.ctlCloseTimer = new System.Windows.Forms.Timer(this.components);
            this.btnHelp = new System.Windows.Forms.Button();
            this.ctlStatusStrip.SuspendLayout();
            this.panTop.SuspendLayout();
            this.SuspendLayout();
            // 
            // ctlWebBrowser
            // 
            this.ctlWebBrowser.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ctlWebBrowser.Location = new System.Drawing.Point(0, 38);
            this.ctlWebBrowser.MinimumSize = new System.Drawing.Size(20, 20);
            this.ctlWebBrowser.Name = "ctlWebBrowser";
            this.ctlWebBrowser.Size = new System.Drawing.Size(784, 502);
            this.ctlWebBrowser.TabIndex = 1;
            this.ctlWebBrowser.Navigated += new System.Windows.Forms.WebBrowserNavigatedEventHandler(this.ctlWebBrowser_Navigated);
            this.ctlWebBrowser.Navigating += new System.Windows.Forms.WebBrowserNavigatingEventHandler(this.ctlWebBrowser_Navigating);
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
            // panTop
            // 
            this.panTop.Controls.Add(this.btnHelp);
            this.panTop.Controls.Add(this.btnRetry);
            this.panTop.Controls.Add(this.txtAddress);
            this.panTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panTop.Location = new System.Drawing.Point(0, 0);
            this.panTop.Name = "panTop";
            this.panTop.Size = new System.Drawing.Size(784, 37);
            this.panTop.TabIndex = 6;
            // 
            // btnRetry
            // 
            this.btnRetry.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRetry.Location = new System.Drawing.Point(723, 6);
            this.btnRetry.Name = "btnRetry";
            this.btnRetry.Size = new System.Drawing.Size(49, 23);
            this.btnRetry.TabIndex = 4;
            this.btnRetry.Text = "&Retry";
            this.btnRetry.UseVisualStyleBackColor = true;
            this.btnRetry.Click += new System.EventHandler(this.btnRetry_Click);
            // 
            // txtAddress
            // 
            this.txtAddress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtAddress.Location = new System.Drawing.Point(12, 8);
            this.txtAddress.Name = "txtAddress";
            this.txtAddress.ReadOnly = true;
            this.txtAddress.Size = new System.Drawing.Size(650, 20);
            this.txtAddress.TabIndex = 2;
            // 
            // panEdge
            // 
            this.panEdge.BackColor = System.Drawing.SystemColors.ButtonShadow;
            this.panEdge.Dock = System.Windows.Forms.DockStyle.Top;
            this.panEdge.Location = new System.Drawing.Point(0, 37);
            this.panEdge.Name = "panEdge";
            this.panEdge.Size = new System.Drawing.Size(784, 1);
            this.panEdge.TabIndex = 7;
            // 
            // ctlCloseTimer
            // 
            this.ctlCloseTimer.Interval = 1000;
            this.ctlCloseTimer.Tick += new System.EventHandler(this.ctlCloseTimer_Tick);
            // 
            // btnHelp
            // 
            this.btnHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnHelp.Location = new System.Drawing.Point(668, 6);
            this.btnHelp.Name = "btnHelp";
            this.btnHelp.Size = new System.Drawing.Size(49, 23);
            this.btnHelp.TabIndex = 3;
            this.btnHelp.Text = "&Help";
            this.btnHelp.UseVisualStyleBackColor = true;
            this.btnHelp.Click += new System.EventHandler(this.btnHelp_Click);
            // 
            // YammerLoginForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 562);
            this.Controls.Add(this.ctlWebBrowser);
            this.Controls.Add(this.panEdge);
            this.Controls.Add(this.panTop);
            this.Controls.Add(this.ctlStatusStrip);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "YammerLoginForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Yammer Login";
            this.ctlStatusStrip.ResumeLayout(false);
            this.ctlStatusStrip.PerformLayout();
            this.panTop.ResumeLayout(false);
            this.panTop.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.WebBrowser ctlWebBrowser;
        private System.Windows.Forms.StatusStrip ctlStatusStrip;
        private System.Windows.Forms.ToolStripStatusLabel txtStatus;
        private System.Windows.Forms.Panel panTop;
        private System.Windows.Forms.TextBox txtAddress;
        private System.Windows.Forms.Button btnRetry;
        private System.Windows.Forms.Panel panEdge;
        private System.Windows.Forms.Timer ctlCloseTimer;
        private System.Windows.Forms.Button btnHelp;
    }
}