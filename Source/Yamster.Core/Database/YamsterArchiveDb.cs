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
using Yamster.Core.SQLite;

namespace Yamster.Core
{
    public class ArchiveInsertEventArgs<T> : EventArgs
    {
        public T Record { get; private set; }

        public ArchiveInsertEventArgs(T record)
        {
            this.Record = record;
        }
    }

    public class UnsupportedDatabaseVersionException : Exception
    {
        public string DatabaseName { get; private set; }
        public int DatabaseVersion { get; private set; }
        public int LatestVersion { get; private set; }

        public UnsupportedDatabaseVersionException(string databaseName, int databaseVersion, 
            int latestVersion)
        {
            this.DatabaseName = databaseName;
            this.DatabaseVersion = databaseVersion;
            this.LatestVersion = latestVersion;
        }

        public override string Message
        {
            get
            {
                if (DatabaseVersion > LatestVersion)
                {
                    return string.Format("Unable to connect to the {0} database (version {1})."
                        + " It appears to have been created by a newer release of the application."
                        + " You may need to upgrade your application.",
                        DatabaseName, DatabaseVersion);
                }
                else 
                {
                    return string.Format("Unable to connect to the {0} database (version {1})."
                        + " It is using an old version that cannot be upgraded."
                        + " You may need to delete and recreate your database file.",
                        DatabaseName, DatabaseVersion);
                }
            }
        }
        
    }

    public class DatabaseUpgradeCanceledException : Exception
    {
        public string DatabaseName { get; private set; }
        public DatabaseUpgradeCanceledException(string databaseName)
        {
            this.DatabaseName = databaseName;
        }

        public override string Message
        {
            get
            {
                return "The " + DatabaseName + " database could not be loaded because the upgrade was canceled.";
            }
        }
    }

    public class YamsterArchiveDb : SQLiteDataContext
    {
        const int CurrentArchiveDbVersion = 1003;

        public event EventHandler<ArchiveInsertEventArgs<DbArchiveMessageRecord>> MessageInserted;
        public event EventHandler<ArchiveInsertEventArgs<DbArchiveRecord>> GroupRefInserted;
        public event EventHandler<ArchiveInsertEventArgs<DbArchiveRecord>> UserRefInserted;
        public event EventHandler<ArchiveInsertEventArgs<DbArchiveRecord>> ConversationRefInserted;

        [SQLiteMapperTable]
        public MappedTable<DbVersion> Versions;

        [SQLiteMapperTable]
        public MappedTable<DbArchiveMessageRecord> ArchiveMessages;
        [SQLiteMapperTable]
        public MappedTable<DbArchiveRecord> ArchiveUserRefs;
        [SQLiteMapperTable]
        public MappedTable<DbArchiveRecord> ArchiveGroupRefs;
        [SQLiteMapperTable]
        public MappedTable<DbArchiveRecord> ArchiveConversationRefs;
        [SQLiteMapperTable]
        public MappedTable<DbArchiveRecord> ArchiveThreadRefs;
        [SQLiteMapperTable]
        public MappedTable<DbArchiveRecord> ArchivePageRefs;
        [SQLiteMapperTable]
        public MappedTable<DbArchiveRecord> ArchiveMessageRefs;
        [SQLiteMapperTable]
        public MappedTable<DbArchiveRecord> ArchiveTopicRefs;

        public YamsterArchiveDb(SQLiteMapper mapper, 
            EventHandler<SQLiteDataContextUpgradeEventArgs> beforeUpgradeHandler,
            EventHandler afterUpgradeHandler)
            : base(mapper, beforeUpgradeHandler, afterUpgradeHandler)
        {
            // Validate the database state
            int archiveDbVersion = GetObjectVersion("Archive");

            if (archiveDbVersion != CurrentArchiveDbVersion)
            {
                if (archiveDbVersion == 0)
                {
                    Debug.WriteLine("Uninitialized database -- creating tables");
                }
                else if (archiveDbVersion == -1)
                {
                    Debug.WriteLine("ArchiveDb is inactive -- skipping initialization");
                    return;
                }
                else
                {
                    throw new UnsupportedDatabaseVersionException("Archive", archiveDbVersion, CurrentArchiveDbVersion);
                }

                CreateTables();
                InitDatabase(markInactive: false);
            }
        }

        void InitDatabase(bool markInactive)
        {
            // TODO: Delete the old contents first
            using (var transaction = BeginTransaction())
            {
                if (markInactive)
                {
                    SetObjectVersion("Archive", -1);
                }
                else
                {
                    SetObjectVersion("Archive", CurrentArchiveDbVersion);
                }

                transaction.Commit();
            }
        }

