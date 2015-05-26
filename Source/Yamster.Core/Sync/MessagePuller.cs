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
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Yamster.Core.SQLite;

namespace Yamster.Core
{
    public class MessagePuller
    {
        /// <summary>
        /// Occurs after the MessagePuller has updated the SQL database.
        /// </summary>
        public event EventHandler UpdatedDatabase;
        
        public event EventHandler EnabledChanged;

        /// <summary>
        /// Occurs before the MessagePuller makes a network service call.
        /// </summary>
        public event EventHandler<MessagePullerCallingServiceEventArgs> CallingService;

        public event EventHandler<MessagePullerErrorEventArgs> Error;

        AppContext appContext;
        YamsterArchiveDb yamsterArchiveDb;
        YamsterCoreDb yamsterCoreDb;
        YamsterApi yamsterApi;

        public MessagePullerAlgorithm Algorithm { get; set; }

        /// <summary>
        /// This setting conserves bandwidth and database size, by preventing the 
        /// MessagePuller from fetching very old messages.  The algorithm will not
        /// fetch Yammer threads that are more than this many days old, as measured from
        /// the current system date.  This limit is approximate:  The algorithm 
        /// does NOT delete messages from the database if they are older than the
        /// limit, and in fact it will download messages older than the limit if they
        /// belong to a thread with more recent messages.
        /// 
        /// To disable this feature and download everything, set HistoryLimitDays=0.
        /// </summary>
        [DefaultValue(90)]
        public int HistoryLimitDays { get; set; }

        /// <summary>
        /// Indicates the general time period where historical messages are being pulled from.
        /// </summary>
        public DateTime? HistoryProgress { get; private set; }

        /// <summary>
        /// Returns true if we have a complete history for all feeds that are being synced,
        /// and we have checked for new messages recently in all feeds, and there are no
        /// gapped threads.  A single-run sync operation should stop when UpToDate=true.
        /// </summary>
        public bool UpToDate { get; private set; }

        FreshenThreadRequest freshenThreadRequest = null;

        Task previousAsyncTask = null;
        bool alreadyProcessing = false;

        bool enabled = true;

        public MessagePuller(AppContext appContext)
        {
            this.appContext = appContext;

            yamsterArchiveDb = appContext.YamsterArchiveDb;
            yamsterCoreDb = appContext.YamsterCoreDb;
            yamsterApi = appContext.YamsterApi;

            HistoryLimitDays = 90;

            UpToDate = false;
        }

        /// <summary>
        /// If false, the MessagePuller is temporarily suspended (i.e. calling Process()
        /// has no effect).
        /// </summary>
        public bool Enabled
        {
            get { return this.enabled; }
            set
            {
                if (this.enabled == value)
                    return;
                this.enabled = value;
                if (EnabledChanged != null)
                    EnabledChanged(this, EventArgs.Empty);
            }

        }

        /// <summary>
        /// FreshenThread() asks the MessagePuller to refresh the specified thread as soon
        /// as possible, ignoring its normal algorithm priorities.
        /// </summary>
        /// <remarks>
        /// Since this operation may require multiple REST service calls and is subject to
        /// Yammer rate limits, it may still take a while to process.  The returned 
        /// FreshenThreadRequest object can be used to track the progress.  Only one 
        /// FreshenThread() request can be active at a time; any previous requests are
        /// canceled by a new call to FreshenThread().  Note that no processing will occur 
        /// unless MessagePuller.Enabled=true and MessagePuller.Process() is being called 
        /// at regular intervals.
        /// </remarks>
        public FreshenThreadRequest FreshenThread(YamsterThread thread)
        {
            FreshenThreadRequest interruptedRequest = freshenThreadRequest;
            freshenThreadRequest = new FreshenThreadRequest(thread, this);
            if (interruptedRequest != null)
            {
                interruptedRequest.SetError(new Exception("The operation was interrupted by a more recent request"));
            }
            return freshenThreadRequest;
        }

        /// <summary>
        /// Forgets all sync progress, and starts over with a clean slate.
        /// The existing database objects are not deleted.
        /// </summary>
        public void RequestFullResync()
        {
            this.yamsterCoreDb.SyncingFeeds.DeleteRecords("");
            this.yamsterCoreDb.SyncingThreads.DeleteRecords("");
        }

