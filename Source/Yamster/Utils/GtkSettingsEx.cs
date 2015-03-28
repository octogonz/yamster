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
using System.Reflection;
using System.Runtime.InteropServices;
using GLib;
using Gtk;
using Yamster.Core;

namespace Yamster
{
    class GtkSettingsEx : Settings
    {
        public GtkSettingsEx(IntPtr raw)
            : base(raw)
        {
            // Don't run the finalizer, since we don't really own the "raw" pointer
            GC.SuppressFinalize(this);
        }

        public override void Dispose()
        {
            // NOOP
        }

        static PropertyInfo Info_Settings_Raw = Utilities.GetPropertyInfo(typeof(Settings), "Raw",
            BindingFlags.NonPublic | BindingFlags.Instance);

        public static new GtkSettingsEx Default
        {
            get
            {
                IntPtr raw = (IntPtr) Info_Settings_Raw.GetValue(Settings.Default, new object[0]);
                return new GtkSettingsEx(raw);
            }
        }

        public bool ButtonImages
        {
            get
            {
                Value property = base.GetProperty("gtk-button-images");
                bool flag = (bool) property;
                property.Dispose();
                return flag;
            }
            set
            {
                Value val = new Value(value);
                base.SetProperty("gtk-button-images", val);
                val.Dispose();
            }
        }

        [DllImport("libgtk-win32-2.0-0.dll", CallingConvention=CallingConvention.Cdecl)]
        private static extern IntPtr gtk_settings_get_default();
    }
}
