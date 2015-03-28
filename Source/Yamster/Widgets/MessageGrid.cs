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
using System.Text.RegularExpressions;
using Gtk;
using Yamster.Core;

namespace Yamster
{
    // This is control is based on Grid, but specicalized for showing 
    // a list of YamsterMessage objects.
    // NOTE: To display Yammer thread for reading, use ThreadViewer instead.
    [System.ComponentModel.ToolboxItem(true)]
    public partial class MessageGrid : Gtk.Bin
    {
        AppContext appContext;
        YamsterCache yamsterCache;
        HashSet<long> referencedThreadIds = new HashSet<long>();
        GridTextColumn col_NewThread;

        public MessageGrid()
        {
            this.Build();

            appContext = AppContext.Default;
            yamsterCache = appContext.YamsterCache;

            ctlGrid.ItemType = typeof(YamsterMessage);

            SetupColumns();

            ctlGrid.Selection.Mode = SelectionMode.Multiple;
            ctlGrid.FormatCell += ctlGrid_FormatCell;

            yamsterCache.MessageChanged += yamsterCache_MessageChanged;
        }

        public override void Destroy()
        {
            yamsterCache.MessageChanged -= yamsterCache_MessageChanged;
            base.Destroy();
        }

        public YamsterMessage FocusedItem
        {
            get
            {
                return (YamsterMessage)ctlGrid.FocusedItem;
            }
        }

        public event EventHandler FocusedItemChanged
        {
            add { this.ctlGrid.FocusedItemChanged += value; }
            remove { this.ctlGrid.FocusedItemChanged -= value; }
        }

        void SetupColumns()
        {
            ctlGrid.AddTextColumn("Sender", 100, (YamsterMessage message) => message.SenderName);

            col_NewThread = ctlGrid.AddTextColumn("New?", 45,
                (YamsterMessage message) => message.Thread.MessagesRead == YamsterMessagesRead.None ? "●" : ""
            );
            col_NewThread.GtkColumn.Alignment = 0.5f;
            col_NewThread.CellRenderer.Xalign = 0.5f;

            ctlGrid.AddTextColumn("Preview", 100, (YamsterMessage message) => message.GetPreviewText());
            ctlGrid.AddDateColumn("Date", 150, (YamsterMessage message) => message.CreatedDate);
            ctlGrid.AddInt32Column("# Likes", 50, (YamsterMessage message) => message.LikesCount);
            ctlGrid.AddTextColumn("Group", 100, (YamsterMessage message) => message.GroupName);
        }

        public void ClearMessages()
        {
            ctlGrid.ClearItems();
        }

        public void LoadMessages(IList<YamsterMessage> messages)
        {
            referencedThreadIds.Clear();

            ctlGrid.ReplaceAllItems(messages);

            foreach (var message in messages)
                referencedThreadIds.Add(message.ThreadId);
        }

        public void AddMessageAtEnd(YamsterMessage message)
        {
            ctlGrid.AddItem(message);
            referencedThreadIds.Add(message.ThreadId);
        }

        void yamsterCache_MessageChanged(object sender, YamsterMessageChangedEventArgs e)
        {
            switch (e.ChangeType)
            {
                case YamsterModelChangeType.PropertyChanged:
                    // Since some of the displayed properties depend on the thread, we look for changes
                    // to any message in the thread.  YamsterCache.ThreadChanged would not be sufficient
                    // since it doesn't fire for properties that only involve YamsterMessage.
                    if (referencedThreadIds.Contains(e.Message.ThreadId))
                        ctlGrid.QueueDraw();
                    break;
            }
        }


        void ctlGrid_FormatCell(object sender, GridFormatCellEventArgs e)
        {
            var message = (YamsterMessage)e.Item;
            bool bold = message.Group.TrackRead && !message.Thread.Read;

            if (e.Column != col_NewThread)
            {
                // Bold if unread
                e.Renderer.Weight = bold ? 800 : 400;
            }
        }

    }
}

