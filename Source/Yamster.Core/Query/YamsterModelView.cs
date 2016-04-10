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
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Yamster.Core
{
    public enum YamsterViewChangeType
    {
        ModelEnterView,
        ModelLeaveView,
        FlushView        
    }

    public class ViewChangedEventArgs : EventArgs
    {
        public YamsterViewChangeType ChangeType { get; private set; }
        public YamsterModel Model { get; private set; }

        public ViewChangedEventArgs(YamsterViewChangeType changeType, YamsterModel model)
        {
            this.ChangeType = changeType;
            this.Model = model;
        }
    }

    public abstract class YamsterModelView : IDisposable
    {
        public event EventHandler<ViewChangedEventArgs> ViewChanged;

        protected AppContext appContext;
        protected YamsterCache yamsterCache;

        bool valid = false;

        public YamsterModelQuery Query { get; private set; }
        internal Func<YqlExecutionContext, bool> CompiledFunc = null;

        public YamsterModelView(AppContext appContext)
        {
            this.appContext = appContext;
            this.yamsterCache = appContext.YamsterCache;
        }

        public virtual void Dispose()
        {
            if (!IsDisposed)
            {
                Invalidate();

                Query = null;
                yamsterCache = null;
            }
        }

        public bool IsDisposed
        {
            get { return this.yamsterCache == null; }
        }

        /// <summary>
        /// Returns true if Validate() has been called, i.e. the internal list of items
        /// has been built and is being actively updated.
        /// </summary>
        protected bool IsValid
        {
            get { return this.valid; }
        }

        void RequireNotDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(this.GetType().Name);
        }

        public void LoadQuery(YamsterModelQuery query)
        {
            RequireNotDisposed();

            bool succeeded = false;
            try
            {
                Query = query;
                CompiledFunc = null;

                if (query != null)
                {
                    CompiledFunc = YqlCompiler.Compile(query);
                }

                Invalidate();
                succeeded = true;
            }
            finally
            {
                if (!succeeded)
                {
                    Query = null;
                    CompiledFunc = null;

                    Invalidate();
                }
            }
        }

        protected void NotifyViewChanged(YamsterViewChangeType changeType, YamsterModel model)
        {
            if (ViewChanged != null)
            {
                ViewChanged(this, new ViewChangedEventArgs(changeType, model));
            }
        }

        /// <summary>
        /// Marks the view as invalid, discarding the internal list of items.
        /// This is called e.g. if the query has changed.  To ensure that the view
        /// is valid, call Validate().
        /// </summary>
        protected void Invalidate()
        {
            if (valid)
            {
                OnInvalidate();
                valid = false;

                NotifyViewChanged(YamsterViewChangeType.FlushView, null);
            }
        }

        protected abstract void OnInvalidate();

        /// <summary>
        /// If the view is not valid yet, calling Validate() performs an expensive
        /// operation that rebuilds the internal list of items from scratch.  Thereafter
        /// it will be actively maintained by monitoring for change events.
        /// </summary>
        protected void Validate()
        {
            if (valid)
                return;

            RequireNotDisposed();

            if (CompiledFunc != null)
            {
                OnValidate();
            }

            valid = true;
        }

        protected abstract void OnValidate();
    }

}
