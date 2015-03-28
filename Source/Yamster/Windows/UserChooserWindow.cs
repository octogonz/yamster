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
using System.Diagnostics;
using Yamster.Core;

namespace Yamster
{
    public partial class UserChooserWindow : Gtk.Window
    {
        AppContext appContext;
        YamsterCache yamsterCache;
        ActionLagger reloadLagger;
        YamsterUser chosenUser = null;
        bool searchTextChanged = false;

        public UserChooserWindow()
            : base(Gtk.WindowType.Toplevel)
        {
            this.Build();

            // On Mac, this prevents the parent window from losing focus
            Debug.Assert(this.TypeHint == Gdk.WindowTypeHint.Normal);    

            appContext = AppContext.Default;
            yamsterCache = appContext.YamsterCache;

            ctlGrid.ItemType = typeof(YamsterUser);

            SetupColumns();

            ctlGrid.FilterDelegate = ctlGrid_Filter;

            reloadLagger = new ActionLagger(ReloadAction, 100);
            reloadLagger.RequestAction(ActionLaggerQueueing.ForceDelayed);
        }

        protected override void OnDestroyed()
        {
            if (reloadLagger != null) 
            {
                reloadLagger.Dispose();
                reloadLagger = null;
            }
            base.OnDestroyed();
        }

        public YamsterUser ChosenUser
        {
            get { return this.chosenUser; }
        }

        void SetupColumns()
        {
            // NOTE: On Mac, the Pango crashes sometimes apparently while attempting
            // to render certain unusual characters in people's Yammer names
            ctlGrid.AddTextColumn("Name", 200, (YamsterUser user) => user.FullName);
            ctlGrid.AddTextColumn("Alias", 75, (YamsterUser user) => user.Alias);
            ctlGrid.AddTextColumn("Job Title", 100, (YamsterUser user) => user.JobTitle);
        }

        void ReloadAction()
        {
            ctlGrid.ReplaceAllItems(yamsterCache.GetAllUsers().OrderBy(x => x.FullName));
        }

        protected void btnCancel_Clicked(object sender, EventArgs e)
        {
            this.Destroy();
        }

        protected void txtSearch_Changed(object sender, EventArgs e)
        {
            searchTextChanged = true;
            ctlGrid.UpdateView();
        }

        void ctlGrid_ViewUpdated(object sender, EventArgs e)
        {
            if (searchTextChanged)
            {
                if (ctlGrid.ViewItems.Count == 1)
                {
                    ctlGrid.SelectSingleItem(ctlGrid.ViewItems[0]);
                }
                else
                {
                    ctlGrid.Selection.UnselectAll();
                }
            }
        }

        protected override bool OnKeyPressEvent(Gdk.EventKey evnt)
        {
            if (evnt.Key == Gdk.Key.Escape)
                this.Destroy();

            if (evnt.Key == Gdk.Key.Return)
            {
                // If something is focused in the grid, return that
                var user = ctlGrid.FocusedItem as YamsterUser;
                if (user != null)
                {
                    this.chosenUser = user;
                    this.Destroy();
                    return true;
                }
            }

            return base.OnKeyPressEvent(evnt);
        }

        bool ctlGrid_Filter(Grid sender, object item)
        {
            string searchText = txtSearch.Text.Trim();
            if (!string.IsNullOrEmpty(searchText))
            {
                var user = (YamsterUser)item;
                if (user == null)
                    return false;
                if (user.FullName.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) < 0
                    && user.Alias.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) < 0)
                    return false; // not found
            }
            return true;
        }

        protected void ctlGrid_Widget(object o, Gtk.WidgetEventArgs args)
        {
            if (args.Event.Type == Gdk.EventType.ButtonRelease)
            {
                // In this case, the user used the mouse to click on a row in the grid
                var user = ctlGrid.FocusedItem as YamsterUser;
                if (user != null)
                {
                    this.chosenUser = user;
                    this.Destroy();
                }
            }
        }

    }
}

  