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
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Gtk;

namespace Yamster.Core
{
    public enum OptionalBool
    {
        Unspecified = 0,
        False,
        True
    }

    public enum YamsterMessagesRead
    {
        /// <summary>
        /// No messages have been read.
        /// </summary>
        None,
        /// <summary>
        /// Some messages have been read.
        /// </summary>
        Some,
        /// <summary>
        /// All messages have been read.
        /// </summary>
        All
    }

    public static class Utilities
    {
        /// <summary>
        /// Returns a text summary including inner exceptions and (in debug builds) the call stack
        /// </summary>
        public static string GetExceptionSummary(Exception ex, bool omitCallstack)
        {
            string indent = "  --> ";
            string message = indent + ex.Message;

            // Inner exceptions generally give more information, so prepend them.
            Exception inner = ex.InnerException;
            while (inner != null)
            {
                if (inner.Message != "")
                    message = indent + inner.Message + "\r\n\r\n" + message;
                inner = inner.InnerException;
            }

#if DEBUG
            if (!omitCallstack)
                message += "\r\n\r\n" + ex.StackTrace;
#endif
            return message;
        }
        /// <inheritdoc cref="GetExceptionSummary(Exception,bool)" /> 
        public static string GetExceptionSummary(Exception ex)
        {
            return GetExceptionSummary(ex, false);
        }

        public static void ShowApplicationException(Exception exception)
        {
            ShowMessageBox("An error occurred:\r\n" + Utilities.GetExceptionSummary(exception, true), 
                "Yamster", ButtonsType.Ok, MessageType.Error);
        }

        public static MethodInfo GetMethodInfo(Type containingType, string methodName, 
            BindingFlags bindingFlags = BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance)
        {
            var result = containingType.GetMethod(methodName, bindingFlags);
            if (result == null)
            {
                throw new InvalidOperationException("Unable to bind method " + containingType.Name
                    + "." + methodName);
            }
            return result;
        }

        public static PropertyInfo GetPropertyInfo(Type containingType, string propertyName,
            BindingFlags bindingFlags = BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance)
        {
            var result = containingType.GetProperty(propertyName, bindingFlags);
            if (result == null)
            {
                throw new InvalidOperationException("Unable to bind property " + containingType.Name
                    + "." + propertyName);
            }
            return result;
        }

        public static void OpenDocumentInDefaultBrowser(string filename)
        {
            if (!File.Exists(filename))
            {
                throw new FileNotFoundException("File not found:\r\n\r\n" + filename, filename);
            }

#if YAMSTER_MAC
            GLib.Process.SpawnCommandLineAsync("/usr/bin/open \"" + filename + "\"");
#else
            Process.Start(filename);
#endif
        }

        public static ResponseType ShowMessageBox(string message, string title, ButtonsType buttonsType, MessageType messageType)
        {
            MessageDialog dialog = null;
            try
            {
                dialog = new MessageDialog(GetForegroundWindow(), DialogFlags.Modal, messageType, buttonsType, 
                    false, "{0}", message);
                dialog.Title = title;
                dialog.SetPosition(WindowPosition.CenterOnParent);
                return (ResponseType)dialog.Run();
            }
            finally
            {
                if (dialog != null)
                    dialog.Destroy();
            }
        }

        static Window GetForegroundWindow()
        {
            foreach (Window window in Window.ListToplevels())
                if (window.HasToplevelFocus)
                    return window;
            return null;
        }

        public static void RunWindowAsDialog(Window window, Window parentWindow = null)
        {
            if (parentWindow == null)
                parentWindow = GetForegroundWindow();

            window.Modal = true;
            window.TransientFor = parentWindow;
            // window.Parent = parentWindow;
            // window.ParentWindow = parentWindow.GdkWindow;

            // Workaround -- not sure why this is broken
            if (window.WindowPosition == WindowPosition.CenterOnParent && parentWindow != null)
            {
                window.SetPosition(WindowPosition.None);

                int width; int height;
                window.GetSize(out width, out height);

                int parentX; int parentY;
                parentWindow.GetPosition(out parentX, out parentY);
                int parentWidth; int parentHeight;
                parentWindow.GetSize(out parentWidth, out parentHeight);

                window.Move(parentX + parentWidth / 2 - width / 2, parentY + parentHeight / 2 - height / 2);
            }

            window.Show();
            while (window.Visible)
                Application.RunIteration(true);
        }
        
