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
using System.Threading;
using System.Threading.Tasks;
using GLib;

namespace Yamster.Core
{

    /// <summary>
    /// A SynchronizationContext that executes all tasks on an application's
    /// foreground thread.
    /// </summary>
    public abstract class ForegroundSynchronizationContext : SynchronizationContext
    {
        protected class QueueItem
        {
            public SendOrPostCallback Callback;
            public object State;
        }
        protected readonly AppContext appContext;
        protected readonly object lockObject = new object();
        protected readonly Queue<QueueItem> queue = new Queue<QueueItem>();

        public ForegroundSynchronizationContext(AppContext appContext)
        {
            this.appContext = appContext;
        }

        protected abstract bool ProcessQueue();

        /// <summary>
        /// The foreground thread can call this periodically to process any async tasks
        /// in the queue.  Returns false if the queue is empty (i.e. no further work to do),
        /// or if a ForegroundSynchronizationContext is not in use.
        /// </summary>
        public static bool ProcessAsyncTasks()
        {
            var currentContext = SynchronizationContext.Current as ForegroundSynchronizationContext;
            if (currentContext == null)
                return false;
            return currentContext.ProcessQueue();
        }

        public static void RunSynchronously(Task task)
        {
            while (!task.IsCompleted)
            {
                if (!ForegroundSynchronizationContext.ProcessAsyncTasks())
                    System.Threading.Thread.Sleep(50);
            }
            task.GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Provides a synchronization context for the GTK# application model.
    /// This is similar to WindowsFormsSynchronizationContext except that it 
    /// dispatches tasks from the GTK# message loop.
    /// </summary>
    public class GtkSynchronizationContext : ForegroundSynchronizationContext
    {
        bool addedHandler = false;

        public GtkSynchronizationContext(AppContext appContext)
            :   base(appContext)
        {
        }

        public override void Post(SendOrPostCallback callback, object state)
        {
            lock (lockObject)
            {
                if (!addedHandler)
                {
                    Idle.Add(ProcessQueue);
                    addedHandler = true;
                }
                queue.Enqueue(new QueueItem() { Callback = callback, State = state });
            }
        }

        protected override bool ProcessQueue() // abstract
        {
            this.appContext.RequireForegroundThread();

            QueueItem queueItem;
            lock (lockObject)
            {
                if (queue.Count == 0)
                {
                    addedHandler = false;
                    return false;
                }
                queueItem = queue.Dequeue();
            }
            queueItem.Callback(queueItem.State);

            return true;
        }

        public static void Install(AppContext appContext)
        {
            if (SynchronizationContext.Current != null)
                throw new InvalidOperationException("Another SynchronizationContext is already registered");
            var synchronizationContext = new GtkSynchronizationContext(appContext);
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
        }
    }

    /// <summary>
    /// Provides a synchronization context for use with services or console applications
    /// which do not have a message loop.  Whereas WindowsFormsSynchronizationContext
    /// (for example) will automatically perform queued async tasks as part of the normal
    /// message loopk, with PollingSynchronizationContext the program must manually call
    /// ProcessAsyncTasks() at appropriate points (e.g. inside a loop that is waiting
    /// for async operations).
    /// </summary>
    public class PollingSynchronizationContext : ForegroundSynchronizationContext
    {
        public PollingSynchronizationContext(AppContext appContext)
            :   base(appContext)
        {
        }

        public override void Post(SendOrPostCallback callback, object state)
        {
            lock (lockObject)
            {
                queue.Enqueue(new QueueItem() { Callback = callback, State = state });
            }
        }

        protected override bool ProcessQueue() // abstract
        {
            this.appContext.RequireForegroundThread();

            QueueItem queueItem;
            lock (lockObject)
            {
                if (queue.Count == 0)
                    return false;
                queueItem = queue.Dequeue();
            }
            queueItem.Callback(queueItem.State);
            lock (lockObject)
            {
                return queue.Count != 0;
            }
        }

        public static void Install(AppContext appContext)
        {
            if (SynchronizationContext.Current != null)
                throw new InvalidOperationException("Another SynchronizationContext is already registered");
            var synchronizationContext = new PollingSynchronizationContext(appContext);
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
        }
    }

}
