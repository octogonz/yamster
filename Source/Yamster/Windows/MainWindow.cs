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
using System.Linq;
using Yamster.Core.SQLite;
using Gtk;
using Yamster.Core;

namespace Yamster
{

    public partial class MainWindow : Gtk.Window
    {
        AppContext appContext;
        YamsterCache yamsterCache;

        SyncWindow syncWindow = null;
        ActionLagger updateStatusBarLagger;

        public static int ChatPaneWidth { get; private set; }

        public MainWindow()
            : base(Gtk.WindowType.Toplevel)
        {
            appContext = AppContext.Default;
            yamsterCache = appContext.YamsterCache;

            MainWindow.ChatPaneWidth = appContext.Settings.ChatPaneWidth;

            Build();

            this.ctlChatPaneVBox.WidthRequest = MainWindow.ChatPaneWidth;

#if YAMSTER_MAC
            ctlMenuBar.Visible = false;
#endif
            ctlStatusBarMain.Push(0, "  Version " + Utilities.YamsterTerseVersion + "  -  https://github.com/octogonz/yamster");
            ctlStatusBarTotalYams.Push(0, "");
            SetStatusBarFormat(ctlStatusBarTotalYams, x => x.Xalign = 0.5f);
            ctlStatusBarSyncStatus.Push(0, "");
            SetStatusBarFormat(ctlStatusBarSyncStatus, x => x.Xalign = 0.5f);

            yamsterCache.MessageChanged += yamsterCache_MessageChanged;

            ctlViewsScreen.ThreadViewer = ctlThreadViewer;
            ctlGroupThreadScreen.ThreadViewer = ctlThreadViewer;
            ctlSearchScreen.ThreadViewer = ctlThreadViewer;
            ctlConversationsScreen.ThreadViewer = ctlThreadViewer;

            ctlGroupConfigScreen.PerformLogin += ctlGroupConfigScreen_PerformLogin;

            updateStatusBarLagger = new ActionLagger(UpdateStatusBarAction);
            updateStatusBarLagger.RequestAction();

#if !DEBUG
            btnTest.Visible = false;
#endif
        }

        public override void Destroy()
        {
            updateStatusBarLagger.Dispose();
            if (syncWindow != null)
            {
                syncWindow.Destroy();
                syncWindow = null;
            }
            base.Destroy();
        }

        protected void OnDeleteEvent(object sender, DeleteEventArgs a)
        {
            Application.Quit();
            a.RetVal = true;
        }

        protected void btnTest_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("Test button was clicked");
        }

        protected void btnSync_Click(object sender, EventArgs e)
        {
            if (!PerformLogin())
                return;

            if (syncWindow == null)
            {
                syncWindow = new SyncWindow();
                syncWindow.StatusChanged += syncWindow_StatusChanged;
            }
            syncWindow.Modal = false;
            syncWindow.Show();
        }

        void syncWindow_StatusChanged(object sender, EventArgs e)
        {
            updateStatusBarLagger.RequestAction();
        }

        void ctlGroupConfigScreen_PerformLogin(object sender, EventArgs e)
        {
            PerformLogin();
        }

