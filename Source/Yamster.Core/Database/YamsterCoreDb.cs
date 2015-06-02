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
using Yamster.Core.SQLite;

namespace Yamster.Core
{
    public partial class YamsterCoreDb : SQLiteDataContext
    {
        YamsterArchiveDb archiveDb;

        [SQLiteMapperTable]
        MappedTable<DbPropertiesRow> DbProperties = null;

        [SQLiteMapperTable]
        public MappedTable<DbSyncingFeed> SyncingFeeds;

        [SQLiteMapperTable]
        public MappedTable<DbSyncingThread> SyncingThreads;

        [SQLiteMapperTable]
        public MappedTable<DbGroup> Groups;

        [SQLiteMapperTable]
        public MappedTable<DbGroupState> GroupStates;

        [SQLiteMapperTable]
        public MappedTable<DbThreadState> ThreadStates;

        [SQLiteMapperTable]
        public MappedTable<DbConversation> Conversations;

        [SQLiteMapperTable]
        public MappedTable<DbMessage> Messages;

        [SQLiteMapperTable]
        public MappedTable<DbMessageState> MessageStates;

        [SQLiteMapperTable]
        public MappedTable<DbUser> Users;

        [DefaultValue(1000)]
        public int ChangeCheckIntervalMs { get; set; }

        public DbPropertiesRow Properties
        {
            get
            {
                return this.DbProperties.QueryAll().FirstOrDefault();
            }
        }

        int LastCheckForChanges;

        public YamsterCoreDb(YamsterArchiveDb archiveDb)
            : base(archiveDb.Mapper, archiveDb.BeforeUpgradeHandler, archiveDb.AfterUpgradeHandler)
        {
            this.archiveDb = archiveDb;

            this.ChangeCheckIntervalMs = 1000;

            LastCheckForChanges = Environment.TickCount;

            UpgradeDatabase();

            archiveDb.ConversationRefInserted += archiveDb_ConversationRefInserted;
            archiveDb.GroupRefInserted += archiveDb_GroupRefInserted;
            archiveDb.MessageInserted += archiveDb_MessageInserted;
            archiveDb.UserRefInserted += archiveDb_UserRefInserted;

            this.DiscardChanges();
        }

        public void CheckForChanges(bool force=false)
        {
            int now = Environment.TickCount;

            if (!force)
            {
                if (unchecked(now - LastCheckForChanges) < ChangeCheckIntervalMs)
                    return;
            }

            LastCheckForChanges = now;

            // Process the graph leaf-first to minimize unresolved references
            this.Users.CheckForChanges();
            this.Groups.CheckForChanges();
            this.GroupStates.CheckForChanges();
            this.ThreadStates.CheckForChanges();
            this.Conversations.CheckForChanges();
            this.Messages.CheckForChanges();
            this.MessageStates.CheckForChanges();
        }

        void DiscardChanges()
        {
            this.Users.DiscardChanges();
            this.Groups.DiscardChanges();
            this.GroupStates.DiscardChanges();
            this.ThreadStates.DiscardChanges();
            this.Conversations.DiscardChanges();
            this.Messages.DiscardChanges();
            this.MessageStates.DiscardChanges();
        }

        void archiveDb_ConversationRefInserted(object sender, ArchiveInsertEventArgs<DbArchiveRecord> e)
        {
            UpdateConversation(e.Record);
        }

        void archiveDb_GroupRefInserted(object sender, ArchiveInsertEventArgs<DbArchiveRecord> e)
        {
            UpdateGroup(e.Record);
        }

        void archiveDb_MessageInserted(object sender, ArchiveInsertEventArgs<DbArchiveMessageRecord> e)
        {
            UpdateMessage(e.Record);
        }

        void archiveDb_UserRefInserted(object sender, ArchiveInsertEventArgs<DbArchiveRecord> e)
        {
            UpdateUser(e.Record);
        }

