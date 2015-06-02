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
using Newtonsoft.Json;
using Yamster.Core.SQLite;

namespace Yamster.Core
{
    public partial class YamsterCoreDb
    {
        const int MinimumUpgradeableCoreDbVersion = 1004;
        const int CurrentCoreDbVersion = 1019;

        [Flags]
        enum RebuildableTable
        {
            None            = 0,
            // Implies Groups and GroupStates, but does not recreate GroupStates
            // since that would discard user data
            Groups          = (1 << 0),
            Conversations   = (1 << 1),
            // Implies Messages and MessageStates, but does not recreate MessageStates
            // since that would discard user data
            Messages        = (1 << 2),
            Users           = (1 << 3),
            Properties      = (1 << 4),
            SyncingFeeds    = (1 << 5),
            SyncingThreads  = (1 << 6),

            All = Groups|Conversations|Messages|Users|Properties|SyncingFeeds|SyncingThreads
        }

        void UpgradeDatabase()
        {
            // Validate the database state
            int coreDbVersion = archiveDb.GetObjectVersion("Core");

            if (coreDbVersion == CurrentCoreDbVersion)
                return;

            if (coreDbVersion > CurrentCoreDbVersion)
            {
                throw new UnsupportedDatabaseVersionException("Core", coreDbVersion, CurrentCoreDbVersion);
            }

            if (coreDbVersion < MinimumUpgradeableCoreDbVersion)
            {
                RebuildDatabase(coreDbVersion);
                return;
            }

            var args2 = new SQLiteDataContextUpgradeEventArgs("Core",
                "For a large database, the upgrade may take a while.  Please be patient.",
                coreDbVersion, CurrentCoreDbVersion);
            OnBeforeUpgrade(args2);
            if (args2.CancelUpgrade)
                throw new DatabaseUpgradeCanceledException("Core");

            var startTime = DateTime.Now;
            Debug.WriteLine("BEGIN UPGRADE CORE DATABASE");

            RebuildableTable alreadyRebuiltTables = RebuildableTable.None;

            // Upgrade 1004 -> 1005
            if (coreDbVersion < 1005)
            {
                Debug.Assert(coreDbVersion == 1004);

                using (var transaction = this.BeginTransaction())
                {
                    string sqlTemplate = @"
ALTER TABLE [{0}]
ADD COLUMN [ChangeNumber] INTEGER NOT NULL DEFAULT -1;

-- Ensure ChangeNumber is unique
UPDATE [{0}]
SET [ChangeNumber] = [RowId];

CREATE UNIQUE INDEX [{0}_Index0] ON [{0}] ([ChangeNumber]);

CREATE TRIGGER [{0}_ChangeNumberTrigger0] AFTER INSERT ON [{0}]
BEGIN
    UPDATE [{0}]
    SET [ChangeNumber] = (SELECT MAX([ChangeNumber]) FROM [{0}] LIMIT 1)+1
    WHERE ROWID = NEW.ROWID;
END;

CREATE TRIGGER [{0}_ChangeNumberTrigger1] AFTER UPDATE ON [{0}]
BEGIN
    UPDATE [{0}]
    SET [ChangeNumber] = (SELECT MAX([ChangeNumber]) FROM [{0}] LIMIT 1)+1
    WHERE ROWID = NEW.ROWID;
END;
";
                    foreach (string tableName in new string[] { "GroupStates", "MessageStates" })
                    {
                        string sql = string.Format(sqlTemplate, tableName);
                        this.Mapper.ExecuteNonQuery(sql);
                    }

                    this.RebuildTablesIfNeeded(
                          RebuildableTable.Groups 
                        | RebuildableTable.Messages 
                        | RebuildableTable.Users, 
                        ref alreadyRebuiltTables
                    );

                    archiveDb.SetObjectVersion("Core", 1005);
                    transaction.Commit();
                    
                    coreDbVersion = 1005;
                }
            }
            // Upgrade 1005 -> 1006
            if (coreDbVersion < 1006)
            {
                Debug.Assert(coreDbVersion == 1005);

                using (var transaction = this.BeginTransaction())
                {
                    this.RebuildTablesIfNeeded(
                        // Added CurrentUserId column
                        RebuildableTable.Properties,
                        ref alreadyRebuiltTables
                    );

                    // Group name changed in this version
                    InitVirtualGroups();

                    archiveDb.SetObjectVersion("Core", 1006);
                    transaction.Commit();
                    coreDbVersion = 1006;
                }
            }

            // Upgrade 1006 -> 1007
            if (coreDbVersion < 1007)
            {
                using (var transaction = this.BeginTransaction())
                {
                    this.RebuildTablesIfNeeded(
                        // Added Conversations table
                        RebuildableTable.Conversations
                        // Added ConversationId column
                        | RebuildableTable.Messages,
                        ref alreadyRebuiltTables
                    );

                    archiveDb.SetObjectVersion("Core", 1007);
                    transaction.Commit();
                    coreDbVersion = 1007;
                }
            }

            // Upgrade 1007 -> 1008
            if (coreDbVersion < 1008)
            {
                using (var transaction = this.BeginTransaction())
                {
                    // Added "(All Company)" group
                    InitVirtualGroups();

                    GroupStates.InsertRecord(new DbGroupState() {
                        GroupId = YamsterGroup.AllCompanyGroupId,
                        ShowInYamster = true,
                        ShouldSync = false,
                        TrackRead = true
                    });

                    archiveDb.SetObjectVersion("Core", 1008);
                    transaction.Commit();
                    coreDbVersion = 1008;
                }
            }

            // Upgrade 1008 -> 1009
            if (coreDbVersion < 1009)
            {
                using (var transaction = this.BeginTransaction())
                {
                    this.RebuildTablesIfNeeded(
                        // Replaced DbConversation.ParticipantsJson with ParticipantUserIds
                          RebuildableTable.Conversations
                        // Added LikingUserIds and NotifiedUserIds
                        | RebuildableTable.Messages,
                        ref alreadyRebuiltTables
                    );

                    archiveDb.SetObjectVersion("Core", 1009);
                    transaction.Commit();
                    coreDbVersion = 1009;
                }
            }

            // Upgrade 1009 -> 1010
            if (coreDbVersion < 1010)
            {
                using (var transaction = this.BeginTransaction())
                {
                    this.RebuildTablesIfNeeded(
                        // Added CurrentNetworkId, FollowYamsterLastAskedUtc, and FollowYamsterState
                        RebuildableTable.Properties,
                        ref alreadyRebuiltTables
                    );

                    archiveDb.SetObjectVersion("Core", 1010);
                    transaction.Commit();
                    coreDbVersion = 1010;
                }
            }

            // Upgrade 1010 -> 1011
            if (coreDbVersion < 1011)
            {
                using (var transaction = this.BeginTransaction())
                {
                    this.RebuildTablesIfNeeded(
                        // Fixed issue where liking users weren't being extracted from the Messages
                        // table into the Users table
                        RebuildableTable.Messages,
                        ref alreadyRebuiltTables
                    );

                    archiveDb.SetObjectVersion("Core", 1011);
                    transaction.Commit();
                    coreDbVersion = 1011;
                }
            }

            // Upgrade 1011 -> 1012
            if (coreDbVersion < 1012)
            {
                using (var transaction = this.BeginTransaction())
                {
                    this.RebuildTablesIfNeeded(
                        // Added SyncInbox
                        RebuildableTable.Properties
                        // These tables are new
                        | RebuildableTable.SyncingFeeds | RebuildableTable.SyncingThreads,
                        ref alreadyRebuiltTables
                    );

                    // This could have been lost if the ArchiveDb was regenerated
                    if (this.Mapper.QueryScalar<int>(
                        "SELECT COUNT(*) FROM sqlite_master WHERE name ='ArchiveSyncState' and type='table'") > 0)
                    {
                        var syncStates = this.Mapper.Query<V1011_DbArchiveSyncState>(
                            "SELECT [GroupId], [Json] FROM [ArchiveSyncState]");

                        foreach (var syncState in syncStates)
                        {
                            var jsonGroupState = SQLiteJsonConverter.LoadFromJson<V1011_MessagePullerGroupState>(syncState.Json); 

                            // Build a DbSyncingFeed record
                            var syncingFeed = new JsonSyncingFeed()
                            {
                                ReachedEmptyResult = jsonGroupState.ReachedEmptyResult,
                                SpanCyclesSinceCheckNew = jsonGroupState.SpanCyclesSinceCheckNew,
                                LastUpdateUtc = jsonGroupState.LastUpdateUtc,
                                LastCheckNewUtc = jsonGroupState.LastCheckNewUtc
                            };
                            foreach (var span in jsonGroupState.Spans)
                            {
                                syncingFeed.AddSpan(new JsonMessagePullerSpan()
                                    {
                                        StartTimeUtc = span.StartTimeUtc,
                                        StartMessageId = span.StartMessageId,
                                        EndMessageId = span.EndMessageId
                                    }
                                );
                            }
                            this.UpdateJsonSyncingFeed(syncState.GroupId, syncingFeed);

                            // Build DbSyncingThread records
                            foreach (var gappedThread in jsonGroupState.GappedThreads)
                            {
                                var syncingThread = new DbSyncingThread()
                                {
                                    ThreadId = gappedThread.ThreadId,
                                    FeedId = syncState.GroupId,
                                    LastPulledMessageId = gappedThread.LastPulledMessageId,
                                    StopMessageId = gappedThread.StopMessageId,
                                    RetryCount = gappedThread.RetryCount
                                };
                                this.SyncingThreads.InsertRecord(syncingThread);
                            }
                        }

                        this.Mapper.ExecuteNonQuery("DROP TABLE [ArchiveSyncState]");
                    }

                    archiveDb.SetObjectVersion("Core", 1012);
                    transaction.Commit();
                    coreDbVersion = 1012;
                }
            }

            // Upgrade 1012 -> 1013
            if (coreDbVersion < 1013)
            {
                using (var transaction = this.BeginTransaction())
                {
                    this.Mapper.CreateTable(this.ThreadStates);

                    // Make sure we have DbThreadState records for each thread
                    foreach (var result in this.Mapper.Query<DbInt64Result>(
                        "SELECT DISTINCT [ThreadId] AS [Value] FROM [Messages]"))
                    {
                        this.ThreadStates.InsertRecord(new DbThreadState() { ThreadId = result.Value },
                            SQLiteConflictResolution.Ignore);
                    }

                    archiveDb.SetObjectVersion("Core", 1013);
                    transaction.Commit();
                    coreDbVersion = 1013;
                }
            }

            // Upgrade 1013 -> 1014
            if (coreDbVersion < 1014)
            {
                using (var transaction = this.BeginTransaction())
                {
                    this.RebuildTablesIfNeeded(
                        // Added DbMessage.MessageIdRepliedTo
                        RebuildableTable.Messages,
                        ref alreadyRebuiltTables
                    );

                    archiveDb.SetObjectVersion("Core", 1014);
                    transaction.Commit();
                    coreDbVersion = 1014;
                }
            }

            // Upgrade 1014 -> 1015
            if (coreDbVersion < 1015)
            {
                using (var transaction = this.BeginTransaction())
                {
                    this.RebuildTablesIfNeeded(
                        // Added DbMessage.AttachmentWidth/AttachmentHeight
                        RebuildableTable.Messages,
                        ref alreadyRebuiltTables
                    );

                    archiveDb.SetObjectVersion("Core", 1015);
                    transaction.Commit();
                    coreDbVersion = 1015;
                }
            }

            // Upgrade 1015 -> 1016
            if (coreDbVersion < 1016)
            {
                using (var transaction = this.BeginTransaction())
                {
                    this.RebuildTablesIfNeeded(
                        // Added DbUser.Alias
                        RebuildableTable.Users,
                        ref alreadyRebuiltTables
                    );

                    archiveDb.SetObjectVersion("Core", 1016);
                    transaction.Commit();
                    coreDbVersion = 1016;
                }
            }

            // Upgrade 1016 -> 1017
            if (coreDbVersion < 1017)
            {
                using (var transaction = this.BeginTransaction())
                {
                    this.RebuildTablesIfNeeded(
                        // Removed FollowYamsterLastAskedUtc and FollowYamsterState
                        RebuildableTable.Properties,
                        ref alreadyRebuiltTables
                    );

                    archiveDb.SetObjectVersion("Core", 1017);
                    transaction.Commit();
                    coreDbVersion = 1017;
                }
            }

            // Upgrade 1017 -> 1018
            if (coreDbVersion < 1018)
            {
                using (var transaction = this.BeginTransaction())
                {
                    this.Mapper.ExecuteNonQuery(@"ALTER TABLE [MessageStates] ADD COLUMN [Deleted] INTEGER NOT NULL DEFAULT 0");

                    this.RebuildTablesIfNeeded(
                        // Added DbMessage.MessageType
                        RebuildableTable.Messages,
                        ref alreadyRebuiltTables
                    );

                    archiveDb.SetObjectVersion("Core", 1018);
                    transaction.Commit();
                    coreDbVersion = 1018;
                }
            }

            // Upgrade 1018 -> 1019
            if (coreDbVersion < 1019)
            {
                using (var transaction = this.BeginTransaction())
                {
                    this.RebuildTablesIfNeeded(
                        // Added DbUser.Alias
                        RebuildableTable.Users,
                        ref alreadyRebuiltTables
                    );

                    archiveDb.SetObjectVersion("Core", 1019);
                    transaction.Commit();
                    coreDbVersion = 1019;
                }
            }

            if (coreDbVersion != CurrentCoreDbVersion)
            {
                // This is a program bug
                throw new InvalidOperationException("Upgrade failed");
            }

            var totalTime = DateTime.Now - startTime;
            Debug.WriteLine("END UPGRADE CORE DATABASE: {0} secs processing time", totalTime.TotalSeconds);

            OnAfterUpgrade();
        }

