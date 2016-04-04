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
using System.Collections.ObjectModel;
using System.ComponentModel;
using Yamster.Core.SQLite;
using Yamster.Core;

namespace Yamster
{
    // This is control is based on Grid, but specicalized for showing 
    // a list of YamsterGroup objects.
    // This is used by GroupThreadScreen, but not by GroupConfigScreen.
    [System.ComponentModel.ToolboxItem(true)]
    public partial class GroupGrid : Gtk.Bin
    {
        AppContext appContext;
        YamsterCache yamsterCache;

        public GroupGrid()
        {
            this.Build();
            
            appContext = AppContext.Default;
            yamsterCache = appContext.YamsterCache;

            ctlGrid.ItemType = typeof(YamsterGroup);

            SetupColumns();

            ctlGrid.FormatCell += ctlGrid_FormatCell;
            yamsterCache.GroupChanged += yamsterCache_GroupChanged;
        }

        public override void Destroy()
        {
            yamsterCache.GroupChanged -= yamsterCache_GroupChanged;
            base.Destroy();
        }

        [Browsable(false)]
        public ReadOnlyCollectionByEnum<GridColumn, GroupGridColumn> Columns
        {
            get { return new ReadOnlyCollectionByEnum<GridColumn,GroupGridColumn>(ctlGrid.Columns); }
        }

        [Browsable(false)]
        public YamsterGroup FocusedItem
        {
            get
            {
                return (YamsterGroup)ctlGrid.FocusedItem;
            }
        }

        public event EventHandler FocusedItemChanged
        {
            add { this.ctlGrid.FocusedItemChanged += value; }
            remove { this.ctlGrid.FocusedItemChanged -= value; }
        }

        void SetupColumns()
        {
            ctlGrid.AddTextColumn("Name", 180,
                (YamsterGroup group) => {
                    string title = group.GroupName;
                    if (!group.ShouldSync)
                        title += "*";

                    if (appContext.Settings.ShowUnreadThreadCount) {
                        if (group.TrackRead && group.UnreadThreadCount > 0)
                        {
                            title += " [" + group.UnreadThreadCount + "]";
                        }
                    }
                    return title;
                }
            );

            ctlGrid.AddCheckboxColumn("Sync This Group", 100,
                (YamsterGroup group) => group.ShouldSync,
                (YamsterGroup group, bool value) => {
                    group.ShouldSync = value;
                }
            );

            ctlGrid.AddCheckboxColumn("Track Read/Unread", 20,
                (YamsterGroup group) => group.TrackRead,
                (YamsterGroup group, bool value) => {
                    group.TrackRead = value;
                }
            );
        }

        public void LoadGroups(IEnumerable<YamsterGroup> groups)
        {
            ctlGrid.ReplaceAllItems(groups);
        }

        void yamsterCache_GroupChanged(object sender, YamsterGroupChangedEventArgs e)
        {
            switch (e.ChangeType)
            {
                case YamsterModelChangeType.Added:
                    break;
                case YamsterModelChangeType.PropertyChanged:
                    ctlGrid.QueueDraw();
                    break;
            }
        }

        void ctlGrid_FormatCell(object sender, GridFormatCellEventArgs e)
        {
            var group = (YamsterGroup)e.Item;

            bool bold = group.TrackRead && !group.Read;
            e.Renderer.Weight = bold ? 800 : 400;
        }
    }

    public enum GroupGridColumn
    {
        Name,
        ShouldSync,
        TrackRead
    }
}

