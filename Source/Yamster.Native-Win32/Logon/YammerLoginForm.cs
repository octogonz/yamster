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
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Yamster.Native
{
    internal partial class YammerLoginForm : Form
    {
        const string oAuthKey = "oauth_token";
        
        // The Yammer application should be configured to redirect to this URL.  Because
        // the OAuth token is appended to this URL as a fragment, it will not be transmitted
        // over the internet.
        const string oAuthRedirectUri = "https://www.yammer.com/";

        YammerLogonManager yammerLogonManager;

        readonly bool appId;

        public YammerLoginForm(YammerLogonManager yammerLogonManager)
        {
            Application.EnableVisualStyles();

            InitializeComponent();

            this.yammerLogonManager = yammerLogonManager;

            ctlWebBrowser.StatusTextChanged += ctlWebBrowser_StatusTextChanged;

            appId = yammerLogonManager.AppClientId != "";

            StartNavigation();
        }

        public bool Success { get; private set; }

        string YammerServiceUrl
        {
            get { return this.yammerLogonManager.YammerServiceUrl; }
        }

        void StartNavigation()
        {
            ctlWebBrowser.Navigate("about:blank");
            Application.DoEvents();
            txtAddress.Text = "";

            ClearCookies();

            if (appId)
            {
                string url = string.Format("{0}/dialog/oauth?response_type=token&client_id={1}&redirect_uri={2}",
                    this.YammerServiceUrl,
                    this.yammerLogonManager.AppClientId,
                    oAuthRedirectUri);

                ctlWebBrowser.Navigate(url);
            }
            else
            {
                ctlWebBrowser.Navigate(this.YammerServiceUrl + "/login");
            }
        }

        // Reset the browser session and clear cookies (for the current process only).
        // This ensures that the login starts with a clean slate.
        void ClearCookies()
        {
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_END_BROWSER_SESSION, IntPtr.Zero, 0);
            IntPtr buffer = Marshal.AllocCoTaskMem(4);
            Marshal.StructureToPtr(INTERNET_SUPPRESS_COOKIE_PERSIST, buffer, true);
            try
            {
                if (!InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SUPPRESS_BEHAVIOR, buffer, 4))
                {
                    Debug.WriteLine("InternetSetOption failed");
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(buffer);
            }
        }

        #region DllImports

        const int ERROR_INSUFFICIENT_BUFFER = 122;
        const int INTERNET_COOKIE_HTTPONLY = 0x2000;
        const int INTERNET_OPTION_END_BROWSER_SESSION = 42;
        const int INTERNET_OPTION_SUPPRESS_BEHAVIOR = 81;
        const int INTERNET_SUPPRESS_COOKIE_PERSIST = 3;

        [DllImport("wininet.dll", SetLastError = true)]
        static extern bool InternetGetCookieEx(string url, string cookieName, StringBuilder cookieData,
            ref int size, Int32 dwFlags, IntPtr lpReserved);


        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int lpdwBufferLength);

        #endregion


        private string GetOAuthCookie()
        {
            // Determine the size of the cookie
            int requiredSize = 0;

            if (!InternetGetCookieEx(this.YammerServiceUrl, oAuthKey, null, 
                ref requiredSize, INTERNET_COOKIE_HTTPONLY, IntPtr.Zero))
                return null;

            if (requiredSize <= 0)
                return null;

            StringBuilder builder = new StringBuilder(requiredSize);

            if (!InternetGetCookieEx(this.YammerServiceUrl, oAuthKey, builder,
                ref requiredSize, INTERNET_COOKIE_HTTPONLY, IntPtr.Zero))
                return null;

            // "oauth_token=xxx"
            string pair = builder.ToString();
            int equalsIndex = pair.IndexOf('=');
            if (equalsIndex < 0)
                return null;

            return pair.Substring(equalsIndex+1);
        }

        private void ctlWebBrowser_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            Debug.WriteLine("Navigating: " + e.Url);

            OnNavigate(e.Url);
        }

        private void ctlWebBrowser_Navigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            Debug.WriteLine("Navigated: " + e.Url);

            txtAddress.Text = e.Url.ToString();

            OnNavigate(e.Url);
        }

        void OnNavigate(Uri url)
        {
            string token = null;

            if (appId)
            {
                Match match = Regex.Match(url.Fragment, "#access_token=([^=#? ]+)");
                if (match.Success)
                {
                    token = match.Groups[1].Value;
                }
            }
            else
            {
                token = GetOAuthCookie();
            }

            if (!string.IsNullOrEmpty(token))
            {
                // Successfully logged in
                this.yammerLogonManager.AccessToken = token;
                this.Success = true;
                ctlWebBrowser.Stop();
                ctlCloseTimer.Enabled = true;
            }
        }

        void ctlWebBrowser_StatusTextChanged(object sender, EventArgs e)
        {
            txtStatus.Text = ctlWebBrowser.StatusText;
        }

        private void btnRetry_Click(object sender, EventArgs e)
        {
            StartNavigation();
        }

        private void ctlCloseTimer_Tick(object sender, EventArgs e)
        {
            Close();
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            string message = "Troubleshooting hints:"
                + "\r\n- Check that your login/password were typed correctly"
                + "\r\n- Sometimes SSO login fails due to intermittent network issues; try logging in again"
                + "\r\n- Make sure you can successfully login into the Yammer.com web site using Internet Explorer"
                + "\r\n- If you recently installed GTK#, you may need to reboot your PC";
            MessageBox.Show(message, "Yamster Login Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
