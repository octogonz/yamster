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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Yamster.Core.SQLite;

namespace Yamster.Core
{

    public class YamsterMessage : YamsterModel
    {
        readonly long messageId;
        DbMessage dbMessage;
        DbMessageState dbMessageState;

        // For now, we assume these can never change
        YamsterGroup group;
        YamsterThread thread;
        YamsterUser senderUser;
        YamsterMessage messageRepliedTo;

        readonly YamsterUserSet likingUsers = new YamsterUserSet();
        readonly YamsterUserSet notifiedUsers = new YamsterUserSet();

        string cachedPreviewText = null;

        bool busyProcessingServiceCall = false;

        internal YamsterMessage(long messageId, YamsterCache yamsterCache)
            : base(yamsterCache)
        {
            this.messageId = messageId;
            this.dbMessage = new DbMessage()
            {
                MessageId = messageId, 
                ChangeNumber = 0
            };

            this.dbMessageState = new DbMessageState()
            {
                MessageId = messageId, 
                ChangeNumber = 0
            };
        }

        public override YamsterModelType ModelType { get { return YamsterModelType.Message; } }

        internal DbMessage DbMessage
        {
            get { return this.dbMessage; }
        }
        internal DbMessageState DbMessageState
        {
            get { return this.dbMessageState; }
        }

        public YamsterThread Thread { get { return this.thread; } }
        static internal PropertyInfo Info_Thread = Utilities.GetPropertyInfo(typeof(YamsterMessage), "Thread");

        public YamsterGroup Group { get { return this.group; } }

        public YamsterUser Sender { get { return this.senderUser; } }
        static internal PropertyInfo Info_Sender = Utilities.GetPropertyInfo(typeof(YamsterMessage), "Sender");

        /// <summary>
        /// The message that this message was "in reply to", or null if none.
        /// </summary>
        public YamsterMessage MessageRepliedTo
        {
            get { return this.messageRepliedTo; }
        }

        /// <summary>
        /// The user being replied to (i.e. the sender of MessageRepliedTo), or null if none.
        /// </summary>
        public YamsterUser UserRepliedTo
        {
            get
            {
                if (this.messageRepliedTo != null)
                    return this.messageRepliedTo.Sender;
                return null;
            }
        }

        /// <summary>
        /// The user ID being replied to (i.e. the sender of MessageRepliedTo), or 0 if none.
        /// </summary>
        public long UserIdRepliedTo
        {
            get
            {
                var user = this.UserRepliedTo;
                if (user != null)
                    return user.UserId;
                return 0;
            }
        }
        static internal PropertyInfo Info_UserIdRepliedTo = Utilities.GetPropertyInfo(typeof(YamsterMessage), "UserIdRepliedTo");

        public long MessageId { get { return this.messageId; } }
        static internal PropertyInfo Info_MessageId = Utilities.GetPropertyInfo(typeof(YamsterMessage), "MessageId");

        #region DbMessage Properties

        public long GroupId { get { return DbMessage.GroupId; } }
        public long ThreadId { get { return DbMessage.ThreadId; } }
        public DateTime CreatedDate { get { return DbMessage.CreatedDate; } }
        //public long SenderUserId;
        public int LikesCount { get { return DbMessage.LikesCount; } }

        public string Body { get { return DbMessage.Body; } }
        static internal PropertyInfo Info_Body = Utilities.GetPropertyInfo(typeof(YamsterMessage), "Body");

        public string WebUrl { get { return DbMessage.WebUrl; } }

        public string AttachmentFilename { get { return DbMessage.AttachmentFilename; } }
        public string AttachmentWebUrl { get { return DbMessage.AttachmentWebUrl; } }
        public string AttachmentScaledUrlTemplate { get { return DbMessage.AttachmentScaledUrlTemplate; } }
        public int AttachmentWidth { get { return DbMessage.AttachmentWidth; } }
        public int AttachmentHeight { get { return DbMessage.AttachmentHeight; } }

        public DbMessageType MessageType { get { return DbMessage.MessageType; } }

        #endregion

        #region DbMessageState Properties
        public bool Read
        {
            get { return this.DbMessageState.Read; }
        }
        public bool Starred
        {
            get { return this.DbMessageState.Starred; }
            set
            {
                this.RequireLoaded();
                this.RequireNotDeleted();

                var yamsterCoreDb = this.YamsterCache.AppContext.YamsterCoreDb;

                yamsterCoreDb.Mapper.ExecuteNonQuery(@"
UPDATE " + yamsterCoreDb.MessageStates.TableName + @"
SET [Starred] = ?
WHERE [MessageId] = ?
AND [Starred] <> ?",
                    value,
                    this.messageId,
                    value
                );

                // This is correct, but not very elegant
                var newState = new DbMessageState();
                newState.CopyFrom(this.dbMessageState);
                newState.Starred = value;
                this.YamsterCache.ProcessDbMessageState(newState);
            }
        }
        
        /// <summary>
        /// If true, then this message was seen by Yamster at one time, but was later deleted from the Yammer
        /// service.
        /// </summary>
        /// <remarks>
        /// Due to limitations of the protocol, currently Yamster generally doesn't detect 
        /// deleted messages unless they were deleted using Yamster.
        /// </remarks>
        public bool Deleted
        {
            get { return this.DbMessageState.Deleted; }
        }

