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
    public class YamsterGroup : YamsterModel
    {
        // This is a fake Yammer group ID used internally to represent the current user's
        // private message feed.
        public const int ConversationsGroupId = -1;
        public const int AllCompanyGroupId = -2;
        public const int InboxFeedId = -3;

        readonly long groupId;
        DbGroup dbGroup;
        DbGroupState dbGroupState;

        readonly List<YamsterThread> threadsInternal = new List<YamsterThread>();
        readonly List<YamsterThread> deletedThreadsInternal = new List<YamsterThread>();
        int readThreadCount = 0;

        internal YamsterGroup(long groupId, YamsterCache yamsterCache)
            : base(yamsterCache)
        {
            this.groupId = groupId;
            
            this.dbGroup = new DbGroup() 
            { 
                GroupId = groupId, 
                GroupName = "(Group #" + groupId + ")",
                ChangeNumber = 0
            };
            this.dbGroupState = new DbGroupState()
            {
                GroupId = groupId,
                ChangeNumber = 0
            };
        }

        public override YamsterModelType ModelType { get { return YamsterModelType.Group; } }

        public ReadOnlyCollection<YamsterThread> Threads
        {
            get
            {
                return new ReadOnlyCollection<YamsterThread>(
                    this.threadsInternal
                );
            }
        }

        public ReadOnlyCollection<YamsterThread> DeletedThreads
        {
            get
            {
                return new ReadOnlyCollection<YamsterThread>(
                    this.deletedThreadsInternal
                );
            }
        }

        public bool Read
        {
            get
            {
                return readThreadCount == threadsInternal.Count;
            }
        }

        public int UnreadThreadCount {
            get {
                return this.threadsInternal.Count - readThreadCount;
            }
        }

        internal DbGroup DbGroup
        {
            get { return this.dbGroup; }
        }
        internal DbGroupState DbGroupState
        {
            get { return this.dbGroupState; }
        }

        public long GroupId { get { return DbGroup.GroupId; } }

        #region DbGroup Properties

        public string GroupName { get { return DbGroup.GroupName; } }
        public string GroupDescription { get { return DbGroup.GroupDescription; } }
        public DbGroupPrivacy Privacy { get { return DbGroup.Privacy; } }
        public string WebUrl { get { return DbGroup.WebUrl; } }
        public string MugshotUrl { get { return DbGroup.MugshotUrl; } }

        #endregion

        #region DbGroupState Properties

        public bool ShowInYamster
        {
            get { return this.DbGroupState.ShowInYamster; }
        }

        public bool ShouldSync
        {
            get { return this.DbGroupState.ShouldSync; }
            set
            {
                this.RequireLoaded();

                var yamsterCoreDb = this.YamsterCache.AppContext.YamsterCoreDb;

                yamsterCoreDb.Mapper.ExecuteNonQuery(@"
UPDATE " + yamsterCoreDb.GroupStates.TableName + @"
SET [ShouldSync] = ?
WHERE [GroupId] = ?
AND [ShouldSync] <> ?",
                    value,
                    this.groupId,
                    value
                );

                // This is correct, but not very elegant
                var newState = new DbGroupState();
                newState.CopyFrom(this.dbGroupState);
                newState.ShouldSync = value;

                var eventCollector = new YamsterModelEventCollector();
                this.SetDbGroupState(newState, eventCollector);
                eventCollector.FireEvents();
            }
        }

        public bool TrackRead
        {
            get { return this.DbGroupState.TrackRead; }
            set
            {
                this.RequireLoaded();

                var yamsterCoreDb = this.YamsterCache.AppContext.YamsterCoreDb;

                yamsterCoreDb.Mapper.ExecuteNonQuery(@"
UPDATE " + yamsterCoreDb.GroupStates.TableName + @"
SET [TrackRead] = ?
WHERE [GroupId] = ?
AND [TrackRead] <> ?",
                    value,
                    this.groupId,
                    value
                );

                // This is correct, but not very elegant
                var newState = new DbGroupState();
                newState.CopyFrom(this.dbGroupState);
                newState.TrackRead = value;

                var eventCollector = new YamsterModelEventCollector();
                this.SetDbGroupState(newState, eventCollector);
                eventCollector.FireEvents();
            }
        }

        #endregion

        #region Internal Methods

        protected override bool CheckIfLoaded() // abstract
        {
            return this.dbGroup.ChangeNumber != 0 && this.dbGroupState.ChangeNumber != 0;
        }

        internal override void FireChangeEvent(YamsterModelChangeType yamsterModelChangeType) // abstract
        {
            this.YamsterCache.FireChangedEvent(new YamsterGroupChangedEventArgs(this, yamsterModelChangeType));
        }

        internal void SetDbGroup(DbGroup newValue, YamsterModelEventCollector eventCollector)
        {
            if (newValue == null)
                throw new ArgumentNullException("DbGroup");
            if (newValue.GroupId != groupId)
                throw new ArgumentException("Cannot change ID");
            this.dbGroup = newValue;
            UpdateLoadedStatus();
            eventCollector.NotifyAfterUpdate(this);
        }

        internal void SetDbGroupState(DbGroupState newValue, YamsterModelEventCollector eventCollector)
        {
            if (newValue == null)
                throw new ArgumentNullException("DbGroupState");
            if (newValue.GroupId != groupId)
                throw new ArgumentException("Cannot change ID");
            this.dbGroupState = newValue;
            UpdateLoadedStatus();
            eventCollector.NotifyAfterUpdate(this);
        }

        internal void AddThread(YamsterThread thread, YamsterModelEventCollector eventCollector)
        {
            int index = this.threadsInternal.BinarySearch(thread,
                Comparer<YamsterThread>.Create((x, y) => Math.Sign(x.ThreadId - y.ThreadId)));
            int deletedIndex = this.deletedThreadsInternal.BinarySearch(thread,
                Comparer<YamsterThread>.Create((x, y) => Math.Sign(x.ThreadId - y.ThreadId)));

            if (index >= 0 || deletedIndex >= 0)
            {
                throw new InvalidOperationException("Program Bug: The message was already added to this thread");
            }

            if (!thread.AllMessagesDeleted)
            {
                this.threadsInternal.Insert(~index, thread);

                if (thread.Read)
                {
                    this.NotifyThreadReadChanged(newReadValue: true, eventCollector: eventCollector);
                }
            }
            else
            {
                this.deletedThreadsInternal.Insert(~deletedIndex, thread);
            }
        }

        internal void RemoveThread(YamsterThread thread, YamsterModelEventCollector eventCollector)
        {
            int index = this.threadsInternal.BinarySearch(thread,
                Comparer<YamsterThread>.Create((x, y) => Math.Sign(x.ThreadId - y.ThreadId)));
            int deletedIndex = this.deletedThreadsInternal.BinarySearch(thread,
                Comparer<YamsterThread>.Create((x, y) => Math.Sign(x.ThreadId - y.ThreadId)));

            if (index < 0 && deletedIndex < 0)
            {
                Debug.Assert(false, "RemoveThread() called on thread that doesn't belong to this group");
                return;
            }

            if (index >= 0)
            {
                this.threadsInternal.RemoveAt(index);

                if (thread.Read)
                {
                    this.NotifyThreadReadChanged(newReadValue: false, eventCollector: eventCollector);
                }
            }
            else
            {
                this.deletedThreadsInternal.RemoveAt(deletedIndex);
            }
        }
        
        internal void NotifyThreadReadChanged(bool newReadValue, YamsterModelEventCollector eventCollector)
        {
            bool oldGroupRead = this.Read;

            if (newReadValue)
                ++readThreadCount;
            else
                --readThreadCount;

            Debug.Assert(readThreadCount >= 0 && readThreadCount <= threadsInternal.Count);

            eventCollector.NotifyAfterUpdate(this);
        }

        #endregion

        public ReadOnlyCollection<YamsterThread> GetThreadsSortedByUpdate()
        {
            return new ReadOnlyCollection<YamsterThread>(
                this.threadsInternal
                .OrderByDescending(x => x.LastUpdate)
                .ToArray()
            );
        }

        public void SetReadStatusForAllMessages(bool read)
        {
            this.RequireLoaded();

            var yamsterCoreDb = this.YamsterCache.AppContext.YamsterCoreDb;

            yamsterCoreDb.Mapper.ExecuteNonQuery(@"
UPDATE " + yamsterCoreDb.MessageStates.TableName  + @"
SET [Read] = ?
WHERE [MessageId] IN
(
    SELECT [MessageId] FROM " + yamsterCoreDb.Messages.TableName + @" WHERE [GroupId] = ?
)
AND [Read] <> ?
", 
                read,
                this.GroupId,
                read
            );
        }

        public override string ToString()
        {
            return GetNotLoadedPrefix() + "Group #" + this.GroupId + " \"" + this.GroupName + "\"";
        }
    }

    public class YamsterGroupChangedEventArgs : ModelChangedEventArgs
    {
        public YamsterGroup Group { get; private set; }

        public YamsterGroupChangedEventArgs(YamsterGroup group, YamsterModelChangeType changeType)
            : base(changeType)
        {
            this.Group = group;
        }

        public override YamsterModel Model
        {
            get { return this.Group; }
        }

        public override string ToString()
        {
            return string.Format("Group {0}: ID={1} Name={1}", this.ChangeType, this.Group.GroupId,
                this.Group.GroupName);
        }
    }

}
