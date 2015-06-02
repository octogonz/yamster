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
using Yamster.Core.SQLite;

namespace Yamster.Core
{
    #region Archive Schema

    public class DbVersion
    {
        [SQLiteMapperProperty(PrimaryKey = true, Nullable = OptionalBool.False)]
        public string ObjectName = "";

        // The version number of the database schema
        [SQLiteMapperProperty]
        public int Version;
    }

    [MappedTableIndex("LastFetchedUtc")]
    public class DbArchiveRecord
    {
        [SQLiteMapperProperty(PrimaryKey = true)]
        public long Id;

        [SQLiteMapperProperty]
        public DateTime LastFetchedUtc;

        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string Json = "";
    }

    // Normally the archive tables keep all their data in the serialized JSON,
    // which minimizes the need for any schema changes (which would require an
    // upgrader to preserve all the data that was downloaded).  We make a special
    // exception by adding a ThreadId column for archive messages, since this
    // prevents the message puller from needing dependencies on anything other
    // than the archive tables.
    [MappedTableIndex("ThreadId", "Id", Unique=true)]
    public class DbArchiveMessageRecord : DbArchiveRecord
    {
        // The REST /api/v1/messages/in_group sometimes returns system messages
        // with a null GroupId, so we use this property to force them into the group
        // they were fetched from.
        // TODO: Can the same message end up in multiple groups?
        [SQLiteMapperProperty]
        public long GroupId;

        [SQLiteMapperProperty]
        public long ThreadId;
    }

    #endregion

    #region Core Schema

    // This is a single-row table that stories global properties for the database
    public class DbPropertiesRow
    {
        [SQLiteMapperProperty(PrimaryKey = true)]
        public long Id = 0;

        [SQLiteMapperProperty]
        public long CurrentUserId = 0;

        [SQLiteMapperProperty]
        public long CurrentNetworkId = 0;

        // TODO: This is a hack.  A better design would be to move 
        // DbGroupState.ShouldSync into DbSyncingFeed so that groups and virtual feeds
        // can be managed in uniform way, however this will require redesigning
        // GroupConfigScreen to get DbGroupState.TrackRead via a join.
        [SQLiteMapperProperty]
        public bool SyncInbox = false;
    }

    public abstract class DbModel : MappedRecordWithChangeTracking
    {
        [SQLiteMapperProperty]
        public DateTime LastFetchedUtc;
    }

    public enum DbGroupPrivacy
    {
        Unknown = 0, // i.e. not downloaded yet
        Public,
        Private,
        Restricted
    }

    public class DbGroup : DbModel
    {
        [SQLiteMapperProperty(PrimaryKey = true)]
        public long GroupId;

        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string GroupName = "";

        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string GroupDescription = "";

        [SQLiteMapperProperty]
        public DbGroupPrivacy Privacy;

        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string WebUrl = "";

        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string MugshotUrl = "";
    }

    public class DbGroupState : MappedRecordWithChangeTracking
    {
        [SQLiteMapperProperty(PrimaryKey = true)]
        public long GroupId;

        /// <summary>
        /// Whether the user wants to see this group in the Yamster group list.
        /// If False, then the DbGroup record may still be used for resolving
        /// references.
        /// </summary>
        [SQLiteMapperProperty]
        public bool ShowInYamster = false;

        /// <summary>
        /// Whether MessagePuller should sync this group.
        /// </summary>
        [SQLiteMapperProperty]
        public bool ShouldSync = false;

        /// <summary>
        /// Whether the UI should highlight read/unread states for this group.
        /// </summary>
        [SQLiteMapperProperty]
        public bool TrackRead = true;

        public void CopyFrom(DbGroupState source)
        {
            base.CopyFrom(source);
            this.GroupId = source.GroupId;
            this.ShowInYamster = source.ShowInYamster;
            this.ShouldSync = source.ShouldSync;
            this.TrackRead = source.TrackRead;
        }
    }

    public class DbThreadState : MappedRecordWithChangeTracking
    {
        [SQLiteMapperProperty(PrimaryKey = true)]
        public long ThreadId;

