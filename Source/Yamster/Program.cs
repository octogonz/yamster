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
using GLib;
using Gtk;
using Yamster.Core;

namespace Yamster
{
    class MainClass
    {
        // NOTE: This version does not match Xamarin's installers.
        //
        // GtkVersion  Installer Version
        // ----------  -----------------
        // 2.24.20     2.12.22
        // 2.24.22     2.12.25
        static readonly Version RequiredGtkVersion = new Version(2, 24, 20);

        [STAThread]
        public static void Main(string[] args)
        {
#if !YAMSTER_MAC
            YamsterNative.RunAndCatchAssemblyLoadExceptions(() => { Startup(); });
#else
            Startup();
#endif
        }

        static void Startup()
        {
            try
            {
                ExceptionManager.UnhandledException += ExceptionManager_UnhandledException;

                Application.Init();

#if !YAMSTER_MAC
                if (Utilities.GtkVersion < RequiredGtkVersion)
                {
                    Utilities.ShowMessageBox(
                        string.Format("Yamster requires GTK# version {0}, but the installed version is {1}."
                            + "\r\n\r\nPlease download and install the latest version.",
                            RequiredGtkVersion, Utilities.GtkVersion),
                        "Yamster!",
                        ButtonsType.Ok,
                        MessageType.Warning
                    );
                    System.Diagnostics.Process.Start("http://monodevelop.com/Download");
                    return;
                }

                YamsterNative.GtkMessagePump = delegate {
                    Utilities.ProcessApplicationEvents();
                };
#endif

                AppContext.InitializeDefaultInstance();
                var appContext = AppContext.Default;
                appContext.ConnectDatabase(
                    (sender, eventArgs) => {
                        string question = eventArgs.GetUpgradeMessage()
                                + "\r\n\r\nProceed with the upgrade?";
                        if (Utilities.ShowMessageBox(question,
                           "Yamster Database Upgrade", ButtonsType.OkCancel, MessageType.Question)
                            != ResponseType.Ok)
                        {
                            eventArgs.CancelUpgrade = true;
                        }
                    },
                    (sender, eventArgs) => {
                        Utilities.ShowMessageBox("Upgrade completed successfully.",
                           "Yamster Database Upgrade", ButtonsType.Ok, MessageType.Info);
                    }
                );

                GtkSynchronizationContext.Install(AppContext.Default);

                // SQLiteMapper.DefaultLogLevel = SQLiteMapperLogLevel.SqlTrace;

                // The MessagePuller is initially disabled
                appContext.MessagePuller.Enabled = false;

                // Configure the UI styles
                string gtkRcFile = Path.Combine(Utilities.ApplicationExeFolder, "GtkRc.cfg");
                if (!File.Exists(gtkRcFile))
                    throw new FileNotFoundException("The GtkRc.cfg config file is missing", gtkRcFile);
                Rc.Parse(gtkRcFile);

                // TODO: For now, this is turned off because the Yamster UI doesn't have any button
                // images yet.  When we turn it on, we need to somehow disable the unprofessional
                // stock images shown by GTK message dialogs.
                GtkSettingsEx.Default.ButtonImages = false;

                MainWindow win = new MainWindow();
                win.Show();
                Application.Run();
            }
            catch (Exception ex)
            {
                Utilities.ShowApplicationException(ex);
            }
            AppContext.UninitializeDefaultInstance();
        }

        static void ExceptionManager_UnhandledException(UnhandledExceptionArgs args)
        {
            Exception exception = args.ExceptionObject as Exception;
            if (exception == null)
                exception = new Exception("Unknown exception");
            Utilities.ShowApplicationException(exception);
            // args.ExitApplication = true;
        }
    }
}