        /// <summary>
        /// Performs one step of the sync algorithm, which makes at most one service call
        /// to the Yammer REST endpoint.  This method is intended to be called at periodic intervals
        /// and may return without doing any work, e.g. if the state machine is waiting for a timeout.
        /// </summary>
        public void Process()
        {
            ForegroundSynchronizationContext.ProcessAsyncTasks();

            // Avoid reentrancy that could occur e.g. if the CallingService event handler calls
            // Utilities.ProcessApplicationEvents(), which causes another timer event to be processed.
            if (alreadyProcessing)
                return;
            try
            {
                alreadyProcessing = true;

                // Make sure the previous async task has completed
                if (previousAsyncTask != null)
                {
                    bool finished = previousAsyncTask.IsCanceled || previousAsyncTask.IsCompleted
                        || previousAsyncTask.IsFaulted;
                    if (!finished)
                        return;
                }

                previousAsyncTask = ProcessAsync();
            }
            finally
            {
                alreadyProcessing = false;
            }

            ForegroundSynchronizationContext.ProcessAsyncTasks();
        }

        async Task ProcessAsync()
        {
            this.appContext.RequireForegroundThread();

            if (!this.enabled)
                return;

            DateTime nowUtc = DateTime.UtcNow;

            // Don't exceed the Yammer throttling limit.  For a FreshenThread() request,
            // we increase the priority.
            if (!yamsterApi.IsSafeToRequest(increasedPriority: freshenThreadRequest != null))
                return;

            // Start by assuming we're not up to date, unless proven otherwise
            UpToDate = false;

            // 1. Is there a request to freshen a specific thread?
            if (freshenThreadRequest != null)
            {
                var freshenedThread = yamsterCoreDb.SyncingThreads
                    .Query("WHERE ThreadId = " + freshenThreadRequest.Thread.ThreadId)
                    .FirstOrDefault();

                if (freshenThreadRequest.State == FreshenThreadState.Queued)
                {
                    freshenThreadRequest.SetState(FreshenThreadState.Processing);

                    // Is there already an existing gap for this thread?
                    if (freshenedThread != null)
                    {
                        // Yes, simply reopen it
                        freshenedThread.LastPulledMessageId = null;
                    }
                    else
                    {
                        // No, so create a new one
                        freshenedThread = new DbSyncingThread();
                        freshenedThread.FeedId = freshenThreadRequest.Thread.GroupId;
                        freshenedThread.ThreadId = freshenThreadRequest.Thread.ThreadId;

                        // NOTE: The thread is presumed to be contiguous at this point.
                        int latestMessageInDb = yamsterArchiveDb.Mapper.QueryScalar<int>(
                            "SELECT MAX(Id) FROM " + this.yamsterArchiveDb.ArchiveMessages.TableName
                            + " WHERE ThreadId = " + freshenThreadRequest.Thread.ThreadId.ToString());
                        freshenedThread.StopMessageId = latestMessageInDb;
                        freshenedThread.LastPulledMessageId = null;

                        yamsterCoreDb.SyncingThreads.InsertRecord(freshenedThread);
                    }
                }

                if (freshenThreadRequest.State != FreshenThreadState.Processing)
                {
                    // This should be impossible
                    freshenThreadRequest.SetError(new Exception("State machine error"));
                    freshenThreadRequest = null;
                    return;
                }

                await ProcessGappedThreadAsync(freshenedThread);
                this.appContext.RequireForegroundThread();
                return;
            }

            // 2. Are there any syncing threads?  We must finish them before processing more spans
            var syncingThread = yamsterCoreDb.SyncingThreads
                .QueryAll()
                .FirstOrDefault();

            if (syncingThread != null)
            {
                // Start at the top of the list
                await ProcessGappedThreadAsync(syncingThread);
                this.appContext.RequireForegroundThread();
                return;
            }


            // Get the list of subscribed feeds
            List<DbGroupState> groupsToSync = yamsterCoreDb.GroupStates
                .Query("WHERE ShouldSync").ToList();
            bool forceCheckNew = false;
            List<JsonSyncingFeed> syncingFeeds = groupsToSync
                .Select(
                    groupState => {
                        var syncingFeed = yamsterCoreDb.GetJsonSyncingFeed(groupState.GroupId)
                            ?? new JsonSyncingFeed();
                        syncingFeed.GroupState = groupState;
                        syncingFeed.FeedId = groupState.GroupId;
                        return syncingFeed;
                    }
                ).ToList();

            if (yamsterCoreDb.Properties.SyncInbox)
            {
                // The Inbox is not a real group, so it doesn't have a DbGroupState record.
                var inboxSyncingFeed = yamsterCoreDb.GetJsonSyncingFeed(YamsterGroup.InboxFeedId)
                    ?? new JsonSyncingFeed();
                inboxSyncingFeed.GroupState = null;
                inboxSyncingFeed.FeedId = YamsterGroup.InboxFeedId;
                syncingFeeds.Insert(0, inboxSyncingFeed);
            }

            JsonSyncingFeed chosenSyncingFeed = null;

            // 3. Should we interrupt work on the history and instead check for new messages?
            if (Algorithm == MessagePullerAlgorithm.OptimizeReading)
            {
                TimeSpan longCheckNewDuration = TimeSpan.FromMinutes(7);

                chosenSyncingFeed = syncingFeeds
                    // Choose a feed that wasn't synced recently, but only if it has already
                    // done some work on its history
                    .Where(x => x.SpanCyclesSinceCheckNew >= 2
                        && (nowUtc - x.LastCheckNewUtc) > longCheckNewDuration)
                    // Pick the feed that was synced least recently
                    .OrderBy(x => x.LastCheckNewUtc)
                    .FirstOrDefault();

                if (chosenSyncingFeed != null)
                    forceCheckNew = true;
            }

            // 4. Are there any incomplete histories?  If so, choose the feed who 
            // made the least progress syncing so far
            if (chosenSyncingFeed == null)
            {
                // There are two kinds of feeds that need work:
                // 1. If it has gaps in the spans
                // 2. If we did not reach the beginning of the stream yet
                var nextHistoricalFeed = syncingFeeds
                    .Where(x => x.HasSpanGaps || !x.ReachedEmptyResult)
                    .OrderByDescending(x => x.GetNextOlderThanTime())
                    .FirstOrDefault();

                if (nextHistoricalFeed != null)
                {
                    var time = nextHistoricalFeed.GetNextOlderThanTime();
                    if (time != DateTime.MaxValue)  // don't show this degenerate value in the UI
                        HistoryProgress = time;

                    if (HistoryLimitDays > 0)
                    {
                        // If HistoryLimitDays is enabled, then don't pull threads that are
                        // older than the historyLimit
                        DateTime historyLimit = DateTime.Now.Date.Subtract(TimeSpan.FromDays(HistoryLimitDays));
                        if (nextHistoricalFeed.GetNextOlderThanTime() >= historyLimit)
                        {
                            chosenSyncingFeed = nextHistoricalFeed;
                        }
                    }
                    else
                    {
                        chosenSyncingFeed = nextHistoricalFeed;
                    }
                }
            }

            // 5. If all the histories are complete, then check for new messages at periodic intervals
            if (chosenSyncingFeed == null)
            {
                TimeSpan shortCheckNewDuration = TimeSpan.FromMinutes(3);

                chosenSyncingFeed = syncingFeeds
                    // Don't sync more often than shortCheckNewDuration
                    .Where(x => (nowUtc - x.LastCheckNewUtc) > shortCheckNewDuration)
                    // Pick the feed that was synced least recently
                    .OrderBy(x => x.LastCheckNewUtc)
                    .FirstOrDefault();

                if (chosenSyncingFeed != null)
                    forceCheckNew = true;
            }

            UpToDate = chosenSyncingFeed == null;
            if (!UpToDate)
            {
                await ProcessSpanAsync(chosenSyncingFeed, forceCheckNew);
                this.appContext.RequireForegroundThread();
            }
            else
            {
                Debug.WriteLine("Up to date.");
            }
        }