        public int GetObjectVersion(string objectName)
        {
            if (!Versions.DoesTableExist())
                return 0;
            var row = Versions.Query("WHERE ObjectName = '" + objectName + "'").FirstOrDefault();
            if (row == null)
                return 0;
            return row.Version;
        }

        public void SetObjectVersion(string objectName, int version)
        {
            Versions.InsertRecord(new DbVersion() { ObjectName = objectName, Version = version },
                SQLiteConflictResolution.Replace);
        }

        /// <summary>
        /// If this returns "true", then the ArchiveDb is empty and cannot be used to
        /// regenerate the CoreDb.
        /// </summary>
        public bool IsInactive()
        {
            return GetObjectVersion("Archive") == -1;
        }

        public void InsertArchiveMessage(JsonMessage message, DateTime lastFetchedUtc)
        {
            long groupId;
            if (message.IsDirectMessage)
            {
                groupId = YamsterGroup.ConversationsGroupId;
            }
            else
            {
                groupId = message.GroupId ?? YamsterGroup.AllCompanyGroupId;
            }

            DbArchiveMessageRecord archiveRecord = new DbArchiveMessageRecord();
            archiveRecord.Id = message.Id;
            archiveRecord.ThreadId = message.ThreadId;
            archiveRecord.GroupId = groupId;
            archiveRecord.LastFetchedUtc = lastFetchedUtc;
            archiveRecord.Json = message.RawJson;
            ArchiveMessages.InsertRecord(archiveRecord, SQLiteConflictResolution.Replace);

            if (MessageInserted != null)
                MessageInserted(this, new ArchiveInsertEventArgs<DbArchiveMessageRecord>(archiveRecord));
        }

        public void InsertReference(IReferenceJson reference, DateTime lastFetchedUtc)
        {
            // Based on ReferenceJsonConverter.typeMap

            if (reference.GetType() == typeof(JsonUserReference))
            {
                var archiveRecord = InsertReference(this.ArchiveUserRefs, reference, lastFetchedUtc);

                if (UserRefInserted != null)
                    UserRefInserted(this, new ArchiveInsertEventArgs<DbArchiveRecord>(archiveRecord));
            }
            else if (reference.GetType() == typeof(JsonGroupReference))
            {
                var archiveRecord = InsertReference(this.ArchiveGroupRefs, reference, lastFetchedUtc);

                if (GroupRefInserted != null)
                    GroupRefInserted(this, new ArchiveInsertEventArgs<DbArchiveRecord>(archiveRecord));
            }
            else if (reference.GetType() == typeof(JsonConversationReference))
            {
                var archiveRecord = InsertReference(this.ArchiveConversationRefs, reference, lastFetchedUtc);
                if (ConversationRefInserted != null)
                    ConversationRefInserted(this, new ArchiveInsertEventArgs<DbArchiveRecord>(archiveRecord));
            }
            else if (reference.GetType() == typeof(ThreadReferenceJson))
                InsertReference(this.ArchiveThreadRefs, reference, lastFetchedUtc);
            else if (reference.GetType() == typeof(JsonPageReference))
                InsertReference(this.ArchivePageRefs, reference, lastFetchedUtc);
            else if (reference.GetType() == typeof(JsonMessageReference))
                InsertReference(this.ArchiveMessageRefs, reference, lastFetchedUtc);
            else if (reference.GetType() == typeof(JsonTopicReference))
                InsertReference(this.ArchiveTopicRefs, reference, lastFetchedUtc);
            else
            {
                Debug.WriteLine("WARNING: Unknown reference type {0}", reference.GetType().Name);
            }
        }

        DbArchiveRecord InsertReference(MappedTable<DbArchiveRecord> table, IReferenceJson reference, DateTime lastFetchedUtc)
        {
            DbArchiveRecord archiveRecord = new DbArchiveRecord();
            archiveRecord.Id = reference.Id;
            archiveRecord.LastFetchedUtc = lastFetchedUtc;
            archiveRecord.Json = reference.RawJson;
            Mapper.InsertRecord(table, archiveRecord, SQLiteConflictResolution.Replace);
            return archiveRecord;
        }

        internal void DeleteEverything(bool markInactive)
        {
            using (var transaction = this.BeginTransaction())
            {
                this.Versions.DeleteAllRecords();
                this.ArchiveMessages.DeleteAllRecords();
                this.ArchiveUserRefs.DeleteAllRecords();
                this.ArchiveGroupRefs.DeleteAllRecords();
                this.ArchiveConversationRefs.DeleteAllRecords();
                this.ArchiveThreadRefs.DeleteAllRecords();
                this.ArchivePageRefs.DeleteAllRecords();
                this.ArchiveMessageRefs.DeleteAllRecords();
                this.ArchiveTopicRefs.DeleteAllRecords();

                this.InitDatabase(markInactive);

                transaction.Commit();
            }
        }
    }
}
