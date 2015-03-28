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
using System.Reflection;

namespace Yamster.Core
{
    public class YamsterThread : YamsterModel
    {
        public long ThreadId { get; private set; }
        static internal PropertyInfo Info_ThreadId = Utilities.GetPropertyInfo(typeof(YamsterThread), "ThreadId");

        public long ConversationId { get; private set; }
        public YamsterGroup Group { get; private set; }

        DbThreadState dbThreadState;

        List<YamsterMessage> messagesInternal = new List<YamsterMessage>();
        int readMessageCount = 0;
        YamsterUserSet participants;
        int totalLikesCount = 0;

        internal YamsterThread(long threadId, YamsterGroup group, YamsterCache yamsterCache)
            : base(yamsterCache)
        {
            this.ThreadId = threadId;
            this.Group = group;
            this.participants = YamsterUserSet.EmptyUserSet;

            this.dbThreadState = new DbThreadState() {
                ThreadId = threadId,
                ChangeNumber = 0
            };
        }

        public override YamsterModelType ModelType { get { return YamsterModelType.Thread; } }

        internal DbThreadState DbThreadState
        {
            get { return this.dbThreadState; }
        }

        public long GroupId { get { return this.Group.GroupId; } }

        public ReadOnlyCollection<YamsterMessage> Messages
        {
            get
            {
                return new ReadOnlyCollection<YamsterMessage>(
                    this.messagesInternal
                );
            }
        }

        /// <summary>
        /// Returns the timestamp of the most recent message in the thread, i.e. the last
        /// time a user posted to this thread.
        /// </summary>
        public DateTime LastUpdate
        {
            get
            {
                return this.messagesInternal.Last().CreatedDate;
            }
        }

        /// <summary>
        /// This is the sum of YamsterMessage.LikesCount for all messages in this thread.
        /// It's useful as a general indicator of the popularity of the thread.
        /// </summary>
        public int TotalLikesCount
        {
            get
            {
                // Debug.Assert(totalLikesCount == this.messagesInternal.Sum(x => x.LikesCount));
                return this.totalLikesCount; 
            }
        }

        public YamsterUserSet ConversationParticipants 
        {
            get { return participants; }
        }

        public YamsterMessagesRead MessagesRead
        {
            get
            {
                if (readMessageCount == 0)
                    return YamsterMessagesRead.None;
                if (readMessageCount == this.messagesInternal.Count)
                    return YamsterMessagesRead.All;
                return YamsterMessagesRead.Some;
            }
        }

        public bool Read
        {
            get
            {
                return this.MessagesRead == YamsterMessagesRead.All;
            }
        }
        static internal PropertyInfo Info_Read = Utilities.GetPropertyInfo(typeof(YamsterThread), "Read");

        public YamsterMessage ThreadStarterMessage
        {
            get
            {
                return this.Messages.First();
            }
        }

        /// <summary>
        /// This is true if the thread was ever seen in the Yammer inbox feed.
        /// (Clicking Yammer's "Remove from Inbox" button does not clear this flag.)
        /// </summary>
        public bool SeenInInboxFeed
        {
            get
            {
                return this.DbThreadState.SeenInInboxFeed;
            }
        }
        static internal PropertyInfo Info_SeenInInboxFeed = Utilities.GetPropertyInfo(typeof(YamsterThread), "SeenInInboxFeed");

        protected override bool CheckIfLoaded() // abstract
        {
            return this.dbThreadState.ChangeNumber != 0;
        }

        internal override void FireChangeEvent(YamsterModelChangeType yamsterModelChangeType) // abstract
        {
            this.YamsterCache.FireChangedEvent(new YamsterThreadChangedEventArgs(this, yamsterModelChangeType));
        }

        internal void SetDbThreadState(DbThreadState newValue, YamsterModelEventCollector eventCollector)
        {
            if (newValue == null)
                throw new ArgumentNullException("DbThreadState");
            if (newValue.ThreadId != this.ThreadId)
                throw new ArgumentException("Cannot change ID");
            this.dbThreadState = newValue;
            UpdateLoadedStatus();
            eventCollector.NotifyAfterUpdate(this);
        }

