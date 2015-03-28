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
using System.Linq;
using System.Text;
using Gtk;
using Yamster.Core;

namespace Yamster
{
    public class GridFormatCellEventArgs : EventArgs
    {
        public GridColumn Column { get; private set; }
        public CellRendererText Renderer { get; private set; }
        public object Item { get; private set; }

        public GridFormatCellEventArgs(GridColumn column, CellRendererText renderer, object item)
        {
            this.Column = column;
            this.Renderer = renderer;
            this.Item = item;
        }
    }

    public class ReadOnlyCollectionByEnum<T, TEnum> : ReadOnlyCollection<T>
        where TEnum : IConvertible
    {
        public ReadOnlyCollectionByEnum(IList<T> list)
            : base(list)
        {
        }

        public T this[TEnum symbol] 
        {
            get {
                int index = Convert.ToInt32(symbol);
                return this[index];
            }
        }
    }
}
