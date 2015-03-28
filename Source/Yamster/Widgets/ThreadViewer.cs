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
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Gdk;
using Gtk;
using Yamster.Core;

namespace Yamster
{
    // Displays a Yammer thread for reading, i.e. formatted text for each message
    // with profile pictures.
    [System.ComponentModel.ToolboxItem(true)]
    public partial class ThreadViewer : Gtk.Bin
    {
        const int MaxMessagesToShow = 1000;

        AppContext appContext;
        YamsterCache yamsterCache;
        
        IList<YamsterMessage> messagesToLoad = null;
        YamsterThread threadToLoad = null;
        YamsterMessage messageToHighlight = null;

        bool destroyed = false;
        bool finishedLoading = false;
        ActionLagger rebuildViewLagger;
        bool suppressEvents = false;
        Dictionary<long, ThreadViewerMessageTile> tilesByMessageId = new Dictionary<long,ThreadViewerMessageTile>();
        FreshenThreadRequest freshenThreadRequest;

        public ThreadViewer()
        {
            appContext = AppContext.Default;
            yamsterCache = appContext.YamsterCache;

            this.Build();

            RebuildView();

            rebuildViewLagger = new ActionLagger(RebuildView);

            GLib.Timeout.Add(500, ProcessLoadingTimeout);

            yamsterCache.MessageChanged += YamsterCache_MessageChanged;
            yamsterCache.ThreadChanged += YamsterCache_ThreadChanged;

            UpdateUI();
        }

        public override void Destroy()
        {
            destroyed = true;
            this.rebuildViewLagger.Dispose();
            yamsterCache.MessageChanged -= YamsterCache_MessageChanged;
            yamsterCache.ThreadChanged -= YamsterCache_ThreadChanged;
            base.Destroy();
        }

        public YamsterThread LoadedThread { get { return this.threadToLoad; } }

        public YamsterMessage MessageToHighlight { get { return this.messageToHighlight; } }

        public void LoadThread(YamsterThread thread, YamsterMessage messageToHighlight = null)
        {
            if (messageToHighlight != null && messageToHighlight.Thread != thread)
                throw new ArgumentException("messageToHighlight must belong to the specified thread");

            this.threadToLoad = thread;
            this.messagesToLoad = threadToLoad.Messages;
            this.messageToHighlight = messageToHighlight;

            rebuildViewLagger.RequestAction();
            UpdateUI();
        }

        public void LoadMessageList(IEnumerable<YamsterMessage> messagesToLoad)
        {
            this.threadToLoad = null;

            this.messagesToLoad = messagesToLoad.OrderBy(x => x.MessageId).ToList();
            this.messageToHighlight = null;

            rebuildViewLagger.RequestAction();
            UpdateUI();
        }

        void UpdateUI()
        {
            btnOpenThread.Sensitive = threadToLoad != null;
            chkThreadRead.Sensitive = threadToLoad != null;
            btnNewThread.Sensitive = this.messagesToLoad != null && this.messagesToLoad.Count > 0;
            btnRefreshThread.Sensitive = this.threadToLoad != null
                && freshenThreadRequest == null;

            btnRefreshThread.Label = freshenThreadRequest != null ? "Refreshing..." : "Refresh";
        }