        bool PerformLogin(bool forceLoginPrompt = false)
        {
            if (!forceLoginPrompt)
            {
                // First try to log in with the existing credentials
                try
                {
                    appContext.YamsterApi.TestServerConnection();
                    return true;
                }
                catch (Exception ex)  // TokenNotFoundException, WebException
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            var yammerLogonManager = new Yamster.Native.YammerLogonManager(
                appContext.Settings.YammerServiceUrl,
                appContext.Settings.AppClientId);

            if (!yammerLogonManager.ShowLoginForm())
                return false;

            appContext.UserManager.LoginWith(yammerLogonManager.AccessToken);
            return true;
            
        }

        void SetStatusBarMessage(Statusbar statusBarPanel, string message)
        {
            statusBarPanel.Pop(0);
            statusBarPanel.Push(0, message);
        }

        void SetStatusBarFormat(Statusbar statusBarPanel, Action<Label> action)
        {
            Container container = statusBarPanel.Children.FirstOrDefault() as Container;
            if (container == null)
                return;
            container = container.Children.FirstOrDefault() as Container;
            if (container == null)
                return;
            Label label = container.Children.FirstOrDefault() as Label;
            if (label == null)
                return;
            action(label);
        }

        void yamsterCache_MessageChanged(object sender, YamsterMessageChangedEventArgs e)
        {
            switch (e.ChangeType)
            {
                case YamsterModelChangeType.Added:
                    updateStatusBarLagger.RequestAction();
                    break;
                case YamsterModelChangeType.PropertyChanged:
                    break;
            }
        }

        void UpdateStatusBarAction()
        {
            // NOTE: This counts deleted and invisible messages
            var count = appContext.YamsterCoreDb.Messages.GetCount();
            string message = string.Format("{0:#,##0} yams in DB", count);
            SetStatusBarMessage(ctlStatusBarTotalYams, message);

            var syncStatus = this.syncWindow != null ? this.syncWindow.Status : SyncWindowStatus.Offline;

            switch (syncStatus)
            {
                case SyncWindowStatus.Syncing:
                    SetStatusBarMessage(ctlStatusBarSyncStatus, "Syncing...");
                    break;
                case SyncWindowStatus.UpToDate:
                    SetStatusBarMessage(ctlStatusBarSyncStatus, "Up to date");
                    break;
                case SyncWindowStatus.Error:
                    SetStatusBarMessage(ctlStatusBarSyncStatus, "Sync Error!");
                    break;
                case SyncWindowStatus.Throttled:
                    SetStatusBarMessage(ctlStatusBarSyncStatus, "> Throttled <");
                    break;
                case SyncWindowStatus.Offline:
                default:
                    SetStatusBarMessage(ctlStatusBarSyncStatus, "Offline");
                    break;
            }
        }

        protected void btnAboutBox_Clicked(object sender, EventArgs e)
        {
            using (var window = new AboutWindow())
            {
                Utilities.RunWindowAsDialog(window);
            }
        }

        #region Main Menu

        protected void mnuFileYammerSync_Activated(object sender, EventArgs e)
        {
            btnSync_Click(sender, e);
        }

        protected void mnuFileExit_Activated(object sender, EventArgs e)
        {
            Application.Quit();
        }

        protected void mnuHelpManual_Activated(object sender, EventArgs e)
        {
            AboutWindow.ShowHelpManual();
        }

        protected void mnuHelpDiscussionGroup_Activated(object sender, EventArgs e)
        {
            AboutWindow.ShowDiscussionGroup();
        }

        protected void mnuHelpWebSite_Activated(object sender, EventArgs e)
        {
            AboutWindow.ShowWebSite();
        }

        protected void mnuHelpAbout_Activated(object sender, EventArgs e)
        {
            btnAboutBox_Clicked(sender, e);
        }

        protected void mnuYammerFullResync_Activated(object sender, EventArgs e)
        {
            if (this.syncWindow != null && this.syncWindow.Status != SyncWindowStatus.Offline)
            {
                Utilities.ShowMessageBox(
                    "This operation cannot be performed while syncing is active."
                    + "\r\n\r\nStop the sync engine first.",
                    "Yamster",
                    Gtk.ButtonsType.Ok, Gtk.MessageType.Error);
                return;
            }

            if (Utilities.ShowMessageBox(
                "This will ask the sync engine to refetch all messages for all subscribed"
                + " Yammer groups.  No data will be deleted, but it may take a long time"
                + " to get back in sync.  It is an expensive operation."
                + "\r\n\r\nAre you sure you want to proceed?",
                "Yamster",
                Gtk.ButtonsType.YesNo, Gtk.MessageType.Question) != Gtk.ResponseType.Yes)
                return;
            
            appContext.MessagePuller.RequestFullResync();

            Utilities.ShowMessageBox(
                "The sync progress has been reset.  It will take effect the next time you start syncing.",
                "Yamster",
                Gtk.ButtonsType.Ok, Gtk.MessageType.Info);
                return;
        }

        #endregion

    }
}
