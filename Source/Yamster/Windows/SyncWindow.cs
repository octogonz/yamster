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
using Yamster.Core.SQLite;
using Yamster.Core;

namespace Yamster
{
    public enum SyncWindowStatus
    {
        Offline,
        Syncing,
        Throttled,
        UpToDate,
        Error
    }

    public partial class SyncWindow : Gtk.Window
    {
        public event EventHandler StatusChanged;

        // TOOD: Push this state down into the MessagePuller object
        SyncWindowStatus status = SyncWindowStatus.Offline;

        AppContext appContext;
        YamsterCoreDb yamsterCoreDb;
        MessagePuller messagePuller;

        DateTime lastDatabaseSave = DateTime.Now;

        bool destroyed = false;
        int lastServiceCallTime = 0;

        public SyncWindow() :
            base(Gtk.WindowType.Toplevel)
        {
            appContext = AppContext.Default;

            this.Build();

#if !DEBUG
            btnOnce.Visible = false;
#endif

            yamsterCoreDb = appContext.YamsterCoreDb;
            messagePuller = appContext.MessagePuller;

            messagePuller.CallingService += messagePuller_CallingService;
            messagePuller.UpdatedDatabase += messagePuller_UpdatedDatabase;
            messagePuller.Error += messagePuller_Error;
            messagePuller.EnabledChanged += messagePuller_EnabledChanged;

            radAlgorithmBulkDownload.Active = true;

            UpdateUI();

            GLib.Timeout.Add(1000, OnPollTimer);
        }

        public override void Destroy()
        {
            if (!destroyed)
            {
                if (messagePuller != null)
                {
                    messagePuller.CallingService -= messagePuller_CallingService;
                    messagePuller.UpdatedDatabase -= messagePuller_UpdatedDatabase;
                    messagePuller.Error -= messagePuller_Error;
                    messagePuller = null;
                }
            }
            destroyed = true;
            base.Destroy();
        }

        protected override void OnShown()
        {
            base.OnShown();
        }

        protected override void OnHidden()
        {
            base.OnHidden();
        }

        protected override bool OnDeleteEvent(Gdk.Event evnt)
        {
            // When the user clicks [x] to close the window, this override causes it
            // to be hidden instead, so that we can call show() again later.
            this.Hide();
            return true;
        }

        public bool IsSyncing
        {
            get { return this.messagePuller.Enabled; }
            set
            {
                this.messagePuller.Enabled = value;
            }
        }
        public SyncWindowStatus Status
        {
            get { return this.status; } 
            set
            {
                if (this.status == value)
                    return;
                this.status = value;
                if (StatusChanged != null)
                    StatusChanged(this, EventArgs.Empty);
            }
        }

        void UpdateUI()
        {
            if (destroyed)
                return;

            btnStart.Sensitive = !this.IsSyncing;
            btnStop.Sensitive = this.IsSyncing;
            btnOnce.Sensitive = !this.IsSyncing;

            txtHistoryLimitDays.Sensitive = chkHistoryLimitDays.Sensitive;
            lblHistoryLimitDays.Sensitive = chkHistoryLimitDays.Sensitive;

            lblHistoryLimitDaysHint.Visible = chkHistoryLimitDays.Active;
            if (lblHistoryLimitDaysHint.Visible) 
            {
                int days = (int)txtHistoryLimitDays.Value;
                DateTime historyLimit = DateTime.Now.Date.Subtract(TimeSpan.FromDays(days));
                lblHistoryLimitDaysHint.Text = historyLimit.ToString("M/d/yyyy");
            }
        }

        bool OnPollTimer()
        {
            if (destroyed)
                return false;
            try
            {
                if (this.IsSyncing)
                    SyncOnce();
            }
            catch (Exception ex)
            {
                txtSyncStatus.Text = "ERROR: " + ex.Message;
                this.IsSyncing = false;
                this.Status = SyncWindowStatus.Error;
                Utilities.ShowApplicationException(ex);
            }
            return true;
        }