        static internal PropertyInfo Info_Starred = Utilities.GetPropertyInfo(typeof(YamsterMessage), "Starred");
        #endregion

        public string GroupName { get { return Group.GroupName; } }
        public string SenderName { get { return Sender.FullName; } }

        /// <summary>
        /// Note: LikingUsers normally returns only the first 3 likes, which
        /// Yammer displays as e.g. "A, B, C, and 3 others like this" (i.e. a separate
        /// service call is needed to fetch the others).  The LikesCount property 
        /// always reports the true total number of likes.
        /// </summary>
        // Same comment on DbMessage.LikingUserIds
        public YamsterUserSet LikingUsers 
        { 
            get { return this.likingUsers; } 
        }
        static internal PropertyInfo Info_LikingUsers = Utilities.GetPropertyInfo(typeof(YamsterMessage), "LikingUsers");

        public bool LikedByCurrentUser
        {
            get
            {
                long currentUserId = this.YamsterCache.CurrentUserId;
                return this.likingUsers.FindUserById(currentUserId) != null;
            }
        }

        public YamsterUserSet NotifiedUsers
        {
            get { return this.notifiedUsers; }
        }
        static internal PropertyInfo Info_NotifiedUsers = Utilities.GetPropertyInfo(typeof(YamsterMessage), "NotifiedUsers");

        protected override bool CheckIfLoaded() // abstract
        {
            return this.dbMessageState.ChangeNumber != 0;
        }

        internal override void FireChangeEvent(YamsterModelChangeType yamsterModelChangeType) // abstract
        {
            this.YamsterCache.FireChangedEvent(new YamsterMessageChangedEventArgs(this, yamsterModelChangeType));
        }

        internal void SetDbMessage(DbMessage newValue, YamsterModelEventCollector eventCollector)
        {
            if (newValue == null)
                throw new ArgumentNullException("DbMessage");
            if (newValue.MessageId != messageId)
                throw new ArgumentException("Cannot change ID");

            var oldValue = this.dbMessage;
            this.dbMessage = newValue;
            this.cachedPreviewText = null;

            this.group = this.YamsterCache.FetchGroupById(this.dbMessage.GroupId, eventCollector);
            this.senderUser = this.YamsterCache.FetchUserById(this.dbMessage.SenderUserId, eventCollector);

            this.messageRepliedTo = null;
            if (this.dbMessage.MessageIdRepliedTo != 0
                // The Yammer UI doesn't show "in reply to" when messageRepliedTo is the threadstarter.
                // TODO: What does it do if the threadstarter's MessageId != ThreadId?
                && this.dbMessage.MessageIdRepliedTo != this.dbMessage.ThreadId)
            {
                this.YamsterCache.ListenForMessageById(
                    this.dbMessage.MessageIdRepliedTo, 
                    this,
                    eventCollector,
                    delegate(YamsterMessage listenedMessage, YamsterModelEventCollector ec)
                    {
                        if (listenedMessage.MessageId == this.dbMessage.MessageIdRepliedTo)
                        {
                            this.messageRepliedTo = listenedMessage;
                            ec.NotifyAfterUpdate(this);
                        }
                    }
                );
            }

            int oldlikingUsersCount = likingUsers.Count;
            likingUsers.Clear();
            foreach (long userId in this.dbMessage.LikingUserIds)
            {
                var user = this.YamsterCache.FetchUserById(userId, eventCollector);
                likingUsers.AddUser(user);
            }

            if (this.thread != null)
            {
                int totalLikesCountDelta = newValue.LikesCount - oldValue.LikesCount;
                if (totalLikesCountDelta != 0)
                    this.thread.NotifyTotalLikesChanged(totalLikesCountDelta, eventCollector);
            }
                
            notifiedUsers.Clear();
            foreach (long userId in this.dbMessage.NotifiedUserIds)
            {
                var user = this.YamsterCache.FetchUserById(userId, eventCollector);
                notifiedUsers.AddUser(user);
            }

            UpdateLoadedStatus();

            eventCollector.NotifyAfterUpdate(this);
        }

        internal void SetDbMessageState(DbMessageState newValue, YamsterModelEventCollector eventCollector)
        {
            if (newValue == null)
                throw new ArgumentNullException("DbMessageState");
            if (newValue.MessageId != messageId)
                throw new ArgumentException("Cannot change ID");

            bool oldRead = this.Read;
            bool oldDeleted = this.Deleted;

            if (newValue.Deleted != oldDeleted)
            {
                // Remove and re-add the message so it moves to the appropriate
                // collection (YamsterThread.Messages or DeletedMessages).
                // (Note that NotifyMessageReadChanged() assumes that the message
                // is in the right collection.)
                this.Thread.RemoveMessage(this, eventCollector);

                this.dbMessageState = newValue;

                this.Thread.AddMessage(this, eventCollector);

                this.cachedPreviewText = null;
            } else {
                this.dbMessageState = newValue;

                if (this.Read != oldRead && !this.Deleted)
                {
                    this.Thread.NotifyMessageReadChanged(this.Read, eventCollector);
                }
            }

            UpdateLoadedStatus();

            eventCollector.NotifyAfterUpdate(this);
        }

