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
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Yamster
{
    public static class YamsterNative
    {
        // Called from the WinForms message loop to process messages for 
        // the Gtk application framework
        public static Action GtkMessagePump = null;

        public static void RunAndCatchAssemblyLoadExceptions(Action action)
        {
            string errorMessage = null;

            try
            {
                action();
            }
            catch (FileNotFoundException ex)
            {
                if (Regex.IsMatch(ex.Message, "glib|gtk", RegexOptions.IgnoreCase))
                {
                    errorMessage = "Yamster! requires the GTK# third party library,"
                      + " which does not appear to be installed on your PC.";
                }
                else
                {
                    errorMessage = "The application was unable to start because a required"
                        + " library package is not installed:\r\n\r\n" + ex.Message;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "The application was unable to start:"
                    + "\r\n\r\n" + ex.GetType().Name + ": " + ex.Message;
            }
            
            if (errorMessage != null)
            {
                MessageBox.Show(errorMessage
                    + "\r\n\r\nFollow the installation steps in the Yamster documentation."
                    + "  If that doesn't help, please report your issue so we can fix it!",
                    "Yamster!", MessageBoxButtons.OK, MessageBoxIcon.Stop);

                Process.Start("https://yamster.codeplex.com/documentation");
            }
        }
    }
}