        void messagePuller_EnabledChanged(object sender, EventArgs e)
        {
            if (!messagePuller.Enabled)
            {
                txtSyncStatus.Text = "Offline";
                this.Status = SyncWindowStatus.Offline;
            }
            else
            {
                this.Status = SyncWindowStatus.Syncing;
            }
            UpdateUI();
        }

        void messagePuller_UpdatedDatabase(object sender, EventArgs e)
        {
            txtTotalMessages.Text = yamsterCoreDb.Messages.GetCount().ToString("#,##0");
        }

        void messagePuller_CallingService(object sender, MessagePullerCallingServiceEventArgs e)
        {
            if (e.ThreadId != null)
            {
                txtSyncStatus.Text = "Fetching messages...";
            }
            else
            {
                txtSyncStatus.Text = "Fetching group index...";
            }
            lastServiceCallTime = Environment.TickCount;

            if (e.FeedId == YamsterGroup.InboxFeedId)
            {
                txtSyncGroup.Text = "(Inbox)";
            }
            else
            {
                var group = yamsterCoreDb.Groups.Query("WHERE GroupId=" + e.FeedId + "").FirstOrDefault();

                if (group != null && !string.IsNullOrEmpty(group.GroupName))
                {
                    txtSyncGroup.Text = group.GroupName;
                }
                else
                {
                    txtSyncGroup.Text = "Group #" + e.FeedId;
                }
            }

            // Repaint the text boxes
            Utilities.ProcessApplicationEvents();
        }

        void messagePuller_Error(object sender, MessagePullerErrorEventArgs e)
        {
            if (!(e.Exception is RateLimitExceededException)
               && !(e.Exception is YamsterEmptyResultException))
            {
                this.IsSyncing = false;
                Utilities.ShowApplicationException(e.Exception);
            }
        }

        protected void btnStart_Click(object sender, EventArgs e)
        {
            this.IsSyncing = true;
        }

        protected void btnStop_Click(object sender, EventArgs e)
        {
            this.IsSyncing = false;
        }

        protected void btnOnce_Click(object sender, EventArgs e)
        {
            try
            {
                this.IsSyncing = true;
                SyncOnce();
            }
            finally
            {
                this.IsSyncing = false;
            }
        }

        protected void chkHistoryLimitDays_Toggled (object sender, EventArgs e)
        {
            UpdateUI();
        }

        protected void txtHistoryLimitDays_ValueChanged (object sender, EventArgs e)
        {
            UpdateUI();
        }

        void SyncOnce()
        {
            messagePuller.Algorithm = radAlgorithmBulkDownload.Active
                ? MessagePullerAlgorithm.BulkDownload
                : MessagePullerAlgorithm.OptimizeReading;

            if (chkHistoryLimitDays.Active)
            {
                messagePuller.HistoryLimitDays = (int)txtHistoryLimitDays.Value;
            }
            else
            {
                messagePuller.HistoryLimitDays = 0;
            }

            messagePuller.Process();

            if (messagePuller.HistoryProgress != null)
            {
                txtGroupHistory.Text = messagePuller.HistoryProgress.Value.ToString();
            }

            if (this.appContext.YamsterApi.Throttled)
            {
                txtSyncStatus.Text = "A request was throttled due to Yammer rate limits";
                this.Status = SyncWindowStatus.Throttled;
            }
            else if (messagePuller.UpToDate)
            {
                txtSyncStatus.Text = "Up to date.";
                this.Status = SyncWindowStatus.UpToDate;
            }
            else
            {
                // If it's been more than 3 seconds since the last service call, then we're waiting
                if (unchecked(Environment.TickCount - lastServiceCallTime) > 3000)
                    txtSyncStatus.Text = "Waiting...";
                this.Status = SyncWindowStatus.Syncing;
            }
        }
    }
}

