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
using System.Threading.Tasks;
using Yamster.Core.SQLite;

namespace Yamster.Core
{
    public class YamsterCache
    {
        public event EventHandler<YamsterGroupChangedEventArgs> GroupChanged;
        public event EventHandler<YamsterMessageChangedEventArgs> MessageChanged;
        public event EventHandler<YamsterThreadChangedEventArgs> ThreadChanged;
        public event EventHandler<YamsterUserChangedEventArgs> UserChanged;

        YamsterCoreDb yamsterCoreDb;

        Exception fatalException = null;

        readonly SortedDictionary<long, YamsterGroup> groupsById = new SortedDictionary<long, YamsterGroup>();

        readonly SortedDictionary<long, YamsterThread> threadsById = new SortedDictionary<long, YamsterThread>();
        readonly Dictionary<long, YamsterThread> threadsByConversationId = new Dictionary<long, YamsterThread>();

        readonly SortedDictionary<long, YamsterMessage> messagesById = new SortedDictionary<long, YamsterMessage>();

        readonly SortedDictionary<long, YamsterUser> usersById = new SortedDictionary<long, YamsterUser>();

        // NOTE: Generally YamsterCache prefers to eagerly construct and return partially initialized
        // models to avoid tracking "unresolved" data to be fixed up later.  This is reasonable assuming
        // the missing parts will show up soon.  However, DbMessageState and DbThreadState are
        // a special case because they store user settings that may be deadwood after a CoreDb rebuild.
        // So we track "unresolved" versions of them to avoid polluting the API output with deadwood objects.
        readonly Dictionary<long, DbThreadState> unresolvedThreadStatesById = new Dictionary<long, DbThreadState>();
        readonly Dictionary<long, DbConversation> unresolvedConversationsById = new Dictionary<long, DbConversation>();
        readonly Dictionary<long, DbMessageState> unresolvedMessageStatesById = new Dictionary<long, DbMessageState>();

        internal AppContext AppContext { get; private set; }

        bool discardChangeEvents = false;

        bool logEventsInDebugBuild = false;

        // The UserId for the current user, or 0 if MessagePuller has not determined it yet.
        // NOTE: Once assigned, this cannot change during the duration of the session.
        public long CurrentUserId { get; private set; }

        // The Yammer NetworkId for the current session, or 0 if MessagePuller has not determined it yet.
        // NOTE: Once assigned, this cannot change during the duration of the session.
        public long CurrentNetworkId { get; private set; }

        /// <summary>
        /// Returns the Yammer user alias that Yamster is using to log in.
        /// </summary>
        public string CurrentUserAlias
        {
            get
            {
                if (this.CurrentUserId == 0)
                    return "[Unknown]";

                YamsterUser user = this.GetUserById(this.CurrentUserId, nullIfMissing: true);
                if (user == null)
                {
                    return "[User #" + this.CurrentUserId + "]";
                }
                return user.Alias;
            }
        }

        /// <summary>
        /// Returns the URL for the currently connected Yammer network.
        /// Example: "https://www.yammer.com/example.com"
        /// </summary>
        public string CurrentNetworkUrl
        {
            get
            {
                YamsterUser serviceUser = this.GetUserByAlias("yammer", nullIfMissing: true);
                if (serviceUser == null)
                    return "";
                if (serviceUser.WebUrl.StartsWith("http"))
                    return serviceUser.WebUrl;
                return "";
            }
        }

        public YamsterCache(AppContext appContext)
        {
            this.AppContext = appContext;

            yamsterCoreDb = appContext.YamsterCoreDb;

            yamsterCoreDb.Groups.RecordChanged += Groups_RecordChanged;
            yamsterCoreDb.GroupStates.RecordChanged += GroupStates_RecordChanged;

            yamsterCoreDb.ThreadStates.RecordChanged += ThreadStates_RecordChanged;

            yamsterCoreDb.Conversations.RecordChanged += Conversations_RecordChanged;

            yamsterCoreDb.Messages.RecordChanged += Messages_RecordChanged;
            yamsterCoreDb.MessageStates.RecordChanged += MessageStates_RecordChanged;

            yamsterCoreDb.Users.RecordChanged += Users_RecordChanged;

            GLib.Timeout.Add(500, OnPollTimer);

            this.ReloadEverything();
        }