        #region V1011 Json structs
        #pragma warning disable 649 // 'X is never assigned to, and will always have its default value 0'
        class V1011_DbArchiveSyncState
        {
            [SQLiteMapperProperty(PrimaryKey = true)]
            public long GroupId;
            // ...
            [SQLiteMapperProperty(Nullable = OptionalBool.False)]
            public string Json;
        }

        [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        class V1011_MessagePullerSpan
        {
            [JsonProperty]
            public long StartMessageId;
            [JsonProperty]
            public long EndMessageId;
            [JsonProperty]
            public DateTime StartTimeUtc;
        }

        [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        class V1011_MessagePullerGappedThread
        {
            [JsonProperty]
            public long ThreadId;
            [JsonProperty]
            public long? LastPulledMessageId = null; 
            [JsonProperty]
            public long StopMessageId;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue(0)]
            public int RetryCount;
        }

        [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        class V1011_MessagePullerGroupState
        {
            [JsonProperty]
            public readonly List<V1011_MessagePullerSpan> Spans = new List<V1011_MessagePullerSpan>();
            [JsonProperty]
            public readonly List<V1011_MessagePullerGappedThread> GappedThreads = new List<V1011_MessagePullerGappedThread>();
            [JsonProperty]
            public bool ReachedEmptyResult;
            [JsonProperty]
            public long SpanCyclesSinceCheckNew = 0;
            [JsonProperty]
            public DateTime LastUpdateUtc = DateTime.MinValue;
            [JsonProperty]
            public DateTime LastCheckNewUtc = DateTime.MinValue;
        }
        #pragma warning restore 649
        #endregion

        private void RebuildDatabase(int coreDbVersion)
        {
            if (coreDbVersion != 0)
            {
                var args = new SQLiteDataContextUpgradeEventArgs("Core",
                    "Warning: Your database is from a beta build, so some settings will be lost during the upgrade.",
                    coreDbVersion, CurrentCoreDbVersion);
                OnBeforeUpgrade(args);
                if (args.CancelUpgrade)
                    throw new DatabaseUpgradeCanceledException("Core");
            }

            var startTime = DateTime.Now;
            Debug.WriteLine("BEGIN REBUILD CORE DATABASE");

            // Delete and recreate all the core tables
            InitDatabase();
            RebuildTables(RebuildableTable.All);

            // Set the ShowInYamster flag for any groups that were synced
            Mapper.ExecuteNonQuery(@"
UPDATE GroupStates
SET ShowInYamster = 1
WHERE GroupId IN ( SELECT GroupId FROM Messages GROUP BY GroupId )
");

            var totalTime = DateTime.Now - startTime;
            Debug.WriteLine("END REBUILD CORE DATABASE: {0} secs processing time", totalTime.TotalSeconds);

            if (coreDbVersion != 0)
            {
                OnAfterUpgrade();
            }

            return;
        }

        void InitDatabase()
        {
            using (var transaction = BeginTransaction())
            {
                CreateTables();

                // Insert the predefined rows
                DbProperties.InsertRecord(new DbPropertiesRow() { });

                InitVirtualGroups();

                GroupStates.InsertRecord(new DbGroupState() {
                    GroupId = YamsterGroup.AllCompanyGroupId,
                    ShowInYamster = true,
                    ShouldSync = false,
                    TrackRead = true
                });

                GroupStates.InsertRecord(new DbGroupState() {
                    GroupId = YamsterGroup.ConversationsGroupId,
                    ShowInYamster = true,
                    ShouldSync = true,
                    TrackRead = true
                });

                archiveDb.SetObjectVersion("Core", CurrentCoreDbVersion);

                transaction.Commit();
            }
        }

        void InitVirtualGroups()
        {
            Groups.InsertRecord(
                new DbGroup() {
                    GroupId = YamsterGroup.AllCompanyGroupId,
                    GroupName = "(All Company)",
                    GroupDescription = "This is the default group for everyone in the network.",
                    Privacy = DbGroupPrivacy.Public,
                    LastFetchedUtc = DateTime.UtcNow,
                    MugshotUrl = "",
                    WebUrl = "https://www.yammer.com/#/threads/company?type=general"
                },
                SQLiteConflictResolution.Replace
            );
            Groups.InsertRecord(
                new DbGroup() {
                    GroupId = YamsterGroup.ConversationsGroupId,
                    GroupName = "(Conversations)",
                    GroupDescription = "This is a virtual Yammer group that shows private conversations.",
                    Privacy = DbGroupPrivacy.Private,
                    LastFetchedUtc = DateTime.UtcNow,
                    MugshotUrl = "",
                    WebUrl = "https://www.yammer.com/#/inbox/index"
                },
                SQLiteConflictResolution.Replace
            );
        }

        void RebuildTablesIfNeeded(RebuildableTable tablesToRebuild, ref RebuildableTable alreadyRebuiltTables)
        {
            var flags = tablesToRebuild;

            if ((flags & RebuildableTable.Messages) != 0)
            {
                // UpdateMessage() writes to the Users table, so if we rebuild Messages then we need
                // to make sure the Users table has the latest schema
                flags |= RebuildableTable.Users;
            }

            flags &= ~alreadyRebuiltTables;

            if ((flags & RebuildableTable.Users) != 0)
            {
                // UpdateMessage() writes to the Users table, so if we rebuild Users then we also
                // need to rebuild the Messages table again (even if we did that before), to make sure
                // the extra rows get added
                flags |= RebuildableTable.Messages;
            }

            RebuildTables(flags);
            alreadyRebuiltTables |= flags;
        }

        void RebuildTables(RebuildableTable tablesToRebuild)
        {
            using (var transaction = this.BeginTransaction())
            {
                if ((tablesToRebuild & RebuildableTable.Groups) != 0)
                {
                    Debug.WriteLine("Regenerating group data from archive");
                    this.Mapper.CreateTable(this.Groups);
                    InitVirtualGroups();
                    // NOTE: We don't call CreateTable() for GroupStates
                    foreach (var archiveGroupRef in archiveDb.ArchiveGroupRefs.QueryAll())
                        UpdateGroup(archiveGroupRef);
                }

                if ((tablesToRebuild & RebuildableTable.Conversations) != 0)
                {
                    Debug.WriteLine("Regenerating conversation data from archive");
                    this.Mapper.CreateTable(this.Conversations);
                    foreach (var archiveConversationRef in archiveDb.ArchiveConversationRefs.QueryAll())
                        UpdateConversation(archiveConversationRef);
                }

                // It's important to do Users before Messages, since UpdateMessage() writes
                // to the Users table
                if ((tablesToRebuild & RebuildableTable.Users) != 0)
                {
                    Debug.WriteLine("Regenerating user data from archive");
                    this.Mapper.CreateTable(this.Users);
                    foreach (var archiveUser in archiveDb.ArchiveUserRefs.QueryAll())
                        UpdateUser(archiveUser);
                }

                if ((tablesToRebuild & RebuildableTable.Messages) != 0)
                {
                    Debug.WriteLine("Regenerating message data from archive");
                    this.Mapper.CreateTable(this.Messages);
                    // NOTE: We don't call CreateTable() for MessageStates
                    foreach (var archiveMessage in archiveDb.ArchiveMessages.QueryAll())
                        UpdateMessage(archiveMessage);
                }

                if ((tablesToRebuild & RebuildableTable.Properties) != 0)
                {
                    Debug.WriteLine("Regenerating DbProperties table");
                    this.Mapper.CreateTable(this.DbProperties);
                    DbProperties.InsertRecord(new DbPropertiesRow() { });
                }

                if ((tablesToRebuild & RebuildableTable.SyncingFeeds) != 0)
                {
                    Debug.WriteLine("Regenerating SyncingFeeds table");
                    this.Mapper.CreateTable(this.SyncingFeeds);
                }

                if ((tablesToRebuild & RebuildableTable.SyncingThreads) != 0)
                {
                    Debug.WriteLine("Regenerating SyncingThreads table");
                    this.Mapper.CreateTable(this.SyncingThreads);
                }

                transaction.Commit();
            }
        }

    }
}
