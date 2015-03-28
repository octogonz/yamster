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
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Gtk;
using System.Diagnostics;
using Gdk;

namespace Yamster
{
    public delegate bool GridFilter(Grid sender, object item);

    // Grid is a general-purpose wrapper for GTK#'s TreeView widget.
    [System.ComponentModel.ToolboxItem(true)]
    public partial class Grid : Gtk.Bin
    {
        class ColumnSort
        {
            public GridColumn Column;
            public SortType Direction;
        }

        public event EventHandler<GridFormatCellEventArgs> FormatCell;
        public event EventHandler FocusedItemChanged;
        public event EventHandler ViewUpdated;

        Type itemType = null;

        // Since all the properties are fetched dynamically, the ListStore
        // simply stores indexes into storeItems
        ListStore store = new ListStore(typeof(int));
        int storeSize = 0;

        List<object> items = new List<object>();

        // This is the items list, after sorting/filtering has been applied
        List<object> viewItems = new List<object>();

        GridFilter filterDelegate = null;

        List<GridColumn> columns = new List<GridColumn>();
        List<ColumnSort> columnSorts = new List<ColumnSort>();

        // Tracks whether the user is currently pressing SHIFT, CTRL, etc.
        // This is used by the column sorting UI
        ModifierType currentModifierKeys = ModifierType.None;

        ActionLagger updateViewLagger;

        ActionLagger focusedItemChangedLagger;
        bool suppressFocusedItemChangedEvent = false;

        public Grid()
        {
            this.Build();

            this.ctlTreeView.Model = this.store;

            this.ctlTreeView.Selection.Changed += Selection_Changed;

            updateViewLagger = new ActionLagger(UpdateViewAction, 100);
            focusedItemChangedLagger = new ActionLagger(FocusedItemChangedAction, 500);
        }

        public override void Destroy()
        {
            focusedItemChangedLagger.Dispose();
            base.Destroy();
        }

        #region Properties

        [Browsable(false)]
        public Type ItemType 
        {
            get { return this.itemType; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("ItemType");
                if (this.itemType != null)
                    throw new InvalidOperationException("The item type has already been set");
                this.itemType = value;
            }
        }

        [Browsable(false)]
        public ReadOnlyCollection<GridColumn> Columns
        {
            get { return new ReadOnlyCollection<GridColumn>(this.columns); }
        }

        [Browsable(false)]
        public ReadOnlyCollection<object> Items
        {
            get { return new ReadOnlyCollection<object>(this.items); }
        }

        [Browsable(false)]
        public ReadOnlyCollection<object> ViewItems
        {
            get { return new ReadOnlyCollection<object>(this.viewItems); }
        }

        [Browsable(false)]
        public TreeView TreeView
        {
            get { return this.ctlTreeView; }
        }

        [Browsable(false)]
        public TreeSelection Selection
        {
            get { return this.ctlTreeView.Selection; }
        }

        [Browsable(false)]
        public object FocusedItem
        {
            get
            {
                var rows = ctlTreeView.Selection.GetSelectedRows();
                if (rows.Length == 0)
                    return null;
                return this.GetItemFromTreePath(rows[0]);
            }
        }

        [Browsable(false)]
        public GridFilter FilterDelegate
        {
            get { return this.filterDelegate; }
            set 
            {
                this.filterDelegate = value;
                this.UpdateView();
            }
        }

        #endregion

        void FocusedItemChangedAction()
        {
            if (FocusedItemChanged != null)
                FocusedItemChanged(this, EventArgs.Empty);
        }

        #region Adding Items

        void RequireItemType()
        {
            if (this.itemType == null)
                throw new InvalidOperationException("The ItemType must be assigned first");
        }
        void ValidateItemType(object item)
        {
            RequireItemType();
            if (item == null)
                throw new InvalidOperationException("The item cannot be null");
            if (!ItemType.IsInstanceOfType(item))
                throw new InvalidOperationException("The item is not of type " + itemType.Name);
        }

        public void ClearItems()
        {
            items.Clear();
            UpdateView();
        }

        public void AddItem(object item)
        {
            RequireItemType();
            ValidateItemType(item);
            items.Add(item);
            UpdateView();
        }

        public void AddItems(IEnumerable<object> source)
        {
            RequireItemType();

            foreach (var item in source)
            {
                ValidateItemType(item);
                items.Add(item);
            }
        }

        public void AddItems<T>(IEnumerable<T> source)
        {
            AddItems(source.Cast<object>());
        }

        // This is like calling ClearItems() then AddItems(), except that it retains
        // the view state
        public void ReplaceAllItems(IEnumerable<object> source)
        {
            RequireItemType();

            items.Clear();
            foreach (var item in source)
            {
                ValidateItemType(item);
                items.Add(item);
            }
            UpdateView();
        }

        public void ReplaceAllItems<T>(IEnumerable<T> source)
        {
            ReplaceAllItems(source.Cast<object>());
        }

        #endregion

        #region Updating The View