        #region YamsterCoreDb Event Handlers

        void GroupStates_RecordChanged(object sender, MappedRecordChangedEventArgs<DbGroupState> e)
        {
            ProcessDbGroupState(e.Record);
        }

        void Groups_RecordChanged(object sender, MappedRecordChangedEventArgs<DbGroup> e)
        {
            ProcessDbGroup(e.Record);
        }

        void ThreadStates_RecordChanged(object sender, MappedRecordChangedEventArgs<DbThreadState> e)
        {
            ProcessDbThreadState(e.Record);
        }

        void Conversations_RecordChanged(object sender, MappedRecordChangedEventArgs<DbConversation> e)
        {
            ProcessDbConversation(e.Record);
        }

        void MessageStates_RecordChanged(object sender, MappedRecordChangedEventArgs<DbMessageState> e)
        {
            ProcessDbMessageState(e.Record);
        }

        void Messages_RecordChanged(object sender, MappedRecordChangedEventArgs<DbMessage> e)
        {
            ProcessDbMessage(e.Record);
        }

        void Users_RecordChanged(object sender, MappedRecordChangedEventArgs<DbUser> e)
        {
            ProcessDbUser(e.Record);
        }

        #endregion

        #region Cache Population

        void ProcessDbThreadState(DbThreadState record)
        {
            // Does the thread exist yet?
            YamsterThread thread;
            if (threadsById.TryGetValue(record.ThreadId, out thread))
            {
                var eventCollector = new YamsterModelEventCollector();
                thread.SetDbThreadState(record,eventCollector);
                eventCollector.FireEvents();
            }
            else
            {
                // Stash the record and deal with it later
                unresolvedThreadStatesById[record.ThreadId] = record;
            }
        }

        void ProcessDbConversation(DbConversation record)
        {
            // Does the thread exist yet?
            YamsterThread thread;
            if (threadsByConversationId.TryGetValue(record.ConversationId, out thread))
            {
                var eventCollector = new YamsterModelEventCollector();
                thread.UpdateConversation(record, eventCollector);
                eventCollector.FireEvents();
            }
            else
            {
                // Stash the conversation and deal with it later
                unresolvedConversationsById[record.ConversationId] = record;
            }
        }

        internal void FixupUnresolvedObjectsForThread(YamsterThread thread, YamsterMessage latestMessage,
            YamsterModelEventCollector eventCollector)
        {
            Debug.Assert(latestMessage.Thread == thread);

            DbThreadState dbThreadState;
            if (unresolvedThreadStatesById.TryGetValue(thread.ThreadId, out dbThreadState))
            {
                unresolvedThreadStatesById.Remove(thread.ThreadId);
                thread.SetDbThreadState(dbThreadState, eventCollector);
            }

            // Is this thread part of a conversation?
            long conversationId = latestMessage.DbMessage.ConversationId;
            if (conversationId != 0)
            {
                // Register the thread as the owner of this conversation
                this.threadsByConversationId[conversationId] = thread;

                // Is there an unresolved conversation object?
                DbConversation unresolvedConversation;
                if (this.unresolvedConversationsById.TryGetValue(conversationId, out unresolvedConversation))
                {
                    // Yes, so apply it now
                    this.unresolvedConversationsById.Remove(conversationId);
                    thread.UpdateConversation(unresolvedConversation, eventCollector);
                }
            }
        }

        void ProcessDbGroup(DbGroup record)
        {
            var eventCollector = new YamsterModelEventCollector();
            YamsterGroup group = this.FetchGroupById(record.GroupId, eventCollector);
            group.SetDbGroup(record, eventCollector);
            eventCollector.FireEvents();
        }