        bool ProcessLoadingTimeout()
        {
            if (destroyed)
                return false;

            try
            {
                if (!finishedLoading)
                {
                    var children = tilesByMessageId.Values
                        .Where(x => !x.FinishedLoading)
                        .ToArray();

                    if (children.Length == 0)
                    {
                        finishedLoading = true;
                    }
                    else foreach (var child in children)
                    {
                        child.FinishLoading();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Discarded exception from timer event: " + ex.Message);
            }
            return true;
        }

        void RebuildView()
        {
            // Remove the old items
            var children = ctlVBox.AllChildren.OfType<Bin>()
                .Where(x => x is ThreadViewerMessageTile || x is ThreadViewerDivider)
                .ToArray();
            foreach (var child in children)
            {
                ctlVBox.Remove(child);
            }
            tilesByMessageId.Clear();

            var oldReplyToMessage = ctlMessageComposer.MessageBeingRepliedTo;
            ctlMessageComposer.SetIdle();

            if (messagesToLoad == null || messagesToLoad.Count == 0)
                return;

            var trimmedList = messagesToLoad;

            // Don't show more than MaxMessagesToShow, since the rendering chokes
            // when there are too many controls.
            // TODO: Should we shown an ellipsis in this case?
            int trimIndex = trimmedList.Count - MaxMessagesToShow;
            if (trimIndex > 0)
            {
                trimmedList = messagesToLoad.Skip(trimIndex).ToArray();
            }

            long lastThreadId = -1;
            int counter = 0;
            ThreadViewerMessageTile tileWidget = null;
            foreach (var message in trimmedList)
            {
                if (counter > 0 && lastThreadId != message.ThreadId)
                {
                    // If the previous control is a tileWidget, then
                    // hide its separator
                    if (tileWidget != null)
                        tileWidget.ShowSeparator = false;

                    // Add a divider
                    var dividerWidget = new ThreadViewerDivider();
                    dividerWidget.Name = "DividerTile" + counter.ToString();
                    dividerWidget.Events = EventMask.ButtonPressMask;

                    ctlVBox.Add(dividerWidget);

                    Box.BoxChild box2 = (Box.BoxChild)this.ctlVBox[dividerWidget];
                    box2.Position = counter;
                    box2.Expand = false;
                    box2.Fill = false;
                    ++counter;

                    dividerWidget.Show();
                }
                lastThreadId = message.ThreadId;

                tileWidget = new ThreadViewerMessageTile(this);
                tileWidget.Name = "MessageTile" + counter.ToString();
                tileWidget.Events = EventMask.ButtonPressMask;

                tileWidget.LoadMessage(message);

                if (messageToHighlight != null)
                {
                    if (message.MessageId == messageToHighlight.MessageId)
                        tileWidget.Highlighted = true;
                }

                ctlVBox.Add(tileWidget);
                tilesByMessageId.Add(message.MessageId, tileWidget);

                Box.BoxChild box = (Box.BoxChild)this.ctlVBox[tileWidget];
                box.Position = counter;
                box.Expand = false;
                box.Fill = false;
                ++counter;

                tileWidget.Show();

                // Attempt to restore the reply context
                if (message == oldReplyToMessage)
                {
                    ctlMessageComposer.ReplyToMessage(oldReplyToMessage);
                    oldReplyToMessage = null;
                }
            }

            if (ctlMessageComposer.ComposerMode == MessageComposerMode.Idle)
            {
                // If we lost our message, then default to replying to the whole thread
                ctlMessageComposer.ReplyToThread(trimmedList.Last().Thread);
            }

            SetThreadReadWithoutEvents(threadToLoad != null ? threadToLoad.Read : false);
            finishedLoading = false;
        }

        void YamsterCache_MessageChanged(object sender, YamsterMessageChangedEventArgs e)
        {
            switch (e.ChangeType)
            {
                case YamsterModelChangeType.PropertyChanged:
                    ThreadViewerMessageTile widget;
                    if (tilesByMessageId.TryGetValue(e.Message.MessageId, out widget))
                    {
                        widget.UpdateUI();
                    }
                    break;
                case YamsterModelChangeType.Added:
                    // Was a thread specified (instead of an ad hoc list of messages?)
                    if (LoadedThread != null)
                    {
                        // Does the new message belong to that thread?
                        if (e.Message.ThreadId == LoadedThread.ThreadId)
                        {
                            rebuildViewLagger.RequestAction();
                        }
                    }
                    break;
            }
        }

        void YamsterCache_ThreadChanged(object sender, YamsterThreadChangedEventArgs e)
        {
            switch (e.ChangeType)
            {
                case YamsterModelChangeType.PropertyChanged:
                    if (LoadedThread != null && e.Thread.ThreadId == LoadedThread.ThreadId)
                    {
                        SetThreadReadWithoutEvents(LoadedThread.Read);
                    }
                    break;
            }
        }

        protected void btnOpenThread_Clicked(object sender, EventArgs e)
        {
            if (threadToLoad == null)
                return;
            string url = threadToLoad.ThreadStarterMessage.WebUrl;
            if (string.IsNullOrWhiteSpace(url))
                return;
            Process.Start(url);
        }

        protected void chkThreadRead_Toggled(object sender, EventArgs e)
        {
            if (suppressEvents)
                return;
            if (threadToLoad == null)
                return;

            threadToLoad.SetReadStatusForAllMessages(chkThreadRead.Active);
        }

        // Assigns chkThreadRead.Active without firing chkThreadRead_Toggled 
        void SetThreadReadWithoutEvents(bool active)
        {
            try
            {
                suppressEvents = true;
                chkThreadRead.Active = active;
            }
            finally
            {
                suppressEvents = false;
            }
        }

        internal void NotifyReplyClicked(ThreadViewerMessageTile messageTile)
        {
            ctlMessageComposer.ReplyToMessage(messageTile.LoadedMessage);

            // Scroll to the bottom of the window
            for (Widget widget = ctlVBox; widget != null; widget = widget.Parent)
            {
                var scrolledWindow = widget as ScrolledWindow;
                if (scrolledWindow != null)
                {
                    scrolledWindow.Vadjustment.Value = scrolledWindow.Vadjustment.Upper;
                }
            }

            ctlMessageComposer.FocusEditor();
        }

        protected void btnNewThread_Clicked(object sender, EventArgs e)
        {
            if (this.messagesToLoad != null && this.messagesToLoad.Count > 0)
            {
                var group = this.messagesToLoad.Last().Group;
                this.LoadMessageList(new YamsterMessage[0]);
                ctlMessageComposer.StartNewThread(group);
            }
        }

        protected void btnRefreshThread_Clicked(object sender, EventArgs e)
        {
            if (freshenThreadRequest != null || this.threadToLoad == null)
                return;

            if (!this.appContext.MessagePuller.Enabled)
            {
                Utilities.ShowMessageBox("Yamster is currently running in offline mode.  In order to"
                    + " refresh a thread, you must enable syncing.",
                    "Refresh Thread", ButtonsType.Ok, MessageType.Info);
                return;
            }

            freshenThreadRequest = this.appContext.MessagePuller.FreshenThread(this.threadToLoad);
            freshenThreadRequest.StateChanged += FreshenThreadRequest_StateChanged;
            UpdateUI();
        }

        void FreshenThreadRequest_StateChanged(object sender, EventArgs e)
        {
            if (this.freshenThreadRequest.State == FreshenThreadState.Completed)
            {
                if (this.freshenThreadRequest.Error != null)
                {
                    Utilities.ShowMessageBox("Refresh failed: " + freshenThreadRequest.Error.Message,
                        "Refresh Thread", ButtonsType.Ok, MessageType.Error);
                }
                this.freshenThreadRequest = null;
            }
            UpdateUI();
        }
    }
}

