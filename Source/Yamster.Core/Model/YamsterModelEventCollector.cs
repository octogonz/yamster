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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Yamster.Core.SQLite;

namespace Yamster.Core
{
    public enum YamsterModelChangeType
    {
        Added,
        PropertyChanged // does not include child objects being added/removed
    }

    public abstract class ModelChangedEventArgs : EventArgs
    {
        abstract public YamsterModel Model { get; }
        public YamsterModelChangeType ChangeType { get; private set; }

        public ModelChangedEventArgs(YamsterModelChangeType changeType)
        {
            this.ChangeType = changeType;
        }
    }

    internal class YamsterModelEventCollector
    {
        class ChangedModel
        {
            public YamsterModel Model;
            public YamsterModelChangeType ChangeType;

            public override string ToString()
            {
                return ChangeType + ": " + Model.ToString();
            }
        }

        // We could eliminate duplicate events that appear in this list,
        // but on average they currently appear to constitute only around 8% of
        // the total which probably isn't worth it.
        List<ChangedModel> changedModels = new List<ChangedModel>();

        public void NotifyAfterAdd(YamsterModel model)
        {
            changedModels.Add(new ChangedModel() { Model = model, ChangeType = YamsterModelChangeType.Added });
        }

        public void NotifyAfterUpdate(YamsterModel model)
        {
            changedModels.Add(new ChangedModel() { Model = model, ChangeType = YamsterModelChangeType.PropertyChanged });
        }

        public void FireEvents()
        {
            var list = changedModels.ToArray();
            changedModels.Clear();
            foreach (var changedModel in list)
            {
                changedModel.Model.FireChangeEvent(changedModel.ChangeType);
            }
        }

    }

}