        async Task ProcessGappedThreadAsync(DbSyncingThread syncingThread)
        {
            this.appContext.RequireForegroundThread();
            Debug.WriteLine("MessagePuller: Fetching ThreaId={0} to close gap {1}..{2}",
                syncingThread.ThreadId, syncingThread.StopMessageId,
                syncingThread.LastPulledMessageId == null ? "newest" : syncingThread.LastPulledMessageId.ToString());

            if (CallingService != null)
                CallingService(this, new MessagePullerCallingServiceEventArgs(syncingThread.FeedId, syncingThread.ThreadId));
            
            DateTime queryUtc = DateTime.UtcNow;

            JsonMessageEnvelope envelope;
            try
            {
                // Perform a REST query like this:
                // https://www.yammer.com/example.com/api/v1/messages/in_thread/123.json?older_than=123
                envelope = await yamsterApi.GetMessagesInThreadAsync(syncingThread.ThreadId,
                    olderThan: syncingThread.LastPulledMessageId);
                this.appContext.RequireForegroundThread();
            }
            catch (RateLimitExceededException ex)
            {
                yamsterApi.NotifyRateLimitExceeded();
                OnError(ex);
                return;
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                {
                    using (var transaction = yamsterArchiveDb.BeginTransaction())
                    {
                        // A 404 error indicates that the thread does not exist, i.e. it was deleted
                        // from Yammer after we started syncing it.  We need to skip it, otherwise
                        // we'll get stuck in a loop retrying this request.
                        yamsterCoreDb.SyncingThreads.DeleteRecords("WHERE ThreadId = " + syncingThread.ThreadId);

                        // NOTE: We should also delete the partially synced messages from YamsterCoreDb,
                        // however this is problematic for YamsterCache, which currently isn't able 
                        // to flush items (or even to flush everything in a way that wouldn't
                        // break certain views).  That's only worth implementing if we wanted to
                        // support deletion in general (either for syncing Yammer deletions, or maybe
                        // for a user command to clean up the Yamster database), but these scenarios are
                        // not currently a priority.  Nobody has asked about it.
                        transaction.Commit();
                    }

                    // This is rare, so for now just report it to the user as an error.
                    OnError(new Exception("Failed to sync thread #" + syncingThread.ThreadId 
                        + " because it appears to have been deleted from Yammer.  (404 error)"));

                    return;
                }
                else
                {
                    // For all other exception types, keep retrying until successful
                    throw;
                }
            }

            using (var transaction = yamsterArchiveDb.BeginTransaction())
            {
                WriteMetaPropertiesToDb(envelope);

                WriteReferencesToDb(envelope.References, queryUtc);

                bool deleteSyncingThread = false;

                if (envelope.Messages.Length == 0)
                {
                    // Normally we expect v1/messages/in_thread to return at least one message.
                    // There are two cases where that is not true:
                    // 1. If someone deleted the messages from the thread *after*
                    //    v1/messages/in_group reported it to Yamster.  In this case, we 
                    //    can skip this thread (and ideally remove the deleted messages).
                    // 2. On rare occasions (maybe once in every 10,000 requests?) the
                    //    Yammer service can return this result for a real thread.
                    //    The raw JSON is indistinguishable from case #2, except that the 
                    //    messages reappear when the same request is retried.
                    //
                    // #2 is actually more common than #1, so we bother handling it

                    ++syncingThread.RetryCount;

                    if (syncingThread.RetryCount <= 3)
                    {
                        // Check for case #2
                        yamsterApi.BackOff();

                        OnError(new YamsterEmptyResultException(syncingThread.FeedId, syncingThread.ThreadId,
                            syncingThread.LastPulledMessageId, syncingThread.RetryCount));
                    }
                    else
                    {
                        // Assume case #1
                        this.yamsterCoreDb.SyncingThreads.DeleteRecordUsingPrimaryKey(syncingThread);

                        OnError(new YamsterEmptyResultException(syncingThread.FeedId, syncingThread.ThreadId,
                            syncingThread.LastPulledMessageId, -1));
                    }
                }
                else
                {
                    // Update the gapped thread
                    syncingThread.LastPulledMessageId = envelope.Messages.Min(x => x.Id);

                    foreach (var message in envelope.Messages)
                    {
                        WriteMessageToDb(message, queryUtc);
                    }

                    // Did we close the gap?  Normally this happens when we reach the message
                    // that we wanted to stop at.  However, there is an edge case where that
                    // message has been deleted, in which case we also need to stop if we
                    // reach the start of the thread (i.e. envelope.Messages comes back empty).
                    if (syncingThread.LastPulledMessageId <= syncingThread.StopMessageId)
                    {
                        // Yes, remove this gapped thread
                        deleteSyncingThread = true;

                        // Does this complete a FreshenThread() request?
                        if (freshenThreadRequest != null)
                        {
                            if (freshenThreadRequest.Thread.ThreadId == syncingThread.ThreadId)
                            {
                                freshenThreadRequest.SetState(FreshenThreadState.Completed);
                            }
                            else 
                            {
                                // This should only be possible if an event handler issues a new request
                                // while processing an existing one
                                freshenThreadRequest.SetError(new Exception("State machine processed wrong thread"));
                            }
                            freshenThreadRequest = null;
                        }
                    }
                }

                if (deleteSyncingThread)
                {
                    this.yamsterCoreDb.SyncingThreads.DeleteRecordUsingPrimaryKey(syncingThread);
                }
                else
                {
                    // Save the changes to syncingThread
                    this.yamsterCoreDb.SyncingThreads.InsertRecord(syncingThread, SQLiteConflictResolution.Replace);
                }

                transaction.Commit();

                if (UpdatedDatabase != null)
                    UpdatedDatabase(this, EventArgs.Empty);
            }
        }

