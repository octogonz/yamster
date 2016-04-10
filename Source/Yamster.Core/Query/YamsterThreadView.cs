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
using System.Linq.Expressions;

namespace Yamster.Core
{
    public class YamsterThreadQuery : YamsterModelQuery
    {
        public YamsterThreadQuery(string title, YqlNode filterNode)
            : base(title, filterNode)
        {
        }
        public YamsterThreadQuery()
        {
        }

        public override YamsterModelType ModelType
        {
            get { return YamsterModelType.Thread; }
        }
    }

    public class YamsterThreadView : YamsterModelView
    {
        readonly Dictionary<long, YamsterThread> threadsById = new Dictionary<long, YamsterThread>();

        public YamsterThreadView(AppContext appContext)
            : base(appContext)
        {
            yamsterCache.ThreadChanged += YamsterCache_ThreadChanged;
        }

        public override void Dispose()
        {
            if (!IsDisposed)
            {
                yamsterCache.ThreadChanged -= YamsterCache_ThreadChanged;
            }

            base.Dispose();
        }

        public ReadOnlyCollection<YamsterThread> GetThreadsInView()
        {
            Validate();
            return new ReadOnlyCollection<YamsterThread>(this.threadsById.Values.ToList());
        }

        protected override void OnInvalidate() // abstract
        {
            threadsById.Clear();
        }

        protected override void OnValidate() // abstract
        {
            var exectionContext = new YqlExecutionContext(appContext);

            foreach (var thread in appContext.YamsterCache.GetAllThreads())
            {
                exectionContext.Thread = thread;
                if (CompiledFunc(exectionContext))
                {
                    this.threadsById.Add(thread.ThreadId, thread);
                }
            }
        }

        void YamsterCache_ThreadChanged(object sender, YamsterThreadChangedEventArgs e)
        {
            if (!this.IsValid)
                return;

            switch (e.ChangeType)
            {
                case YamsterModelChangeType.Added:
                case YamsterModelChangeType.PropertyChanged:
                    UpdateViewWithThread(e.Thread);
                    break;
            }
        }

        void UpdateViewWithThread(YamsterThread thread)
        {
            bool shouldBeInView = false;
            if (CompiledFunc != null)
            {
                var exectionContext = new YqlExecutionContext(this.appContext);
                exectionContext.Thread = thread;
                shouldBeInView = CompiledFunc(exectionContext);
            }

            bool isInView = threadsById.ContainsKey(thread.ThreadId);

            if (isInView)
            {
                if (!shouldBeInView)
                {
                    this.threadsById.Remove(thread.ThreadId);
                    NotifyViewChanged(YamsterViewChangeType.ModelLeaveView, thread);
                }
            }
            else
            {
                if (shouldBeInView)
                {
                    this.threadsById.Add(thread.ThreadId, thread);
                    NotifyViewChanged(YamsterViewChangeType.ModelEnterView, thread);
                }
            }
        }
    }

}