        void ProcessDbGroupState(DbGroupState record)
        {
            var eventCollector = new YamsterModelEventCollector();
            YamsterGroup group = this.FetchGroupById(record.GroupId, eventCollector);
            group.SetDbGroupState(record, eventCollector);
            eventCollector.FireEvents();
        }

        void ProcessDbMessage(DbMessage record)
        {
            // Does the message exist yet?
            var message = this.GetMessageById(record.MessageId, nullIfMissing: true);

            bool messageIsNew = message == null;

            var eventCollector = new YamsterModelEventCollector();
            if (messageIsNew)
            {
                message = new YamsterMessage(record.MessageId, this);
                this.messagesById.Add(record.MessageId, message);
                eventCollector.NotifyAfterAdd(message);
                message.SetDbMessage(record, eventCollector);

                // For now we assume that messages cannot move between threads
                var threadId = message.ThreadId;
                YamsterThread thread = GetThreadById(threadId, nullIfMissing: true);
                if (thread == null)
                {
                    thread = new YamsterThread(threadId, message.Group, this);
                    threadsById.Add(threadId, thread);
                    eventCollector.NotifyAfterAdd(thread);
                    message.Group.AddThread(thread);

                    thread.AddMessage(message, eventCollector);

                    FixupUnresolvedObjectsForThread(thread, message, eventCollector);
                }
                else
                {
                    thread.AddMessage(message, eventCollector);
                }
            }
            else
            {
                message.SetDbMessage(record, eventCollector);
            }

            // Was there an unresolved message that we can process now?
            DbMessageState unresolvedMessageState;
            if (this.unresolvedMessageStatesById.TryGetValue(record.MessageId, out unresolvedMessageState))
            {
                this.unresolvedMessageStatesById.Remove(record.MessageId);
                ProcessDbMessageState(unresolvedMessageState, eventCollector);
            }

            if (messageIsNew)
                CheckForListenedMessage(message, eventCollector);

            eventCollector.FireEvents();
        }

        void ProcessDbMessageState(DbMessageState record, YamsterModelEventCollector eventCollector)
        {
            // Does the message exist yet?
            var message = this.GetMessageById(record.MessageId, nullIfMissing: true);
            if (message != null)
            {
                message.SetDbMessageState(record, eventCollector);
            }
            else
            {
                // Stash the message state and deal with it later
                this.unresolvedMessageStatesById[record.MessageId] = record;
            }
        }
        internal void ProcessDbMessageState(DbMessageState record)
        {
            var eventCollector = new YamsterModelEventCollector();
            ProcessDbMessageState(record, eventCollector);
            eventCollector.FireEvents();
        }

        void ProcessDbUser(DbUser record)
        {
            var eventCollector = new YamsterModelEventCollector();
            YamsterUser user = this.FetchUserById(record.UserId, eventCollector);
            user.DbUser = record;
            eventCollector.NotifyAfterUpdate(user);
            eventCollector.FireEvents();
        }

