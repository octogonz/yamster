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
using Yamster.Core;

namespace Yamster
{
    // Used by GroupConfigScreen.
    public partial class AddGroupWindow : Gtk.Window
    {
        AppContext appContext;

        public JsonSearchedGroup ChosenGroup { get; private set; }

        ActionLagger searchLagger;

        public AddGroupWindow() : 
            base(Gtk.WindowType.Toplevel)
        {
            this.appContext = AppContext.Default;
            this.Build();

            ctlGrid.ItemType = typeof(JsonSearchedGroup);
            SetupColumns();

            searchLagger = new ActionLagger(SearchAction, 100);
            searchLagger.RequestAction(ActionLaggerQueueing.ForceDelayed);

            UpdateUI();
        }

        protected override void OnDestroyed()
        {
            if (searchLagger != null) 
            {
                searchLagger.Dispose();
                searchLagger = null;
            }
            base.OnDestroyed();
        }

        void SetupColumns()
        {
            ctlGrid.AddTextColumn<JsonSearchedGroup>("Name", 180,
                x => { return x.FullName; }
            );
        }

        void UpdateUI()
        {
            btnAddGroup.Sensitive = ctlGrid.FocusedItem != null;
        }

        protected void btnSearch_Clicked(object sender, EventArgs e)
        {
            searchLagger.RequestAction();
        }

        protected void btnAddGroup_Clicked(object sender, EventArgs e)
        {
            ChosenGroup = (JsonSearchedGroup)ctlGrid.FocusedItem;
            this.Destroy();
        }

        protected void btnCancel_Clicked(object sender, EventArgs e)
        {
            this.Destroy();
        }

        protected void txtSearch_KeyRelease(object o, Gtk.KeyReleaseEventArgs args)
        {
            if (args.Event.Key == Gdk.Key.Return)
                searchLagger.RequestAction();
        }

        protected void ctlGrid_FocusedItemChanged(object sender, EventArgs e)
        {
            UpdateUI();
        }

        void SearchAction()
        {
            IList<JsonSearchedGroup> JsonSearchedGroups = appContext.YamsterApi.SearchForGroups(txtSearch.Text, 30);
            ctlGrid.ReplaceAllItems(JsonSearchedGroups);
        }
    }
}