        void UpdateConversation(DbArchiveRecord archiveConversationRef)
        {
            JsonConversationReference conversationRef = SQLiteJsonConverter.LoadFromJson<JsonConversationReference>(
                archiveConversationRef.Json);

            DbConversation coreConversation = new DbConversation();
            coreConversation.LastFetchedUtc = archiveConversationRef.LastFetchedUtc;
            coreConversation.ConversationId = conversationRef.Id;
            coreConversation.ParticipantUserIds.AssignFrom(conversationRef.Participants.Select(x => x.Id));

            Conversations.InsertRecord(coreConversation, SQLiteConflictResolution.Replace);
        }

        void UpdateGroup(DbArchiveRecord archiveGroupRef)
        {
            JsonGroupReference groupRef = SQLiteJsonConverter.LoadFromJson<JsonGroupReference>(archiveGroupRef.Json);

            bool incompleteRecord = false;

            DbGroup coreGroup = new DbGroup();
            coreGroup.LastFetchedUtc = archiveGroupRef.LastFetchedUtc;
            coreGroup.GroupId = groupRef.Id;
            coreGroup.GroupName = groupRef.FullName ?? "";
            coreGroup.GroupDescription = groupRef.Description ?? "";

            if (!Enum.TryParse<DbGroupPrivacy>(groupRef.Privacy, true, out coreGroup.Privacy))
            {
                if (!string.IsNullOrEmpty(groupRef.Privacy))
                    throw new YamsterProtocolException(string.Format("Unsupported group privacy \"{0}\"", coreGroup.Privacy));
                coreGroup.Privacy = DbGroupPrivacy.Unknown;
                incompleteRecord = true;
            }

            coreGroup.WebUrl = groupRef.WebUrl ?? "";
            coreGroup.MugshotUrl = groupRef.MugshotUrl ?? "";

            // If the record is incomplete, don't overwrite an existing record that might have complete data
            // TODO: this merging should be more fine-grained
            Groups.InsertRecord(coreGroup, incompleteRecord ? SQLiteConflictResolution.Ignore : SQLiteConflictResolution.Replace);

            DbGroupState groupState = new DbGroupState() { GroupId = groupRef.Id };
            GroupStates.InsertRecord(groupState, SQLiteConflictResolution.Ignore);
        }

        void UpdateMessage(DbArchiveMessageRecord archiveMessage)
        {
            JsonMessage message = SQLiteJsonConverter.LoadFromJson<JsonMessage>(archiveMessage.Json);

            DbMessage coreMessage = new DbMessage();
            coreMessage.LastFetchedUtc = archiveMessage.LastFetchedUtc;
            coreMessage.MessageId = message.Id;

            coreMessage.GroupId = archiveMessage.GroupId;
            coreMessage.ThreadId = message.ThreadId;
            coreMessage.ConversationId = message.ConversationId;
            coreMessage.CreatedDate = message.Created;
            coreMessage.SenderUserId = message.SenderId;
            coreMessage.MessageIdRepliedTo = message.RepliedToId ?? 0;
            
            coreMessage.LikingUserIds.AssignFrom(message.Likes.Users.Select(x => x.UserId));
            foreach (var likingUser in message.Likes.Users)
            {
                // We don't get a proper UserReference for liking users, but we do get
                // some basic information.  Write this to the Users table *only* if there
                // is not already some real data there.
                DbUser coreUser = new DbUser();
                coreUser.LastFetchedUtc = archiveMessage.LastFetchedUtc;
                coreUser.UserId = likingUser.UserId;
                coreUser.FullName = likingUser.FullName ?? "";
                coreUser.JobTitle = "";
                coreUser.WebUrl = ""; // we could infer this from likingUser.Alias
                coreUser.MugshotUrl = "";

                // Ignore = only write if there isn't already an existing record
                Users.InsertRecord(coreUser, SQLiteConflictResolution.Ignore);
            }

            coreMessage.LikesCount = message.Likes.Count;
            coreMessage.NotifiedUserIds.AssignFrom(message.NotifiedUserIds ?? new long[0]);
            coreMessage.Body = message.Body.Plain ?? "";
            coreMessage.WebUrl = message.Permalink ?? "";

            var firstImageAttachment = message.Attachments.Where(x => x.AttachmentType == "image").FirstOrDefault();
            if (firstImageAttachment != null)
            {
                coreMessage.AttachmentFilename = firstImageAttachment.Name;
                coreMessage.AttachmentWebUrl = firstImageAttachment.WebUrl;
                coreMessage.AttachmentScaledUrlTemplate = firstImageAttachment.ScaledUrlTemplate;
                coreMessage.AttachmentWidth = firstImageAttachment.Width ?? 0;
                coreMessage.AttachmentHeight = firstImageAttachment.Height ?? 0;                
            }


            if (!Enum.TryParse(message.MessageType, true, out coreMessage.MessageType))
            {
                coreMessage.MessageType = DbMessageType.Unknown;
            }

            Messages.InsertRecord(coreMessage, SQLiteConflictResolution.Replace);

            DbMessageState messageState = new DbMessageState() { MessageId = archiveMessage.Id };
            MessageStates.InsertRecord(messageState, SQLiteConflictResolution.Ignore);

            // Ensure that every message has a corresponding DbThreadState for its thread
            DbThreadState threadState = new DbThreadState() { ThreadId = coreMessage.ThreadId };
            ThreadStates.InsertRecord(threadState, SQLiteConflictResolution.Ignore);
        }

