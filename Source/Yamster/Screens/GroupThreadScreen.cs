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
using Gdk;
using Yamster.Core;

namespace Yamster
{
    // This is the "Browse" tab on the MainWindow.  It shows groups on the left,
    // and threads on the right.
    // (NOTE: The ThreadViewer on the right is part of the MainWindow,
    // and this screen interacts with it via the MessageFocused event.)
    [System.ComponentModel.ToolboxItem(true)]
    public partial class GroupThreadScreen : Gtk.Bin
    {
        AppContext appContext;
        YamsterCache yamsterCache;
        ActionLagger reloadGroupsLagger;
        ActionLagger reloadThreadsLagger;

        public GroupThreadScreen()
        {
            appContext = AppContext.Default;
            yamsterCache = appContext.YamsterCache;

            this.Build();

            ctlGroupGrid.Columns[GroupGridColumn.ShouldSync].Visible = false;
            ctlGroupGrid.Columns[GroupGridColumn.TrackRead].Visible = false;

            reloadGroupsLagger = new ActionLagger(ReloadGroupsAction);
            reloadThreadsLagger = new ActionLagger(ReloadThreadsAction);

            reloadGroupsLagger.RequestAction();

            yamsterCache.GroupChanged += yamsterCache_GroupChanged;
            yamsterCache.ThreadChanged += yamsterCache_ThreadChanged;
        }

        public override void Destroy()
        {
            this.reloadThreadsLagger.Dispose();
            this.reloadThreadsLagger.Dispose();

            yamsterCache.GroupChanged -= yamsterCache_GroupChanged;
            yamsterCache.ThreadChanged -= yamsterCache_ThreadChanged;
            base.Destroy();
        }

        public ThreadViewer ThreadViewer { get; set; }

        protected void ctlGroupGrid_FocusedItemChanged(object sender, EventArgs e)
        {
            ReloadThreads();
        }

        protected void ctlThreadGrid_FocusedItemChanged(object sender, EventArgs e)
        {
            if (ThreadViewer != null && ctlThreadGrid.FocusedItem != null)
            {
                ThreadViewer.LoadThread(ctlThreadGrid.FocusedItem);
            }
        }

        public void ReloadThreads()
        {
            reloadThreadsLagger.RequestAction();
        }

        void ReloadThreadsAction()
        {
            var group = ctlGroupGrid.FocusedItem;
            if (group != null)
            {
                var threads = group.GetThreadsSortedByUpdate();

                bool showReadThreads = chkShowReadThreads.Active || !group.TrackRead;

                if (showReadThreads)
                {
                    ctlThreadGrid.LoadThreads(threads);
                }
                else
                {
                    ctlThreadGrid.LoadThreads(threads.Where(x => !x.Read).ToArray());
                }

                chkShowReadThreads.Sensitive = group.TrackRead;
                lblMarkAllRead.Sensitive = group.TrackRead;
                ctlThreadGrid.TrackRead = group.TrackRead;
            }
            else
            {
                ctlThreadGrid.LoadThreads(new YamsterThread[0]);
                chkShowReadThreads.Sensitive = false;
                lblMarkAllRead.Sensitive = false;
                ctlThreadGrid.TrackRead = true;
            }
        }

        void yamsterCache_ThreadChanged(object sender, YamsterThreadChangedEventArgs e)
        {
            var thread = e.Thread;
            switch (e.ChangeType)
            {
                case YamsterModelChangeType.PropertyChanged:
                    // Handled by ThreadGrid
                    break;
                case YamsterModelChangeType.Added:
                    // This is an incremental equivalent of the filtering logic
                    // in ReloadThreadsAction()
                    var group = ctlGroupGrid.FocusedItem;
                    if (group != null)
                    {
                        if (thread.Group == group)
                        {
                            bool showReadThreads = chkShowReadThreads.Active || !group.TrackRead;
                            if (showReadThreads || !thread.Read)
                            {
                                ctlThreadGrid.AddThreadAtEnd(thread);
                            }

                        }
                    }
                    break;
            }
        }

        public void ReloadGroups()
        {
            reloadGroupsLagger.RequestAction();
        }

        void ReloadGroupsAction()
        {
            var groups = yamsterCache.GetAllGroups();

            if (!chkNonSyncedGroups.Active)
                groups = groups.Where(x => x.ShouldSync);

            ctlGroupGrid.LoadGroups(groups);
        }

        protected void chkNonSyncedGroups_Toggled(object sender, EventArgs e)
        {
            ReloadGroups();
        }

        protected void chkShowReadThreads_Toggled(object sender, EventArgs e)
        {
            ReloadThreads();
        }

        void yamsterCache_GroupChanged(object sender, YamsterGroupChangedEventArgs e)
        {
            switch (e.ChangeType)
            {
                case YamsterModelChangeType.PropertyChanged:
                    // We need to reload the groups in case the changed property is ShouldSync
                    ReloadGroups();
                    break;
                case YamsterModelChangeType.Added:
                    ReloadGroups();
                    break;
            }
        }

        protected void lblMarkAllRead_ButtonPress(object o, Gtk.ButtonPressEventArgs args)
        {
            var group = ctlGroupGrid.FocusedItem;

            if (group == null)
                return;

            if (Utilities.ShowMessageBox(
                string.Format("Are you sure you want to mark all messages as read in the group \"{0}\"?", 
                    group.GroupName), 
                "Yamster", 
                Gtk.ButtonsType.YesNo, Gtk.MessageType.Question) != Gtk.ResponseType.Yes)
                return;

            group.SetReadStatusForAllMessages(true);
        }
    }
}

