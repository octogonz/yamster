#region MIT License

// Yamster!
// Copyright (c) Microsoft Corporation
// All rights reserved. 
// 
// MIT License
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
// associated documentation files (the ""Software""), to deal in the Software without restriction, 
// including without limitation the rights to use, copy, modify, merge, publish, distribute, 
// sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or 
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT 
// NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#endregion

using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web;
using System.Windows.Forms;
using System.Drawing;

namespace Yamster.Native
{
    internal partial class YammerLoginForm : Form
    {
        YammerLogonManager yammerLogonManager;
        bool clearedExampleText = false;
        bool waitingForLogin = false;

        public YammerLoginForm(YammerLogonManager yammerLogonManager)
        {
            InitializeComponent();

            ctlWebBrowser.StatusTextChanged += ctlWebBrowser_StatusTextChanged;

            this.yammerLogonManager = yammerLogonManager;

            if (!string.IsNullOrWhiteSpace(yammerLogonManager.UserName))
            {
                ClearExampleText();
                txtLoginUser.Text = yammerLogonManager.UserName;
            }                
            txtLoginUser.SelectionStart = 0;
            txtLoginUser.SelectionLength = 0;

            UpdateUI();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
        }

        public bool Success { get; private set; }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            ClearExampleText();
            
            waitingForLogin = true;
            UpdateUI();

            var queryParams = new Dictionary<string, string>
                {
                    {"client_id", yammerLogonManager.ConsumerKey},
                    {"client_secret", yammerLogonManager.ConsumerSecret},
                    {"login", txtLoginUser.Text},
                };

            string[] items = queryParams.Keys
                .Select(key => String.Format("{0}={1}", key, HttpUtility.UrlEncode(queryParams[key])))
                .ToArray();

            var query = String.Join("&", items);

            string loginUrl = string.Format("{0}/sso_session/access_token?{1}", yammerLogonManager.YammerServiceUrl, query);

            ctlWebBrowser.Navigate(loginUrl);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            CancelLogin();
        }

        void CancelLogin()
        {
            waitingForLogin = false;
            UpdateUI();
            ctlWebBrowser.Stop();
            ctlWebBrowser.Navigate("about:blank");
        }

        void UpdateUI()
        {
            txtLoginUser.Enabled = !waitingForLogin;
            btnLogin.Enabled = !waitingForLogin;
            btnCancel.Enabled = waitingForLogin;
        }

        private void ctlWebBrowser_Navigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            var ssoUrlParser = new SsoUrlParser(e.Url);
            if (ssoUrlParser.Status == SsoUrlParser.CompleteStatus.Success)
            {
                // Delegate.Succeeded(ssoUrlParser.AccessToken, ssoUrlParser.RefreshToken);
                yammerLogonManager.AccessToken = ssoUrlParser.AccessToken;
                Success = true;
                Close();
            }
            else if (ssoUrlParser.Status == SsoUrlParser.CompleteStatus.Error)
            {
                // Delegate.Failed(ssoUrlParser.ErrorMessage);
                ctlWebBrowser.Stop();
                string error = (ssoUrlParser.ErrorMessage ?? "").Trim();
                if (error == "")
                    error = "A general error occurred";
                string message = "Login failed: " + error
                    + "\r\n\r\nTroubleshooting hints:"
                    + "\r\n- Check that your login/password were typed correctly"
                    + "\r\n- Sometimes SSO login fails due to intermittent network issues; try logging in again"
                    + "\r\n- Make sure you can successfully login into the Yammer.com web site using Internet Explorer"
                    + "\r\n- Yammer networks requiring non-SSO authentication are not supported yet"
                    + "\r\n- If you recently installed GTK#, you may need to reboot your PC";
                MessageBox.Show(message, "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                CancelLogin();
            }
            else if (ssoUrlParser.Status == SsoUrlParser.CompleteStatus.Redirect)
            {
                //Delegate.Redirected(ssoUrlParser.RedirectUrl);
            }
        }

        private void txtLoginUser_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (waitingForLogin)
                return;
            if (e.KeyChar == '\r')
                btnLogin_Click(sender, e);

            ClearExampleText();
        }

        private void txtLoginUser_MouseDown(object sender, MouseEventArgs e)
        {
            ClearExampleText();
        }

        void ClearExampleText()
        {
            if (!clearedExampleText)
            {
                clearedExampleText = true;
                txtLoginUser.Text = "";
                txtLoginUser.ForeColor = SystemColors.WindowText;
            }
        }

        void ctlWebBrowser_StatusTextChanged(object sender, EventArgs e)
        {
            txtStatus.Text = ctlWebBrowser.StatusText;
        }
    }
}
