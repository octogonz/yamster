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
using System.ComponentModel;
using System.Linq;
using Gdk;
using Yamster.Core;

namespace Yamster
{
    // This is control is based on Grid, but specicalized for showing 
    // a list of YamsterThread objects.
    [System.ComponentModel.ToolboxItem(true)]
    public partial class ThreadGrid : Gtk.Bin
    {
        AppContext appContext;
        YamsterCache yamsterCache;
        GridTextColumn col_NewThread;
        bool trackRead = true;

        public ThreadGrid()
        {
            this.Build();

            appContext = AppContext.Default;
            yamsterCache = appContext.YamsterCache;

            ctlGrid.ItemType = typeof(YamsterThread);

            SetupColumns();

            ctlGrid.Selection.Mode = Gtk.SelectionMode.Multiple;
            ctlGrid.FormatCell += ctlGrid_FormatCell;

            yamsterCache.ThreadChanged += yamsterCache_ThreadChanged;

            UpdateUI();
        }

        public override void Destroy()
        {
            yamsterCache.ThreadChanged -= yamsterCache_ThreadChanged;
            base.Destroy();
        }

        public YamsterThread FocusedItem
        {
            get
            {
                return (YamsterThread) ctlGrid.FocusedItem;
            }
        }

        /// <summary>
        /// If TrackRead is true, then threads with unread merssages will be marked as bold, and
        /// the "New?" column will be shown to indicate new threads.
        /// </summary>
        [DefaultValue(true)]
        public bool TrackRead
        {
            get { return this.trackRead; }
            set 
            {
                if (this.trackRead == value)
                    return;
                this.trackRead = value;
                UpdateUI();
            }
        }

        public event EventHandler FocusedItemChanged
        {
            add { this.ctlGrid.FocusedItemChanged += value; }
            remove { this.ctlGrid.FocusedItemChanged -= value; }
        }

        void SetupColumns()
        {
            col_NewThread = ctlGrid.AddTextColumn("New?", 45,
                (YamsterThread thread) => thread.MessagesRead == YamsterMessagesRead.None ? "●" : ""
            );
            col_NewThread.GtkColumn.Alignment = 0.5f;
            col_NewThread.CellRenderer.Xalign = 0.5f;

            ctlGrid.AddTextColumn("Started By", 100, (YamsterThread thread) => thread.Messages.First().SenderName);
            ctlGrid.AddTextColumn("Preview", 100, (YamsterThread thread) => thread.Messages.First().GetPreviewText());
            ctlGrid.AddDateColumn("Last Update", 150, (YamsterThread thread) => thread.LastUpdate);
            ctlGrid.AddTextColumn("Group", 100, (YamsterThread thread) => thread.Group.GroupName);
            ctlGrid.AddInt32Column("# Yams", 100, (YamsterThread thread) => thread.Messages.Count);
            ctlGrid.AddInt32Column("Total Likes", 100, (YamsterThread thread) => thread.TotalLikesCount);
        }

        void UpdateUI()
        {
            col_NewThread.Visible = this.TrackRead;
        }

        public void ClearThreads()
        {
            ctlGrid.ClearItems();
        }

        public void LoadThreads(IList<YamsterThread> threads)
        {
            ctlGrid.ReplaceAllItems(threads);
        }

        public void AddThreadAtEnd(YamsterThread thread)
        {
            ctlGrid.AddItem(thread);
            ctlGrid.QueueDraw();
        }

        protected override bool OnKeyReleaseEvent(EventKey evnt)
        {
            // If SPACE is pressed, then mark the selected item as read
            if (evnt.Key == Key.space)
            {
                if (this.FocusedItem != null)
                {
                    this.FocusedItem.SetReadStatusForAllMessages(true);
                }
                return true;
            }
            return base.OnKeyReleaseEvent(evnt);
        }

        void yamsterCache_ThreadChanged(object sender, YamsterThreadChangedEventArgs e)
        {
            switch (e.ChangeType)
            {
                case YamsterModelChangeType.PropertyChanged:
                    ctlGrid.QueueDraw();
                    break;
            }
        }

        void ctlGrid_FormatCell(object sender, GridFormatCellEventArgs e)
        {
            var thread = (YamsterThread) e.Item;
            bool bold = this.TrackRead && !thread.Read;

            if (e.Column != col_NewThread)
            {
                // Bold if unread
                e.Renderer.Weight = bold ? 800 : 400;
            }
        }
    }
}

