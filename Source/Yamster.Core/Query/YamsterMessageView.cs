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
    public class YamsterMessageQuery : YamsterModelQuery
    {
        public YamsterMessageQuery(string title, YqlNode filterNode)
            : base(title, filterNode)
        {
        }
        public YamsterMessageQuery()
        {
        }

        public override YamsterModelType ModelType
        {
            get { return YamsterModelType.Message; }
        }
    }

    public class YamsterMessageView : YamsterModelView
    {
        readonly Dictionary<long, YamsterMessage> messagesById = new Dictionary<long, YamsterMessage>();

        public YamsterMessageView(AppContext appContext)
            : base(appContext)
        {
            yamsterCache.MessageChanged += YamsterCache_MessageChanged;
        }

        public override void Dispose()
        {
            if (!IsDisposed)
            {
                yamsterCache.MessageChanged -= YamsterCache_MessageChanged;
            }

            base.Dispose();
        }

        public ReadOnlyCollection<YamsterMessage> GetMessagesInView()
        {
            Validate();
            return new ReadOnlyCollection<YamsterMessage>(this.messagesById.Values.ToList());
        }

        protected override void OnInvalidate() // abstract
        {
            messagesById.Clear();
        }

        protected override void OnValidate() // abstract
        {
            var exectionContext = new YqlExecutionContext(appContext);

            foreach (var message in appContext.YamsterCache.GetAllMessages())
            {
                exectionContext.Message = message;
                if (CompiledFunc(exectionContext))
                {
                    this.messagesById.Add(message.MessageId, message);
                }
            }
        }

        void YamsterCache_MessageChanged(object sender, YamsterMessageChangedEventArgs e)
        {
            switch (e.ChangeType)
            {
                case YamsterModelChangeType.Added:
                case YamsterModelChangeType.PropertyChanged:
                    UpdateViewWithMessage(e.Message);
                    break;
            }
        }

        void UpdateViewWithMessage(YamsterMessage message)
        {
            bool shouldBeInView = false;
            if (CompiledFunc != null)
            {
                var exectionContext = new YqlExecutionContext(this.appContext);
                exectionContext.Message = message;
                shouldBeInView = CompiledFunc(exectionContext);
            }

            bool isInView = messagesById.ContainsKey(message.MessageId);

            if (isInView)
            {
                if (!shouldBeInView)
                {
                    this.messagesById.Remove(message.MessageId);
                    NotifyViewChanged(YamsterViewChangeType.ModelLeaveView, message);
                }
            }
            else
            {
                if (shouldBeInView)
                {
                    this.messagesById.Add(message.MessageId, message);
                    NotifyViewChanged(YamsterViewChangeType.ModelEnterView, message);
                }
            }
        }

    }

}