        internal void UpdateProperties(Action<DbPropertiesRow> update)
        {
            var record = this.DbProperties.QueryAll().First();
            update(record);
            record.Id = 0;
            this.DbProperties.InsertRecord(record, SQLiteConflictResolution.Replace);
        }

        public void SetSyncInbox(bool syncInbox)
        {
            this.UpdateProperties(properties => {
                properties.SyncInbox = syncInbox;
            });
        }

        void UpdateUser(DbArchiveRecord archiveUser)
        {
            JsonUserReference userRef = SQLiteJsonConverter.LoadFromJson<JsonUserReference>(archiveUser.Json);

            DbUser coreUser = new DbUser();
            coreUser.LastFetchedUtc = archiveUser.LastFetchedUtc;
            coreUser.UserId = userRef.Id;
            coreUser.Alias = userRef.Alias ?? "";
            coreUser.Email = userRef.Email ?? "";
            coreUser.FullName = userRef.DisplayValue ?? "";
            coreUser.JobTitle = userRef.JobTitle ?? "";
            coreUser.WebUrl = userRef.Permalink ?? "";
            coreUser.MugshotUrl = userRef.MugshotUrl ?? "";

            Users.InsertRecord(coreUser, SQLiteConflictResolution.Replace);
        }

        public JsonSyncingFeed GetJsonSyncingFeed(long feedId)
        {
            var row = SyncingFeeds.Query("WHERE FeedId = " + feedId).FirstOrDefault();
            if (row == null)
                return null;

            var state = SQLiteJsonConverter.LoadFromJson<JsonSyncingFeed>(row.Json);
            return state;
        }

        public void UpdateJsonSyncingFeed(long groupId, JsonSyncingFeed state)
        {
            DbSyncingFeed row = new DbSyncingFeed();
            row.FeedId = groupId;

            // Update the aggregated properties
            row.LastUpdateUtc = state.LastUpdateUtc;
            row.LastCheckNewUtc = state.LastCheckNewUtc;
            row.ReachedEmptyResult = state.ReachedEmptyResult;
            row.HasSpanGaps = state.HasSpanGaps;

            row.Json = SQLiteJsonConverter.SaveToJson(state);
            SyncingFeeds.InsertRecord(row, SQLiteConflictResolution.Replace);
        }

    }
}
