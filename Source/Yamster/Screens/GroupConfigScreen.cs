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
using Yamster.Core;

namespace Yamster
{
    // This is the "Manage Groups" tab on the MainWindow.
    [System.ComponentModel.ToolboxItem(true)]
    public partial class GroupConfigScreen : Gtk.Bin
    {
        AppContext appContext;
        YamsterCache yamsterCache;
        ActionLagger reloadGroupsLagger;

        // TODO: Move this to a second AppContext for the desktop app
        public event EventHandler PerformLogin;

        bool suspendEvents = true;

        public GroupConfigScreen()
        {
            this.Build();

            this.appContext = AppContext.Default;
            yamsterCache = appContext.YamsterCache;

            reloadGroupsLagger = new ActionLagger(ReloadGroupsAction);
            reloadGroupsLagger.RequestAction();

            appContext.YamsterCache.GroupChanged += YamsterCache_GroupChanged;

            suspendEvents = false;
            UpdateUI();
        }

        public override void Destroy()
        {
            reloadGroupsLagger.Dispose();
            appContext.YamsterCache.GroupChanged -= YamsterCache_GroupChanged;
            base.Destroy();
        }

        void UpdateUI()
        {
            try
            {
                suspendEvents = true;
                chkSyncInbox.Active = appContext.YamsterCoreDb.Properties.SyncInbox;
            }
            finally
            {
                suspendEvents = false;
            }

            btnViewGroup.Sensitive = ctlGroupGrid.FocusedItem != null;
        }

        void YamsterCache_GroupChanged(object sender, YamsterGroupChangedEventArgs e)
        {
            switch (e.ChangeType)
            {
                case YamsterModelChangeType.Added:
                case YamsterModelChangeType.PropertyChanged: // might be the ShowInYamster property
                    reloadGroupsLagger.RequestAction();
                    break;
            }
        }

        void ReloadGroupsAction()
        {
            var groups = yamsterCache.GetAllGroups()
                .OrderBy(x => x.GroupName);
            ctlGroupGrid.LoadGroups(groups);
        }

        protected void btnViewGroup_Clicked(object sender, EventArgs e)
        {
            var group = ctlGroupGrid.FocusedItem;
            if (group != null)
            {
                string webUrl = group.WebUrl;
                if (!string.IsNullOrWhiteSpace(webUrl))
                {
                    Process.Start(webUrl);
                    return;
                }
            }

            Utilities.ShowMessageBox("The group URL is missing.  Try again after resyncing.", 
                "Unable to Show Group", Gtk.ButtonsType.Ok, Gtk.MessageType.Warning);
        }

        protected void btnAddGroup_Clicked(object sender, EventArgs e)
        {
            if (PerformLogin != null)
                PerformLogin(this, EventArgs.Empty);

            using (var window = new AddGroupWindow())
            {
                Utilities.RunWindowAsDialog(window);

                if (window.ChosenGroup != null)
                {
                    appContext.YamsterCache.AddGroupToYamster(window.ChosenGroup.Id, 
                        window.ChosenGroup);
                }
            }
        }

        protected void ctlGroupGrid_FocusedItemChanged(object sender, EventArgs e)
        {
            UpdateUI();
        }        

        protected void chkSyncInbox_Toggled(object sender, EventArgs e)
        {
            if (suspendEvents)
                return;

            appContext.YamsterCoreDb.SetSyncInbox(chkSyncInbox.Active);
        }
    }
}