        void AdjustStoreSize()
        {
            int newStoreSize = viewItems.Count;

            if (newStoreSize == 0)
            {
                storeSize = newStoreSize;
                this.store.Clear();
                return;
            }

            if (newStoreSize > storeSize)
            {
                while (newStoreSize > storeSize)
                {
                    store.AppendValues(storeSize);
                    ++storeSize;
                }
                return;
            }

            // If we are removing more than 75% of the items, then rebuild the whole store
            if (newStoreSize < storeSize * 3 / 4)
            {
                storeSize = 0;
                this.store.Clear();
                AdjustStoreSize();
                return;
            }

            // ...otherwise incrementally remove the items
            while (storeSize > newStoreSize)
            {
                var treePath = GetTreePathFromItemIndex(storeSize-1);
                var iter = GetIterFromTreePath(treePath);
                store.Remove(ref iter);
                --storeSize;
            }
        }

        public void UpdateView()
        {
            this.updateViewLagger.RequestAction();
        }

        void UpdateViewAction()
        {
            bool fireEvent = focusedItemChangedLagger.ActionQueued;
            try
            {
                Debug.Assert(!suppressFocusedItemChangedEvent);
                suppressFocusedItemChangedEvent = true;

                object oldFocusedItem = this.FocusedItem;
                ctlTreeView.Selection.UnselectAll();

                int startTime = Environment.TickCount;
                ApplyFilterAndSort();
                int totalMs = unchecked(Environment.TickCount - startTime);
                if (totalMs > 50)
                {
                    Debug.WriteLine("Grid: ApplyFilterAndSort() took " + totalMs + " ms");
                }

                int index = viewItems.IndexOf(oldFocusedItem);
                if (index >= 0)
                {
                    var treePath = this.GetTreePathFromItemIndex(index);
                    ctlTreeView.Selection.SelectPath(treePath);
                }
                else
                {
                    fireEvent = true;
                }
                ctlTreeView.QueueDraw();
            }
            finally
            {
                suppressFocusedItemChangedEvent = false;
            }
            if (fireEvent)
            {
                focusedItemChangedLagger.RequestAction();
            }

            if (ViewUpdated != null)
                ViewUpdated(this, EventArgs.Empty);
        }

        void ApplyFilterAndSort()
        {
            // Special case for no sorting/filtering
            if (this.columnSorts.Count == 0 && this.filterDelegate == null)
            {
                this.viewItems.Clear();
                this.viewItems.AddRange(this.items);
                AdjustStoreSize();
                return;
            }

            List<Tuple<object, int>> itemsWithIndexes = new List<Tuple<object, int>>(items.Count);

            // First, apply the filter
            for (int i=0; i<items.Count; ++i)
            {
                var item = items[i];

                bool keepItem = true;
                if (filterDelegate != null)
                {
                    keepItem = filterDelegate(this, item);
                }
                if (keepItem)
                {
                    itemsWithIndexes.Add(Tuple.Create(item, i));
                }
            }

            // Next, apply the sort
            if (this.columnSorts.Count > 0)
            {
                itemsWithIndexes.Sort((x, y) => {
                    foreach (var columnSort in this.columnSorts)
                    {
                        int comparison = columnSort.Column.CompareItemsForSorting(x.Item1, y.Item1);
                        if (columnSort.Direction == SortType.Descending)
                            comparison = -comparison;
                        if (comparison != 0)
                            return comparison;
                    }

                    // to ensure stable sort, our final criteria is to sort by the original item indexes
                    return x.Item2 - y.Item2;
                });
            }

            this.viewItems.Clear();
            for (int i=0; i<itemsWithIndexes.Count; ++i)
            {
                this.viewItems.Add(itemsWithIndexes[i].Item1);
            }
            AdjustStoreSize();
        }

        #endregion

        #region Adding Columns

        void AddColumn(GridColumn column, int width)
        {
            column.MinWidth = width;
            column.GtkColumn.Clickable = true;
            column.GtkColumn.SortIndicator = false;
            column.GtkColumn.Clicked += GtkColumn_Clicked;
            columns.Add(column);
            this.TreeView.AppendColumn(column.GtkColumn);
        }

        public GridTextColumn AddTextColumn(string title, int width, Func<object, string> getValue)
        {
            var column = new GridTextColumn(this, title, getValue);
            AddColumn(column, width);
            return column;
        }

        public GridTextColumn AddTextColumn<T>(string title, int width, Func<T, string> getValue)
        {
            return AddTextColumn(title, width, (object item) => getValue((T)item));
        }

        public GridValueColumn<DateTime> AddDateColumn(string title, int width, Func<object, DateTime> getValue)
        {
            var column = new GridValueColumn<DateTime>(this, title, getValue);
            AddColumn(column, width);
            return column;
        }

        public GridValueColumn<DateTime> AddDateColumn<T>(string title, int width, Func<T, DateTime> getValue)
        {
            return AddDateColumn(title, width, (object item) => getValue((T)item));
        }

        public GridValueColumn<int> AddInt32Column(string title, int width, Func<object, int> getValue)
        {
            var column = new GridValueColumn<int>(this, title, getValue);
            AddColumn(column, width);
            return column;
        }