        void ReloadEverything()
        {
            try
            {
                discardChangeEvents = true;
                Debug.WriteLine("YamsterCache: START RELOADING CACHE");
                var startTime = DateTime.Now;

                TryUpdateSessionProperties();

                usersById.Clear();
                groupsById.Clear();
                threadsById.Clear();
        
                threadsByConversationId.Clear();
                unresolvedConversationsById.Clear();

                messagesById.Clear();
                unresolvedMessageStatesById.Clear();

#if true
                // Load the graph leaf-first to minimize unresolved references
                foreach (DbUser record in yamsterCoreDb.Users.QueryAll())
                    ProcessDbUser(record);
                foreach (DbGroup record in yamsterCoreDb.Groups.QueryAll())
                    ProcessDbGroup(record);
                foreach (DbGroupState record in yamsterCoreDb.GroupStates.QueryAll())
                    ProcessDbGroupState(record);

                foreach (DbMessage record in yamsterCoreDb.Messages.QueryAll())
                    ProcessDbMessage(record);
                foreach (DbMessageState record in yamsterCoreDb.MessageStates.QueryAll())
                    ProcessDbMessageState(record);

                foreach (DbThreadState record in yamsterCoreDb.ThreadStates.QueryAll())
                    ProcessDbThreadState(record);
                foreach (DbConversation record in yamsterCoreDb.Conversations.QueryAll())
                    ProcessDbConversation(record);

#else
                // Load the graph in the worst possible order to expose dependency bugs
                foreach (DbConversation record in yamsterCoreDb.Conversations.QueryAll())
                    ProcessDbConversation(record);
                foreach (DbThreadState record in yamsterCoreDb.ThreadStates.QueryAll())
                    ProcessDbThreadState(record);

                foreach (DbMessageState record in yamsterCoreDb.MessageStates.QueryAll())
                    ProcessDbMessageState(record);
                foreach (DbMessage record in yamsterCoreDb.Messages.QueryAll())
                    ProcessDbMessage(record);

                foreach (DbGroupState record in yamsterCoreDb.GroupStates.QueryAll())
                    ProcessDbGroupState(record);
                foreach (DbGroup record in yamsterCoreDb.Groups.QueryAll())
                    ProcessDbGroup(record);
                foreach (DbUser record in yamsterCoreDb.Users.QueryAll())
                    ProcessDbUser(record);
#endif
                var totalTime = DateTime.Now - startTime;
                Debug.WriteLine("YamsterCache: END RELOADING CACHE  (TotalTime = {0})", (object)totalTime.ToString());
            }
            finally
            {
                discardChangeEvents = false;
            }
        }

        void TryUpdateSessionProperties()
        {
            if (this.CurrentUserId == 0)
            {
                this.CurrentUserId = this.yamsterCoreDb.Properties.CurrentUserId;
            }
            if (this.CurrentNetworkId == 0)
            {
                this.CurrentNetworkId = this.yamsterCoreDb.Properties.CurrentNetworkId;
            }
        }

        #endregion

        #region Change Events

        internal void FireChangedEvent(YamsterGroupChangedEventArgs eventArgs)
        {
            if (discardChangeEvents)
                return;
            if (logEventsInDebugBuild)
                Debug.WriteLine("YamsterCache: " + eventArgs.ToString());

            if (GroupChanged != null)
                GroupChanged(this, eventArgs);
        }

        internal void FireChangedEvent(YamsterMessageChangedEventArgs eventArgs)
        {
            if (discardChangeEvents)
                return;
            if (logEventsInDebugBuild)
                Debug.WriteLine("YamsterCache: " + eventArgs.ToString());

            if (MessageChanged != null)
                MessageChanged(this, eventArgs);
        }

        internal void FireChangedEvent(YamsterThreadChangedEventArgs eventArgs)
        {
            if (discardChangeEvents)
                return;
            if (logEventsInDebugBuild)
                Debug.WriteLine("YamsterCache: " + eventArgs.ToString());

            if (ThreadChanged != null)
                ThreadChanged(this, eventArgs);
        }

        internal void FireChangedEvent(YamsterUserChangedEventArgs eventArgs)
        {
            if (discardChangeEvents)
                return;
            if (logEventsInDebugBuild)
                Debug.WriteLine("YamsterCache: " + eventArgs.ToString());

            if (UserChanged != null)
                UserChanged(this, eventArgs);
        }

        public bool SuspendEvents { get; set; }

