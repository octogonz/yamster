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
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Gtk;

namespace Yamster
{
    public abstract class GridColumn
    {
        readonly Grid widget;
        string title;

        public GridColumn(Grid widget, string title)
        {
            this.widget = widget;
            this.title = title;
        }

        public Grid Widget { get { return this.widget; } }

        public abstract TreeViewColumn GtkColumn { get; }

        public string Title
        {
            get { return this.title; }
        }

        public int MinWidth
        {
            get { return GtkColumn.MinWidth; }
            set { GtkColumn.MinWidth = value; }
        }

        public bool Visible
        {
            get { return GtkColumn.Visible; }
            set { GtkColumn.Visible = value; }
        }

        abstract public int CompareItemsForSorting(object itemA, object itemB);
    }

    public class GridValueColumn<T> : GridColumn where T : IComparable<T>
    {
        protected readonly Func<object, T> getValue;

        TreeViewColumn gtkColumn;
        CellRendererText cellRenderer;

        internal GridValueColumn(Grid widget, string title, Func<object, T> getValue)
            : base(widget, title)
        {
            this.getValue = getValue;

            cellRenderer = new CellRendererText();
            gtkColumn = new TreeViewColumn(title, cellRenderer);

            gtkColumn.Resizable = true;
            gtkColumn.Sizing = TreeViewColumnSizing.Fixed;

            //gtkColumn.PackStart(cellRenderer, true);
            gtkColumn.SetCellDataFunc(cellRenderer,
                delegate(TreeViewColumn treeColumn, CellRenderer renderer, TreeModel treeModel, TreeIter iter) {
                    object item = this.Widget.GetItemFromIter(iter);
                    var cellRendererText = (CellRendererText)renderer;

                    this.Widget.NotifyFormatCell(new GridFormatCellEventArgs(this, cellRendererText, item));

                    T value = getValue(item);
                    cellRendererText.Text = (value == null) ? "" : (value.ToString() ?? "");
                }
            );
        }

        public override TreeViewColumn GtkColumn
        {
            get { return gtkColumn; }
        }

        public CellRendererText CellRenderer
        {
            get { return cellRenderer; }
        }

        public override int CompareItemsForSorting(object itemA, object itemB) // abstract
        {
            T valueA = this.getValue(itemA);
            T valueB = this.getValue(itemB);
            return valueA.CompareTo(valueB);
        }
    }

    public class GridTextColumn : GridValueColumn<string>
    {
        internal GridTextColumn(Grid widget, string title, Func<object, string> getValue)
            : base(widget, title, getValue)
        {
        }

        public override int CompareItemsForSorting(object itemA, object itemB) // abstract
        {
            string stringA = this.getValue(itemA);
            string stringB = this.getValue(itemB);
            return StringComparer.InvariantCulture.Compare(stringA, stringB);
        }
    }

    public class GridCheckboxColumn : GridColumn
    {
        Func<object, bool> getValue;
        Action<object, bool> setValue;

        TreeViewColumn gtkColumn;
        CellRendererToggle cellRenderer;

        internal GridCheckboxColumn(Grid widget, string title,
            Func<object, bool> getValue, Action<object, bool> setValue)
            : base(widget, title)
        {
            this.getValue = getValue;
            this.setValue = setValue;

            cellRenderer = new CellRendererToggle();
            cellRenderer.Xalign = 0;

            gtkColumn = new TreeViewColumn(title, cellRenderer);
            gtkColumn.Resizable = true;
            gtkColumn.Sizing = TreeViewColumnSizing.Fixed;

            gtkColumn.SetCellDataFunc(cellRenderer,
                delegate(TreeViewColumn treeColumn, CellRenderer renderer, TreeModel treeModel, TreeIter iter) {
                    object item = this.Widget.GetItemFromIter(iter);
                    var cellRendererToggle = (CellRendererToggle)renderer;
                    cellRendererToggle.Active = getValue(item);
                }
            );

            cellRenderer.Toggled += delegate (object sender, ToggledArgs args)
            {
                var cellRendererToggle = (CellRendererToggle)sender;
                object item = this.Widget.GetItemFromPathString(args.Path);
                bool oldValue = getValue(item);
                setValue(item, !oldValue);
            };
        }

        public override TreeViewColumn GtkColumn
        {
            get { return gtkColumn; }
        }

        public override int CompareItemsForSorting(object itemA, object itemB) // abstract
        {
            bool valueA = getValue(itemA);
            bool valueB = getValue(itemB);
            return valueA.CompareTo(valueB);
        }

    }

}
