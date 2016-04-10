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
using System.Diagnostics;
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
        class ViewedThread
        {
            public readonly YamsterThread Thread;
            public bool Read;

            public ViewedThread(YamsterThread thread)
            {
                this.Thread = thread;
                this.Read = thread.Read;
            }

            public long ThreadId
            {
                get { return this.Thread.ThreadId; }
            }
        }

        readonly Dictionary<long, ViewedThread> viewedThreadsById = new Dictionary<long, ViewedThread>();
        int readThreadCount = 0;

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

        public override int TotalItems
        {
            get { return this.viewedThreadsById.Count; }
        }

        public override int UnreadItems
        {
            get { return this.viewedThreadsById.Count - this.readThreadCount; }
        }

        public ReadOnlyCollection<YamsterThread> GetThreadsInView()
        {
            Validate();

            var list = new List<YamsterThread>(this.viewedThreadsById.Count);
            foreach (var viewedThread in this.viewedThreadsById.Values)
            {
                list.Add(viewedThread.Thread);
            }

            return new ReadOnlyCollection<YamsterThread>(list);
        }

        protected override void OnInvalidate() // abstract
        {
            viewedThreadsById.Clear();
            this.readThreadCount = 0;
        }

        protected override void OnValidate() // abstract
        {
            var executionContext = new YqlExecutionContext(appContext);

            Debug.Assert(viewedThreadsById.Count == 0);

            foreach (var thread in appContext.YamsterCache.GetAllThreads())
            {
                executionContext.Thread = thread;
                if (CompiledFunc(executionContext))
                {
                    this.viewedThreadsById.Add(thread.ThreadId, new ViewedThread(thread));
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
                var executionContext = new YqlExecutionContext(this.appContext);
                executionContext.Thread = thread;
                shouldBeInView = CompiledFunc(executionContext);
            }

            ViewedThread viewedThread = null;
            bool isInView = viewedThreadsById.TryGetValue(thread.ThreadId, out viewedThread);

            if (isInView)
            {
                if (!shouldBeInView)
                {
                    this.RemoveViewedThread(viewedThread);
                    NotifyViewChanged(YamsterViewChangeType.ModelLeaveView, thread);
                }
            }
            else
            {
                if (shouldBeInView)
                {
                    this.viewedThreadsById.Add(thread.ThreadId, new ViewedThread(thread));
                    NotifyViewChanged(YamsterViewChangeType.ModelEnterView, thread);
                }
            }
        }

        void AddViewedThread(ViewedThread viewedThread)
        {
            this.viewedThreadsById.Add(viewedThread.ThreadId, viewedThread);
            if (viewedThread.Read)
                ++this.readThreadCount;
        }

        void RemoveViewedThread(ViewedThread viewedThread)
        {
            if (viewedThread.Read)
                --this.readThreadCount;

            if (!this.viewedThreadsById.Remove(viewedThread.ThreadId))
            {
                throw new KeyNotFoundException();
            }
        }
    }

}
