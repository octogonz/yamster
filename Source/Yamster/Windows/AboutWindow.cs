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

using System.IO;
using System;
using System.Diagnostics;
using Gdk;
using Yamster.Core;

namespace Yamster
{
    public partial class AboutWindow : Gtk.Window
    {
        const string LicenseFilename = "Yamster-License.txt";

        public AboutWindow() : 
            base(Gtk.WindowType.Toplevel)
        {
            this.Build();
            ModifyBg(Gtk.StateType.Normal, new Color(128,128,128));
            ctlLeftBox.ModifyBg(Gtk.StateType.Normal, new Color(239,239,239));
            ctlRightBox.ModifyBg(Gtk.StateType.Normal, new Color(255,255,255));

            lblBuildNumber.LabelProp = Utilities.YamsterVersion;
        }

        protected override void OnShown()
        {
            base.OnShown();

            // Don't focus any control
            this.Focus = null;
        }

        protected void Window_KeyPress(object o, Gtk.KeyPressEventArgs args)
        {
            this.Destroy();
        }

        protected void Window_ButtonRelease(object o, Gtk.ButtonReleaseEventArgs args)
        {
            this.Destroy();
        }

        protected void lblLicense_ButtonPress(object o, Gtk.ButtonPressEventArgs args)
        {
            this.Destroy();
            args.RetVal = true;

            ShowLicense();
        }

        protected void lblHelpManual_ButtonPress(object o, Gtk.ButtonPressEventArgs args)
        {
            this.Destroy();
            args.RetVal = true;

            ShowHelpManual();
        }

        protected void lblDiscussionGroup_ButtonPress(object o, Gtk.ButtonPressEventArgs args)
        {
            this.Destroy();
            args.RetVal = true;

            ShowDiscussionGroup();
        }

        protected void lblWebSite_ButtonPress(object o, Gtk.ButtonPressEventArgs args)
        {
            this.Destroy();
            args.RetVal = true;

            ShowWebSite();
        }

        internal static void ShowLicense()
        {
            string fullPath = System.IO.Path.Combine(Utilities.ApplicationExeFolder, LicenseFilename);
            Utilities.OpenDocumentInDefaultBrowser(fullPath);
        }

        internal static void ShowHelpManual()
        {
            Process.Start("https://yamster.codeplex.com/documentation");
        }

        internal static void ShowDiscussionGroup()
        {
            Process.Start("https://yamster.codeplex.com/discussions");
        }

        internal static void ShowWebSite()
        {
            Process.Start("https://yamster.codeplex.com/");
        }

    }
}