        internal void AddMessage(YamsterMessage message, YamsterModelEventCollector eventCollector)
        {
            // Insert sorted by MessageId
            int index = this.messagesInternal.BinarySearch(message,
                Comparer<YamsterMessage>.Create((x, y) => Math.Sign(x.MessageId - y.MessageId)));
            if (index >= 0)
            {
                throw new InvalidOperationException("Program Bug: The message was already added to this thread");
            }

            bool oldThreadRead = this.Read;

            this.messagesInternal.Insert(~index, message);
            message.NotifyAddedToThread(this);

            if (message.Read)
                ++readMessageCount;

            if (this.Read != oldThreadRead)
            {
                this.Group.NotifyThreadReadChanged(this.Read, eventCollector);
            }

            this.totalLikesCount += message.LikesCount;
            Debug.Assert(totalLikesCount == this.messagesInternal.Sum(x => x.LikesCount));

            // Adding a message changes LastUpdate, Messages.Count, and possibly other properties
            // like TotalLikesCount
            eventCollector.NotifyAfterUpdate(this);
        }

        internal void NotifyMessageReadChanged(bool newReadValue, YamsterModelEventCollector eventCollector)
        {
            bool oldThreadRead = this.Read;
            
            if (newReadValue)
                ++readMessageCount;
            else
                --readMessageCount;

            Debug.Assert(readMessageCount >= 0 && readMessageCount <= messagesInternal.Count);
            
            if (this.Read != oldThreadRead)
            {
                this.Group.NotifyThreadReadChanged(this.Read, eventCollector);
                eventCollector.NotifyAfterUpdate(this);
            }
        }

        internal void NotifyTotalLikesChanged(int totalLikesCountDelta, YamsterModelEventCollector eventCollector)
        {
            this.totalLikesCount += totalLikesCountDelta;
            Debug.Assert(totalLikesCount == this.messagesInternal.Sum(x => x.LikesCount));
            eventCollector.NotifyAfterUpdate(this);
        }

        internal void UpdateConversation(DbConversation conversation, YamsterModelEventCollector eventCollector)
        {
            this.ConversationId = conversation.ConversationId;

            var users = new List<YamsterUser>(conversation.ParticipantUserIds.Count);
            foreach (long userId in conversation.ParticipantUserIds)
            {
                var user = YamsterCache.FetchUserById(userId, eventCollector);
                users.Add(user);
            }
            this.participants = new YamsterUserSet(users);
            eventCollector.NotifyAfterUpdate(this);
        }

        public void SetReadStatusForAllMessages(bool read)
        {
            var yamsterCoreDb = this.YamsterCache.AppContext.YamsterCoreDb;

            yamsterCoreDb.Mapper.ExecuteNonQuery(@"
UPDATE " + yamsterCoreDb.MessageStates.TableName + @"
SET [Read] = ?
WHERE [MessageId] IN
(
    SELECT [MessageId] FROM " + yamsterCoreDb.Messages.TableName + @" WHERE [ThreadId] = ?
)
AND [Read] <> ?
",
                read,
                this.ThreadId,
                read
            );
        }

        public override string ToString()
        {
            return GetNotLoadedPrefix() + string.Format("Thread #{0} ({1} messages)", 
                this.ThreadId, this.messagesInternal.Count);
        }
    }

    public class YamsterThreadChangedEventArgs : ModelChangedEventArgs
    {
        public YamsterThread Thread { get; private set; }

        public YamsterThreadChangedEventArgs(YamsterThread thread, YamsterModelChangeType changeType)
            : base(changeType)
        {
            this.Thread = thread;
        }

        public override YamsterModel Model
        {
            get { return this.Thread; }
        }

        public override string ToString()
        {
            return string.Format("Thread {0}: ID={1}", this.ChangeType, this.Thread.ThreadId);
        }
    }

}