        async Task ProcessSpanAsync(JsonSyncingFeed syncingFeed, bool forceCheckNew)
        {
            this.appContext.RequireForegroundThread();

            long feedId = syncingFeed.FeedId;
            DateTime queryUtc = DateTime.UtcNow;

            bool checkNew = forceCheckNew 
                || syncingFeed.Spans.Count < 1
                || (syncingFeed.Spans.Count == 1 && syncingFeed.ReachedEmptyResult);

            long? olderThan = null;

            if (checkNew)
            {
                syncingFeed.SpanCyclesSinceCheckNew = 0;
                syncingFeed.LastCheckNewUtc = queryUtc;
            }
            else
            {
                ++syncingFeed.SpanCyclesSinceCheckNew;

                // Work backwards from the most recent gap
                var lastSpan = syncingFeed.Spans.Last();
                olderThan = lastSpan.StartMessageId;
            }
            syncingFeed.LastUpdateUtc = queryUtc;

            if (CallingService != null)
                CallingService(this, new MessagePullerCallingServiceEventArgs(feedId, null));
            
            JsonMessageEnvelope envelope;
            try
            {
                // Perform a REST query like this:
                // https://www.yammer.com/example.com/api/v1/messages/in_group/3.json?threaded=extended&older_than=129
                envelope = await yamsterApi.GetMessagesInFeedAsync(feedId, olderThan);
                this.appContext.RequireForegroundThread();
            }
            catch (RateLimitExceededException ex)
            {
                yamsterApi.NotifyRateLimitExceeded();
                OnError(ex);
                return;
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound && syncingFeed.GroupState != null)
                    {
                        // The group does not exist; disable further syncing for it and report 
                        // a more specific error
                        DbGroupState groupState = syncingFeed.GroupState;
                        groupState.ShouldSync = false;
                        yamsterCoreDb.GroupStates.InsertRecord(groupState, SQLiteConflictResolution.Replace);
                        OnError(new YamsterFailedSyncException(feedId, ex));
                        return;
                    }
                }
                // A general error has occurred
                yamsterApi.BackOff();
                OnError(ex);
                return;
            }