        bool OnPollTimer()
        {
            if (fatalException != null)
                return false;

            try
            {
                if (!SuspendEvents)
                {
                    this.yamsterCoreDb.CheckForChanges();
                    TryUpdateSessionProperties();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Discarding exception in timer: " + ex.Message);
                fatalException = ex;
                return false;
            }
            return true;
        }

        #endregion

        #region Public API

        public IEnumerable<YamsterGroup> GetAllGroups()
        {
            return groupsById.Values
                    .Where(x => x.ShowInYamster)
                    .OrderBy(x => x.GroupName);
        }

        public IEnumerable<YamsterThread> GetAllThreads()
        {
            return threadsById.Values
                    .Where(x => x.Group.ShowInYamster);
        }

        public IEnumerable<YamsterMessage> GetAllMessages()
        {
            return messagesById.Values
                    .Where(x => x.Group.ShowInYamster);
        }

        public IEnumerable<YamsterUser> GetAllUsers()
        {
            return usersById.Values;
        }

        public YamsterGroup AddGroupToYamster(long groupId, JsonSearchedGroup searchedGroup = null)
        {
            DbGroup group = yamsterCoreDb.Groups.Query("WHERE GroupId = " + groupId)
                .FirstOrDefault();
            DbGroupState groupState = yamsterCoreDb.GroupStates.Query("WHERE GroupId = " + groupId)
                .FirstOrDefault();

            using (var transaction = yamsterCoreDb.BeginTransaction())
            {
                if (group == null)
                {
                    if (searchedGroup == null)
                    {
                        group = new DbGroup() {
                            GroupId = groupId,
                            GroupName = "(Unsynced Group #" + groupId + ")"
                        };
                    }
                    else
                    {
                        group = new DbGroup() {
                            GroupId = groupId,
                            GroupName = searchedGroup.FullName ?? "???",
                            GroupDescription = searchedGroup.Description ?? "",
                            WebUrl = searchedGroup.WebUrl ?? "",
                            MugshotUrl = searchedGroup.Photo ?? ""
                        };
                    }
                    yamsterCoreDb.Groups.InsertRecord(group);
                }

                if (groupState == null)
                {
                    groupState = new DbGroupState() {
                        GroupId = groupId,
                        ShowInYamster = true,
                        ShouldSync = true,
                        TrackRead = true
                    };
                }
                groupState.ShowInYamster = true;
                yamsterCoreDb.GroupStates.InsertRecord(groupState, SQLiteConflictResolution.Replace);

                transaction.Commit();
            }

            // Force an update
            this.ProcessDbGroup(group);
            this.ProcessDbGroupState(groupState);

            return this.GetGroupById(group.GroupId);
        }

        public async Task<YamsterMessage> PostMessageAsync(YamsterNewMessage messageToPost)
        {
            DateTime nowUtc = DateTime.UtcNow;
            var envelope = await this.AppContext.YamsterApi.PostMessageAsync(messageToPost);

            var message = envelope.Messages.Single();

            this.AppContext.MessagePuller.WriteManualEnvelopeToDb(envelope, messageToPost.Group.GroupId, nowUtc);
            
            var coreMessage = this.yamsterCoreDb.Messages.Query("WHERE [MessageId] = " + message.Id.ToString()).FirstOrDefault();
            if (coreMessage == null)
                return null;

            this.ProcessDbMessage(coreMessage);
            return this.GetMessageById(message.Id);
        }

        #endregion

        #region Model Getters

        public YamsterGroup GetGroupById(long groupId, bool nullIfMissing = false)
        {
            YamsterGroup group = null;
            if (!this.groupsById.TryGetValue(groupId, out group))
            {
                if (!nullIfMissing)
                    throw new KeyNotFoundException("The group ID " + groupId + " was not found in Yamster's local database. (Has it been synced yet?)");
            }
            return group;
        }

        internal YamsterGroup FetchGroupById(long groupId, YamsterModelEventCollector eventCollector)
        {
            YamsterGroup group = this.GetGroupById(groupId, nullIfMissing: true);
            if (group == null)
            {
                group = new YamsterGroup(groupId, this);
                this.groupsById.Add(groupId, group);
                eventCollector.NotifyAfterAdd(group);
            }
            return group;
        }

        public YamsterMessage GetMessageById(long messageId, bool nullIfMissing = false)
        {
            YamsterMessage message = null;
            if (!this.messagesById.TryGetValue(messageId, out message))
            {
                if (!nullIfMissing)
                    throw new KeyNotFoundException("The message ID " + messageId + " was not found");
            }
            return message;
        }

        public YamsterThread GetThreadById(long threadId, bool nullIfMissing = false)
        {
            YamsterThread thread = null;
            if (!this.threadsById.TryGetValue(threadId, out thread))
            {
                if (!nullIfMissing)
                    throw new KeyNotFoundException("The thread ID " + threadId + " was not found");
            }
            return thread;
        }

        public YamsterUser GetUserById(long userId, bool nullIfMissing = false)
        {
            YamsterUser user = null;
            if (!this.usersById.TryGetValue(userId, out user))
            {
                if (!nullIfMissing)
                    throw new KeyNotFoundException("The user ID " + userId + " was not found");
            }
            return user;
        }

        public YamsterUser GetUserByAlias(string alias, bool nullIfMissing = false)
        {
            if (alias == null)
                throw new ArgumentNullException("alias");
            string trimmedAlias = alias.Trim();
            
            if (trimmedAlias != "")
            {
                List<YamsterUser> matches = new List<YamsterUser>(1);

                // Ideally we should build and maintain a dictionary for this lookup, but currently
                // it is only used for very rare operations such as parsing the CC line when sending
                // a message.
                foreach (YamsterUser user in this.GetAllUsers())
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(user.Alias, alias))
                        matches.Add(user);
                }
                if (matches.Count == 1)
                    return matches[0];

                if (matches.Count > 1)
                {
                    // This is probably a program bug
                    throw new InvalidOperationException("The alias \"" + alias + "\" matched more than one user record");
                }
            }
            if (nullIfMissing)
                return null;
            throw new InvalidOperationException("The user alias \"" + alias + "\" was not found in the database");
        }