        public GridValueColumn<int> AddInt32Column<T>(string title, int width, Func<T, int> getValue)
        {
            return AddInt32Column(title, width, (object item) => getValue((T)item));
        }

        public GridCheckboxColumn AddCheckboxColumn<T>(string title, int width,
            Func<T, bool> getValue,
            Action<T, bool> setValue)
        {
            var column = new GridCheckboxColumn(this, title, 
                (obj) => getValue((T)obj),
                (obj, value) => setValue((T)obj, value)
            );
            AddColumn(column, width);
            return column;
        }

        internal void NotifyFormatCell(GridFormatCellEventArgs args)
        {
            if (FormatCell != null)
                FormatCell(this, args);
        }

        #endregion

        #region Tree Conversions

        object GetItemFromTreePath(TreePath treePath)
        {
            var iter = GetIterFromTreePath(treePath);
            return GetItemFromIter(iter);
        }

        internal object GetItemFromPathString(string treePath)
        {
            TreeIter iter;
            if (!this.store.GetIterFromString(out iter, treePath))
                throw new KeyNotFoundException("The Grid row index is out of range");
            return GetItemFromIter(iter);
        }

        internal object GetItemFromIter(TreeIter iter)
        {
            int index = (int)this.store.GetValue(iter, 0);
            return this.viewItems[index];
        }

        TreePath GetTreePathFromItemIndex(int itemIndex)
        {
            return new TreePath(new int[] { itemIndex });
        }

        TreeIter GetIterFromTreePath(TreePath treePath)
        {
            TreeIter treeIter;
            if (!this.store.GetIter(out treeIter, treePath))
                throw new IndexOutOfRangeException("The Grid row index is out of range");

            return treeIter;
        }

        #endregion

        #region Misc operations

        public void SelectSingleItem(object item)
        {
            int itemIndex = this.viewItems.FindIndex(x => System.Object.ReferenceEquals(x, item));

            if (itemIndex < 0)
                throw new KeyNotFoundException("The item was not found");

            TreePath treePath = GetTreePathFromItemIndex(itemIndex);
            this.Selection.UnselectAll();
            this.Selection.SelectPath(treePath);
        }

        #endregion

        #region Event Handlers

        void GtkColumn_Clicked(object sender, EventArgs e)
        {
            TreeViewColumn gtkColumn = (TreeViewColumn)sender;
            GridColumn clickedColumn = columns.FirstOrDefault(x => x.GtkColumn == gtkColumn);
            if (clickedColumn == null)
            {
                Debug.WriteLine("Grid: Error: Missing column reference");
                return;
            }

            bool multiSortKey = (currentModifierKeys & (ModifierType.ShiftMask | ModifierType.ControlMask)) != 0;

            int foundIndex = columnSorts.FindIndex(x => x.Column == clickedColumn);
            ColumnSort columnSort = null;
            if (foundIndex >= 0)
                columnSort = columnSorts[foundIndex];

            if (!multiSortKey)
            {
                // If we were doing some kind of multi-column sorting, then start over
                if (columnSorts.Count != 1 || foundIndex < 0 || columnSorts[foundIndex].Column != clickedColumn)
                {
                    columnSorts.Clear();
                    columnSort = null;
                    foundIndex = -1;
                }
            }
                
            if (columnSort != null && foundIndex == columnSorts.Count - 1)
            {
                // The user clicked the header for the column that was most recently included in the sort
                switch (columnSort.Direction)
                {
                    case SortType.Ascending:
                        columnSort.Direction = SortType.Descending;
                        break;
                    case SortType.Descending:
                        if (columnSorts.Count == 1)
                            columnSorts.RemoveAt(foundIndex);
                        else
                            columnSort.Direction = SortType.Ascending;
                        break;
                }
            } 
            else if (columnSort != null)
            {
                // The user clicked the header for a column that is already in the sort;
                // start over with a new compound key
                columnSorts.Clear();
                columnSorts.Add(columnSort);
                columnSort.Direction = SortType.Ascending;
            }
            else 
            {
                // The user is adding a new column to the sort
                columnSorts.Add(new ColumnSort() { Column = clickedColumn, Direction = SortType.Ascending });
            }

            // Update the visual display
            foreach (var column in columns)
            {
                columnSort = columnSorts.FirstOrDefault(x => x.Column == column);
                if (columnSort != null)
                {
                    column.GtkColumn.SortIndicator = true;
                    column.GtkColumn.SortOrder = columnSort.Direction;
                }
                else
                {
                    column.GtkColumn.SortIndicator = false;
                }
            }

            UpdateView();
        }

        void Selection_Changed(object sender, EventArgs e)
        {
            if (!suppressFocusedItemChangedEvent)
                focusedItemChangedLagger.RequestAction();
        }

        void ctlTreeView_KeyPress(object o, KeyPressEventArgs args)
        {
            this.currentModifierKeys = args.Event.State;
        }

        void ctlTreeView_KeyRelease(object o, KeyReleaseEventArgs args)
        {
            this.currentModifierKeys = ModifierType.None;
        }

        #endregion
    }

}
