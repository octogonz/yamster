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
    public enum YamsterModelType
    {
        User,
        Group,
        Thread,
        Message
    }

    public abstract class YamsterModel
    {
        public readonly YamsterCache YamsterCache;

        public bool IsLoaded { get; private set; }

        internal YamsterModel(YamsterCache yamsterCache)
        {
            this.YamsterCache = yamsterCache;
            this.IsLoaded = false;
        }

        abstract public YamsterModelType ModelType { get; }

        protected void UpdateLoadedStatus()
        {
            if (!this.IsLoaded)
            {
                this.IsLoaded = CheckIfLoaded();
            }
        }

        protected abstract bool CheckIfLoaded();

        protected void RequireLoaded()
        {
            if (!this.IsLoaded)
            {
                throw new InvalidOperationException("The operation cannot be performed yet"
                    + " because the model has not finished loading");
            }
        }

        internal abstract void FireChangeEvent(YamsterModelChangeType yamsterModelChangeType);

        protected string GetNotLoadedPrefix()
        {
            return this.IsLoaded ? "" : "*** ";
        }

        static public Type GetModelClass(YamsterModelType modelType)
        {
            switch (modelType)
            {
                case YamsterModelType.Group:
                    return typeof(YamsterGroup);
                case YamsterModelType.Thread:
                    return typeof(YamsterThread);
                case YamsterModelType.Message:
                    return typeof(YamsterMessage);
                case YamsterModelType.User:
                    return typeof(YamsterUser);
                default:
                    throw new NotSupportedException("Unsupported model type: " + modelType.ToString());
            }
        }
    }

}
