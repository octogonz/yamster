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
using System.Diagnostics;
using System.Linq;
using Yamster.Core;

namespace Yamster
{
    class YamsterConversation
    {
        public readonly YamsterUserSet UserSet;
        public List<YamsterThread> Threads = new List<YamsterThread>();
        public YamsterMessage LatestMessage { get; private set; }

        public YamsterConversation(YamsterUserSet userSet)
        {
            this.UserSet = userSet;
        }

        public DateTime LastUpdate 
        { 
            get
            {
                if (this.LatestMessage == null)
                    return DateTime.MinValue;
                return this.LatestMessage.CreatedDate;
            }
        }

        public void AddThread(YamsterThread thread)
        {
            Threads.Add(thread);

            var latestMessageInThread = thread.Messages.Last();

            if (this.LatestMessage == null 
                || latestMessageInThread.MessageId > this.LatestMessage.MessageId)
            {
                this.LatestMessage = latestMessageInThread;
            }
        }

        public IEnumerable<YamsterMessage> GetMessages()
        {
            foreach (var thread in Threads)
            {
                foreach (var message in thread.Messages)
                {
                    yield return message;
                }
            }
        }

        // We need this because ReloadConversationsAction() recreates the YamsterConversations,
        // but we want Grid to be able to maintain its selection
        #region Equality
        public override bool Equals(object obj)
        {
            return Equals((YamsterConversation)obj);
        }

        public bool Equals(YamsterConversation other)
        {
            if (other == null)
                return false;
            return this.UserSet.Equals(other.UserSet);
        }

        public override int GetHashCode()
        {
            return this.UserSet.GetHashCode();
        }
        #endregion
    }

    [System.ComponentModel.ToolboxItem(true)]
    public partial class ConversationsScreen : Gtk.Bin
    {
        Dictionary<YamsterUserSet, YamsterConversation> conversationsByUserSet = new Dictionary<YamsterUserSet, YamsterConversation>();

        AppContext appContext;
        YamsterCache yamsterCache;
        ActionLagger reloadConversationsLagger;
        long currentUserId = 0;

        public ConversationsScreen()
        {
            appContext = AppContext.Default;
            yamsterCache = appContext.YamsterCache;

            this.Build();

            ctlGrid.ItemType = typeof(YamsterConversation);
            SetupColumns();

            reloadConversationsLagger = new ActionLagger(ReloadConversationsAction, 1000);
            reloadConversationsLagger.RequestAction();

            ctlGrid.FilterDelegate = ctlGrid_Filter;

            this.yamsterCache.MessageChanged += yamsterCache_MessageChanged;
        }

        public override void Dispose()
        {
            reloadConversationsLagger.Dispose();
            base.Dispose();
        }

        protected override void OnShown()
        {
            base.OnShown();
            UpdateUI(); // this doesn't seem to work from the constructor
        }

        public ThreadViewer ThreadViewer { get; set; }

        void SetupColumns()
        {
            ctlGrid.AddTextColumn("Participants", 220,
                (YamsterConversation conversation) => conversation.UserSet.ToString(this.currentUserId)
            );

            ctlGrid.AddDateColumn("Last Update", 140,
                (YamsterConversation conversation) => conversation.LastUpdate
            );

            ctlGrid.AddInt32Column("# Threads", 70,
                (YamsterConversation conversation) =>
                    conversation.Threads.Count
            );

            ctlGrid.AddTextColumn("Latest Message", 180,
                (YamsterConversation conversation) =>
                    conversation.LatestMessage != null
                    ? conversation.LatestMessage.GetPreviewText()
                    : ""
            );
        }

        public void ReloadConversationsAction()
        {
            currentUserId = yamsterCache.CurrentUserId;
            var privateGroup = yamsterCache.GetGroupById(YamsterGroup.ConversationsGroupId);

            conversationsByUserSet.Clear();

            foreach (var thread in privateGroup.Threads)
            {
                var participants = thread.ConversationParticipants;
                if (!chkShowMultiParticipant.Active)
                {
                    if (participants.Count > 2)
                    {
                        continue;
                    }
                }
                
                YamsterConversation conversation;
                if (!conversationsByUserSet.TryGetValue(participants, out conversation))
                {
                    conversation = new YamsterConversation(participants);
                    conversationsByUserSet.Add(conversation.UserSet, conversation);
                }

                conversation.AddThread(thread);
            }

            ctlGrid.ReplaceAllItems(
                conversationsByUserSet.Values.OrderByDescending(x => x.LastUpdate)
            );
        }

        void ctlGrid_FocusedItemChanged(object sender, EventArgs e)
        {
            if (ThreadViewer != null)
            {
                YamsterConversation conversation = ctlGrid.FocusedItem as YamsterConversation;
                if (conversation != null && conversation.Threads.Count > 0)
                {
                    ThreadViewer.LoadMessageList(conversation.GetMessages());
                }
            }
        }

        void yamsterCache_MessageChanged(object sender, YamsterMessageChangedEventArgs e)
        {
            switch (e.ChangeType)
            {
                case YamsterModelChangeType.Added:
                    if (e.Message.GroupId == YamsterGroup.ConversationsGroupId)
                        this.reloadConversationsLagger.RequestAction();
                    break;
            }
        }

        protected void chkShowMultiParticipant_Clicked(object sender, EventArgs e)
        {
            reloadConversationsLagger.RequestAction();
        }

        protected void txtSearch_Changed(object sender, EventArgs e)
        {
            ctlGrid.UpdateView();
            UpdateUI();
        }

        void btnCancelSearch_Clicked(object sender, EventArgs e)
        {
            txtSearch.Text = "";
            UpdateUI();
        }

        void UpdateUI()
        {
            bool searching = txtSearch.Text != "";
            this.ctlSearchImage.Visible = !searching;
            this.btnCancelSearch.Visible = searching;
        }

        bool ctlGrid_Filter(Grid sender, object item)
        {
            string searchText = txtSearch.Text.Trim();
            if (!string.IsNullOrEmpty(searchText))
            {
                var conversation = (YamsterConversation)item;
                if (conversation == null)
                    return false;
                string match = conversation.UserSet.ToString();
                if (match.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) < 0)
                    return false; // not found
            }
            return true;
        }
    }
}