        internal void NotifyAddedToThread(YamsterThread thread)
        {
            this.thread = thread;
        }

        public string GetPreviewText()
        {
            if (cachedPreviewText == null)
            {
                string text = this.Body;
                if (this.Deleted) 
                    text = "[Deleted] " + text;
                if (!string.IsNullOrWhiteSpace(this.AttachmentWebUrl))
                    text += " (image)";
                text = Regex.Replace(text, "[\r\n\t ]+", " ").Trim();
                cachedPreviewText = Utilities.TruncateWithEllipsis(text, 100);
            }
            return cachedPreviewText;
        }

        /// <summary>
        /// Sets the message as "liked" or "unliked" by the current user.
        /// </summary>
        public async Task SetLikeStatusAsync(bool liked, bool forceUpdate=false)
        {
            this.RequireLoaded();
            this.RequireNotDeleted();

            if (this.LikedByCurrentUser == liked && !forceUpdate)
                return;

            if (this.busyProcessingServiceCall)
            {
                // Prevent the UI from queueing multiple requests for the same message
                throw new InvalidOperationException(
                    "The service is still waiting for a previous operation to complete.  Please try again later.");
            }

            try
            {
                busyProcessingServiceCall = true;

                var appContext = this.YamsterCache.AppContext;
                await appContext.YamsterApi.SetMessageLikeStatusAsync(this.messageId, liked);

                this.YamsterCache.AppContext.RequireForegroundThread();

                // After SetMessageLikeStatus has completed successfully, we know that Yammer 
                // has applied the change, so it's safe to update the database record.
                // But we need to be careful to avoid a race condition with the MessagePuller,
                // and we need to fetch the most current record from the database.
                var yamsterCoreDb = this.YamsterCache.AppContext.YamsterCoreDb;
                using (var transaction = yamsterCoreDb.BeginTransaction())
                {
                    var coreMessage = yamsterCoreDb.Messages.Query("WHERE [MessageId] = " + this.MessageId)
                        .FirstOrDefault();

                    if (coreMessage != null)
                    {
                        long currentUserId = this.YamsterCache.CurrentUserId;
                        if (liked)
                        {
                            if (!coreMessage.LikingUserIds.Contains(currentUserId))
                            {
                                coreMessage.LikingUserIds.Insert(0, currentUserId);
                                ++coreMessage.LikesCount;
                            }
                        }
                        else
                        {
                            if (coreMessage.LikingUserIds.Remove(currentUserId))
                            {
                                --coreMessage.LikesCount;
                            }
                        }
                        yamsterCoreDb.Messages.InsertRecord(coreMessage, SQLiteConflictResolution.Replace);
                    }

                    transaction.Commit();
                }
            }
            finally
            {
                busyProcessingServiceCall = false;
            }
        }

        public void DeleteFromServer()
        {
            var task = DeleteFromServerAsync();
            ForegroundSynchronizationContext.RunSynchronously(task);
        }

        public async Task DeleteFromServerAsync()
        {
            await this.YamsterCache.AppContext.YamsterApi.DeleteMessageAsync(this.MessageId);

            // The eventing system doesn't handle deletes yet, so for now just alter
            // the message body to indicate that it was deleted.
            var yamsterCoreDb = this.YamsterCache.AppContext.YamsterCoreDb;
            
            this.MarkAsDeleted();
        }

        internal void MarkAsDeleted()
        {
            if (this.Deleted)
                return;

            var yamsterCoreDb = this.YamsterCache.AppContext.YamsterCoreDb;

            yamsterCoreDb.Mapper.ExecuteNonQuery(@"
UPDATE " + yamsterCoreDb.MessageStates.TableName + @"
SET [Deleted] = 1
WHERE [MessageId] = ?",
                this.messageId
            );

            // This is correct, but not very elegant
            var newState = new DbMessageState();
            newState.CopyFrom(this.dbMessageState);
            newState.Deleted = true;
            this.YamsterCache.ProcessDbMessageState(newState);

            Debug.Assert(this.Deleted);
        }

        void RequireNotDeleted()
        {
            if (this.Deleted)
            {
                throw new InvalidOperationException("This operation cannot be performed because message has been deleted.");
            }
        }

        public override string ToString()
        {
            return GetNotLoadedPrefix() + "Message #" + this.MessageId;
        }
    }

    public class YamsterMessageChangedEventArgs : ModelChangedEventArgs
    {
        public YamsterMessage Message { get; private set; }

        public YamsterMessageChangedEventArgs(YamsterMessage message, YamsterModelChangeType changeType)
            : base(changeType)
        {
            this.Message = message;
        }

        public override YamsterModel Model
        {
            get { return this.Message; }
        }

        public override string ToString()
        {
            return string.Format("Message {0}: ID={1} Preview=\"{2}\"", this.ChangeType, this.Message.MessageId,
                this.Message.GetPreviewText());
        }
    }

}