            var newSpan = new JsonMessagePullerSpan();
            newSpan.StartMessageId = long.MaxValue;
            newSpan.StartTimeUtc = DateTime.MaxValue;
            newSpan.EndMessageId = long.MinValue;

            using (var transaction = yamsterArchiveDb.BeginTransaction())
            {
                WriteReferencesToDb(envelope.References, queryUtc);

                foreach (var threadStarter in envelope.Messages)
                {
                    // Clean up any corrupted data
                    if (yamsterCoreDb.SyncingThreads
                        .DeleteRecords("WHERE [ThreadId] = " + threadStarter.ThreadId) > 0)
                    {
                        Debug.WriteLine("MessagePuller: WARNING: Removed unexpected sync state for thread ID={0}",
                            threadStarter.ThreadId);
                    }

                    JsonMessage[] extendedMessages;

                    // Note that ThreadedExtended is indexed by thread ID, not message ID
                    if (!envelope.ThreadedExtended.TryGetValue(threadStarter.ThreadId, out extendedMessages))
                        extendedMessages = new JsonMessage[0];

                    // Update the span bounds
                    long latestMessageIdInThread;
                    DateTime latestMessageTimeInThread;
                    if (extendedMessages.Length > 0)
                    {
                        latestMessageIdInThread = extendedMessages.Max(x => x.Id);
                        latestMessageTimeInThread = extendedMessages.Max(x => x.Created);
                    }
                    else
                    {
                        latestMessageIdInThread = threadStarter.Id;
                        latestMessageTimeInThread = threadStarter.Created;
                    }
                    newSpan.StartMessageId = Math.Min(newSpan.StartMessageId, latestMessageIdInThread);
                    if (latestMessageTimeInThread < newSpan.StartTimeUtc)
                        newSpan.StartTimeUtc = latestMessageTimeInThread;
                    newSpan.EndMessageId = Math.Max(newSpan.EndMessageId, latestMessageIdInThread);

                    WriteMessageToDb(threadStarter, queryUtc);

                    // NOTE: The thread is presumed to be contiguous at this point.
                    // This is guaranteed to return at least threadStarter.Id written above
                    int latestMessageInDb = yamsterArchiveDb.Mapper.QueryScalar<int>(
                        "SELECT MAX(Id) FROM [" + this.yamsterArchiveDb.ArchiveMessages.TableName 
                        + "] WHERE ThreadId = " + threadStarter.ThreadId.ToString());

                    // There are two scenarios where we can prove that there is no gap,
                    // i.e. that we already have all the messages for the thread.
                    // NOTE:  Originally we assumed there was no gap if extendedMessages.Length<2,
                    // but a counterexample was found.
                    bool gapped = true;

// For debugging -- skip pulling most messages to accumulate threads faster
#if false
if ((threadStarter.ThreadId & 31) != 0)
{
    gapped = false;
}
#endif
                    // Scenario 1: Does the envelope contain the complete thread?
                    var threadReference = envelope.References
                        .OfType<ThreadReferenceJson>()
                        .Where(x => x.Id == threadStarter.ThreadId)
                        .FirstOrDefault();

                    if (threadReference != null)
                    {
                        // (+1 for threadStarter)
                        if (extendedMessages.Length + 1 == threadReference.Stats.MessagesCount)
                        {
// This criteria should work, but I found cases where Yammer's counter is incorrect
#if false
                            // The envelope contains the complete thread
                            gapped = false;
#endif
                        }
                    } 
                    else
                    {
                        // This should never happen, but if it does it's okay if we wrongly assume
                        // the thread is gapped
                        Debug.Assert(false);
                    }

                    // Scenario 2: Do the envelope messages overlap with the database's version of the thread?
                    if (gapped && extendedMessages.Length > 0)
                    {
                        long extendedStartId = extendedMessages.Min(x => x.Id);
                        if (latestMessageInDb >= extendedStartId)
                        {
                            // Yes, the messages overlap
                            gapped = false;
                        }
                    }

                    if (gapped)
                    {
                        var gappedThread = new DbSyncingThread();
                        gappedThread.FeedId = feedId;
                        gappedThread.ThreadId = threadStarter.ThreadId;
                        gappedThread.StopMessageId = latestMessageInDb;

                        // NOTE: In a static database, it would be most efficient to call
                        // WriteMessageToDb() for the extendedMessages that we already received
                        // and pick up with LastPulledMessageId=extendedStartId.
                        // However, if we assume people are actively posting in Yammer, it's 
                        // better to begin processing a gapped thread by querying for the absolute
                        // latest stuff, since a fair amount of time may have elapsed by the
                        // time we get around to doing the query.
                        gappedThread.LastPulledMessageId = null;

                        gappedThread.RetryCount = 0;
                        
                        // A key violation should be impossible here since if there was a conflicting
                        // record, we deleted it above.
                        yamsterCoreDb.SyncingThreads.InsertRecord(gappedThread);
                    }
                    else
                    {
                        foreach (var extendedMessage in extendedMessages)
                            WriteMessageToDb(extendedMessage, queryUtc);
                    }
                }

                if (envelope.Messages.Length > 0)
                {
                    if (olderThan.HasValue)
                    {
                        // If the Yammer result includes messages newer than what we asked
                        // for with olderThan, this is most likely a bug. 
                        // NOTE: Skip this check for the Inbox feed, which seems to have minor
                        // overlap about 50% of the time.  This issue wasn't observed in the Yammer web page,
                        // but that may be due to the additional filtering there for seen/unarchived.
                        if (feedId != YamsterGroup.InboxFeedId)
                        {
                            Debug.Assert(newSpan.EndMessageId < olderThan);
                        }

                        // If olderThan was specified, then the span actually covers anything
                        // up to that point in the history
                        newSpan.EndMessageId = olderThan.Value - 1;
                    }

                    // Now create a span corresponding to the range of messages we just received.
                    syncingFeed.AddSpan(newSpan);
                }
                else
                {
                    syncingFeed.ReachedEmptyResult = true;
                }

                yamsterCoreDb.UpdateJsonSyncingFeed(feedId, syncingFeed);

                if (feedId == YamsterGroup.InboxFeedId)
                {
                    // For each GroupId in the messages that we wrote, make sure ShowInYamster = 1
                    string showInYamsterSql = string.Format(
                        @"UPDATE [GroupStates] SET [ShowInYamster] = 1"
                            + " WHERE [GroupId] in ({0}) AND [ShowInYamster] <> 1",
                        string.Join(
                            ", ",
                            envelope.Messages.Where(x => x.GroupId != null).Select(x => x.GroupId).Distinct()
                        )
                    );
                    yamsterCoreDb.Mapper.ExecuteNonQuery(showInYamsterSql);

                    // For each ThreadId in the messages that we wrote, mark it as appearing in the inbox
                    string seenInInboxSql = string.Format(
                        @"UPDATE [ThreadStates] SET [SeenInInboxFeed] = 1"
                            + " WHERE [ThreadId] in ({0}) AND [SeenInInboxFeed] <> 1",
                        string.Join(
                            ", ",
                            envelope.Messages.Select(x => x.ThreadId).Distinct()
                        )
                    );
                    yamsterCoreDb.Mapper.ExecuteNonQuery(seenInInboxSql);
                }

                transaction.Commit();

                if (UpdatedDatabase != null)
                    UpdatedDatabase(this, EventArgs.Empty);
            }
        }

        internal void WriteManualEnvelopeToDb(JsonMessageEnvelope envelopeJson, long groupId, DateTime lastFetchedUtc)
        {
            WriteReferencesToDb(envelopeJson.References, lastFetchedUtc);
            foreach (var message in envelopeJson.Messages)
            {
                WriteMessageToDb(message, lastFetchedUtc);
            }
        }

        void WriteMessageToDb(JsonMessage message, DateTime lastFetchedUtc)
        {
#if DEBUG
            if (yamsterArchiveDb.Mapper.QueryScalar<int>("SELECT COUNT(*) FROM "
                + this.yamsterArchiveDb.ArchiveMessages.TableName + " WHERE Id = " + message.Id) > 0)
            {
                Debug.WriteLine("MessagePuller: Processing record " + message.Id + " UPDATE");
            }
            else
            {
                Debug.WriteLine("MessagePuller: Processing record " + message.Id + " ADD");
            }
#endif

            yamsterArchiveDb.InsertArchiveMessage(message, lastFetchedUtc);
        }

        void WriteReferencesToDb(IList<IReferenceJson> references, DateTime lastFetchedUtc)
        {
            foreach (var reference in references)
            {
                yamsterArchiveDb.InsertReference(reference, lastFetchedUtc);
            }
        }

        void WriteMetaPropertiesToDb(JsonMessageEnvelope envelope)
        {
            var originalProperties = yamsterCoreDb.Properties;

            if (envelope.Meta.CurrentUserId != originalProperties.CurrentUserId)
            {
                yamsterCoreDb.UpdateProperties(properties => {
                    properties.CurrentUserId = envelope.Meta.CurrentUserId;
                });
            }

            var anyMessage = envelope.Messages.FirstOrDefault();
            if (anyMessage != null)
            {
                if (anyMessage.NetworkId != originalProperties.CurrentNetworkId)
                {
                    yamsterCoreDb.UpdateProperties(properties => {
                        properties.CurrentNetworkId = anyMessage.NetworkId;
                    });
                }
            }
        }

        void OnError(Exception exception)
        {
            Debug.WriteLine("ERROR: " + exception.Message);

            if (freshenThreadRequest != null)
            {
                freshenThreadRequest.SetError(exception);
                freshenThreadRequest = null;
            }

            if (Error != null)
                Error(this, new MessagePullerErrorEventArgs(exception));
        }

    }
}