        // TODO: This is a hack.  Need a better story for referencing objects that haven't been
        // synced yet.
        public YamsterUser GetPossiblyUnknownUserById(long userId)
        {
            var eventCollector = new YamsterModelEventCollector();
            var user = FetchUserById(userId, eventCollector);
            eventCollector.FireEvents();
            return user;
        }

        internal YamsterUser FetchUserById(long userId, YamsterModelEventCollector eventCollector)
        {
            YamsterUser user = this.GetUserById(userId, nullIfMissing: true);
            if (user == null)
            {
                user = new YamsterUser(userId, this);
                this.usersById.Add(userId, user);
                eventCollector.NotifyAfterAdd(user);
            }
            return user;
        }

        #endregion

        #region ListenForMessageById()

        /// <summary>
        /// If the YamsterMessage with messageId already exists, this calls messageAction
        /// immediately.  Otherwise, messageAction is queued and will be called later when
        /// the message appears.  If the same listener calls ListenForMessageById() more
        /// than once, only the most recent request is kept.
        /// </summary>
        internal void ListenForMessageById(long messageId, 
            object listener,
            YamsterModelEventCollector eventCollector,
            ListenedMessageAction messageAction)
        {
            YamsterMessage message = this.GetMessageById(messageId, nullIfMissing: true);
            if (message != null)
            {
                messageAction(message, eventCollector);
                return;
            }

            ListenedMessage listenedMessage;
            if (!listenedMessagesById.TryGetValue(messageId, out listenedMessage))
            {
                listenedMessage = new ListenedMessage() { MessageId = messageId };
                listenedMessagesById.Add(messageId, listenedMessage);
            }

            listenedMessage.ActionsByListener[listener] = messageAction;
        }

        void CheckForListenedMessage(YamsterMessage message, YamsterModelEventCollector eventCollector)
        {
            ListenedMessage listenedMessage;
            if (!listenedMessagesById.TryGetValue(message.MessageId, out listenedMessage))
                return;
            listenedMessagesById.Remove(message.MessageId);

            foreach (var messageAction in listenedMessage.ActionsByListener.Values)
            {
                messageAction(message, eventCollector);
            }
        }

        class ListenedMessage
        {
            public long MessageId;
            public readonly Dictionary<object, ListenedMessageAction> ActionsByListener
                = new Dictionary<object, ListenedMessageAction>();
        }
        Dictionary<long, ListenedMessage> listenedMessagesById = new Dictionary<long, ListenedMessage>();

        #endregion
    }

    internal delegate void ListenedMessageAction(YamsterMessage message, YamsterModelEventCollector eventCollector);

}