        [SQLiteMapperProperty]
        public bool SeenInInboxFeed = false;
    }

    public class DbConversation : DbModel
    {
        [SQLiteMapperProperty(PrimaryKey = true)]
        public long ConversationId;

        // This is an array of UserId's who were added to the private conversation.
        [SQLiteMapperProperty]
        public readonly SQLiteIdList ParticipantUserIds = new SQLiteIdList();
    }

    public enum DbMessageType
    {
        Unknown=0,
        Update,
        System,
        Announcement,
        Chat
    }

    [MappedTableIndex("GroupId", "ThreadId", "MessageId", Unique = true)]
    [MappedTableIndex("ThreadId", "MessageId", Unique = true)]
    [MappedTableIndex("SenderUserId")]
    public class DbMessage : DbModel
    {
        [SQLiteMapperProperty(PrimaryKey = true)]
        public long MessageId;

        [SQLiteMapperProperty]
        public long GroupId;  // denormalized

        [SQLiteMapperProperty]
        public long ThreadId;

        [SQLiteMapperProperty]
        public long ConversationId;

        [SQLiteMapperProperty]
        public DateTime CreatedDate;

        [SQLiteMapperProperty]
        public long SenderUserId;

        [SQLiteMapperProperty]
        public long MessageIdRepliedTo;

        /// <summary>
        /// Note: LikingUserIds normally returns only the first 3 likes, which
        /// Yammer displays as e.g. "A, B, C, and 3 others like this" (i.e. a separate
        /// service call is needed to fetch the others).  The LikesCount property 
        /// always reports the true total number of likes.
        /// </summary>
        // Same comment on YamsterMessage.LikingUsers
        [SQLiteMapperProperty]
        public readonly SQLiteIdList LikingUserIds = new SQLiteIdList();

        [SQLiteMapperProperty]
        public int LikesCount;

        [SQLiteMapperProperty]
        public readonly SQLiteIdList NotifiedUserIds = new SQLiteIdList();

        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string Body = "";

        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string WebUrl = "";

        // TODO: This is a temporary hack that allows us to show the first image 
        // attachment, which is the most interesting usage scenario.  The 
        // full spec requires supporting multiple attachments with several different
        // schemas.
        // Ex. "Sunset.jpg"
        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string AttachmentFilename = "";

        // URL for Yammer page about the image itself
        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string AttachmentWebUrl = "";

        // Ex. "https://www.yammer.com/example.com/api/v1/uploaded_files/12345/version/23456/scaled/{{width}}x{{height}}"
        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string AttachmentScaledUrlTemplate = "";

        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public int AttachmentWidth;

        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public int AttachmentHeight;

        [SQLiteMapperProperty]
        public DbMessageType MessageType = DbMessageType.Unknown;
    }

    public class DbMessageState : MappedRecordWithChangeTracking
    {
        [SQLiteMapperProperty(PrimaryKey = true)]
        public long MessageId;

        [SQLiteMapperProperty]
        public bool Read = false;

        [SQLiteMapperProperty]
        public bool Starred = false;

        // Specify a default value because the upgrader can't add a column without
        // providing a default value.
        [SQLiteMapperProperty(SqlDefaultValue = false)]
        public bool Deleted = false;

        public void CopyFrom(DbMessageState source)
        {
            base.CopyFrom(source);
            this.MessageId = source.MessageId;
            this.Read = source.Read;
            this.Starred = source.Starred;
            this.Deleted = source.Deleted;
        }
    }

    public class DbUser : DbModel
    {
        [SQLiteMapperProperty(PrimaryKey = true)]
        public long UserId;

        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string Alias = "";

        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string Email = "";

        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string FullName = "";

        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string JobTitle = "";

        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string WebUrl = "";

        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string MugshotUrl = "";
    }

    #endregion

    #region Query Helpers

    class DbInt64Result
    {
        [SQLiteMapperProperty]
        public long Value = 0;
    }

    #endregion

}
