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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Yamster.Native
{
    public class YammerLogonManager
    {
        public string ConsumerKey { get; private set; }
        public string ConsumerSecret { get; private set; }
        public string YammerServiceUrl { get; private set; }

        public string UserName { get; set; }
        public string AccessToken { get; set; }

        public YammerLogonManager(string consumerKey, string consumerSecret, string yammerServiceUrl)
        {
            this.ConsumerKey = consumerKey;
            this.ConsumerSecret = consumerSecret;
            this.YammerServiceUrl = yammerServiceUrl;

            UserName = "";
            AccessToken = "";
        }

        public bool ShowLoginForm()
        {
            using (YammerLoginForm form = new YammerLoginForm(this))
            {
                try
                {
                    // Process GTK messages for the background window while 
                    // we're running the WinForms message loop
                    Application.Idle += Application_Idle;
                    form.ShowDialog();
                    return form.Success;
                }
                finally
                {
                    Application.Idle -= Application_Idle;
                }
            }
        }

        void Application_Idle(object sender, EventArgs e)
        {
            if (YamsterNative.GtkMessagePump != null)
                YamsterNative.GtkMessagePump();
        }
    }
}