        /// <summary>
        /// Equivalent to Windows.Forms.Application.DoEvents()
        /// </summary>
        public static void ProcessApplicationEvents()
        {
            // The GTK Reference Manual gives this example for "Updating the UI during
            // a long computation":
            // while (gtk_events_pending()) gtk_main_iteration();
            //
            // However, this sometimes spins in an infinite loop that consumes 100% CPU.
            // ProcessApplicationEvents() implements the timeout idea suggested here:
            // http://stackoverflow.com/questions/21271484/gtk-events-pending-returns-false-with-events-still-pending

            int timeout = Environment.TickCount + 50; // wait a maximum of 50 ms
            while (Gtk.Application.EventsPending())
            {
                Gtk.Application.RunIteration(blocking: false);
                if (Environment.TickCount > timeout)
                {
                    Debug.WriteLine("Warning: ProcessApplicationEvents() got stuck");
                    break;
                }
            }
        }

        public static string TruncateWithEllipsis(string text, int maxLength)
        {
            if (maxLength > 0)
            {
                int maxWithEllipsis = Math.Max(maxLength - 3, 0);
                if (text.Length > maxWithEllipsis)
                    text = text.Substring(0, maxWithEllipsis) + "...";
            }
            return text;
        }

        /// <summary>
        /// Allows performing a binary search that matches an arbitrary key, i.e. to avoid
        /// constructing an object that is "equal to" the desired object.
        /// The "comparisonPredicate" is a delegate that returns an integer akin to
        /// to IComparer.Compare(listItem, itemToFind).  The return value is the index of
        /// the matching item, or else the bitwise complement familiar from Array.BinarySearch().
        /// </summary>
        public static int CustomBinarySearch<T>(IList<T> list, Func<T, int> comparisonPredicate)
        {
            int leftIndex = 0;
            int rightIndex = list.Count - 1;
            while (leftIndex <= rightIndex)
            {
                int middleIndex = leftIndex + ((rightIndex - leftIndex) >> 1);
                int comparisonResult = comparisonPredicate(list[middleIndex]);
                if (comparisonResult == 0)
                {
                    return middleIndex;
                }
                if (comparisonResult < 0)
                {
                    leftIndex = middleIndex + 1;
                }
                else
                {
                    rightIndex = middleIndex - 1;
                }
            }
            return ~leftIndex;
        }

        // Equivalent to Windows.Forms.Application.ExecutablePath
        public static string ApplicationExePath
        {
            get
            {
                string exePath = Path.GetFullPath(Assembly.GetExecutingAssembly().Location);
                return exePath;
            }
        }

        // The directory containing the application EXE file.
        public static string ApplicationExeFolder
        {
            get
            {
                return Path.GetDirectoryName(ApplicationExePath);
            }
        }

        public static string YamsterTerseVersion
        {
            get
            {
                var version = typeof(Utilities).Assembly.GetName().Version;
                string versionString = version.ToString(3);
                return versionString;
            }
        }

        public static string YamsterVersion
        {
            get
            {
                string versionString = YamsterTerseVersion;
#if YAMSTER_MAC
                versionString += " for Mac";
#else
                versionString += " for Win32";
#endif
                return versionString;
            }
        }

        #region GtkVersion property

        static Version gtkVersion = null;
        public static Version GtkVersion
        {
            get
            {
#if YAMSTER_MAC
                return new Version();
#else
                if (gtkVersion == null)
                {
                    gtkVersion = new Version();
                    IntPtr libraryHandle = LoadLibrary("libgtk-win32-2.0-0.dll");
                    if (libraryHandle != IntPtr.Zero)
                    {
                        try
                        {
                            IntPtr procAddress;
                            procAddress = GetProcAddress(libraryHandle, "gtk_major_version");
                            if (procAddress != IntPtr.Zero)
                            {
                                int major = Marshal.ReadInt32(procAddress);
                                procAddress = GetProcAddress(libraryHandle, "gtk_minor_version");
                                int minor = Marshal.ReadInt32(procAddress);
                                procAddress = GetProcAddress(libraryHandle, "gtk_micro_version");
                                int micro = Marshal.ReadInt32(procAddress);
                                gtkVersion = new Version(major, minor, micro);
                            }
                        }
                        finally
                        {
                            FreeLibrary(libraryHandle);
                        }
                    }
                }
                return gtkVersion;
#endif
            }
        }

#if !YAMSTER_MAC
        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)]string lpFileName);

        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);
#endif
        #endregion
    }
}
