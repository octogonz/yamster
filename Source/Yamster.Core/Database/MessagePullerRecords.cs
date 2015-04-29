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
using System.Linq;
using Newtonsoft.Json;
using Yamster.Core.SQLite;

namespace Yamster.Core
{
    // Yamster's sync algorithm is driven by two SQL tables, SyncingFeeds and SyncingThreads.
    // 
    // SyncingFeeds is used by ProcessSpanAsync(), which is the outer loop, and uses
    // the REST endpoint "messages/in_group".  The DbSyncingFeed.Json property is a serialized
    // JsonSyncingFeed which contains a list of JsonMessagePullerSpan objects.
    //
    // The class comments for JsonSyncingFeed have more detail, but the main idea is that
    // DbSyncingFeed tracks our overall progress for syncing a single Yammer group
    // (or virtualized "feed").  It's okay for the same thread to appear in multiple feeds,
    // because the algorithm won't do any work for a thread that's already been synced.
    //
    // The challenge is that "messages/in_group" pages in reverse chronological order.
    // For example, suppose the Yammer history starts from Jan 1st, and today is Jan 28th,
    // and last time we ran Yamster on Jan 14th and synced backwards to the 10th.
    // When we start syncing today, we could pick up from the 10th, but the user is more
    // interested to see what's new, i.e. start syncing backwards from the 28th (today).
    // But suppose we sync back to the 26th, and then exit Yamster.  The next time we start
    // syncing, we now have two "spans" (10..14 and 26..28) and one "gap" (14..26).
    // In general we can accumulate an arbitrary number of spans/gaps depending on how 
    // often the sync gets interrupted (whereas if it can run uninterrupted, it will 
    // eventually close all the gaps).  JsonMessagePullerSpan represents these spans for
    // a given Yammer feed.
    public class DbSyncingFeed
    {
        [SQLiteMapperProperty(PrimaryKey = true)]
        public long FeedId;

        #region Properties Mirrored from Json

        [SQLiteMapperProperty]
        public DateTime LastUpdateUtc;

        [SQLiteMapperProperty]
        public DateTime LastCheckNewUtc;

        [SQLiteMapperProperty]
        public bool ReachedEmptyResult;

        [SQLiteMapperProperty]
        public bool HasSpanGaps;

        #endregion

        // This is a serialized JsonSyncingFeed
        [SQLiteMapperProperty(Nullable = OptionalBool.False)]
        public string Json;
    }

    // Yamster's sync algorithm is driven by two SQL tables, SyncingFeeds and SyncingThreads.
    // 
    // SyncingThreads used by ProcessGappedThreadAsync(), which is the inner loop,
    // and uses the REST endpoint "messages/in_thread".  Each DbSyncingThread record
    // represents a Yammer thread that is being worked on, and when the SyncingThreads table
    // is empty, it means we're done with the inner loop and can return to the outer loop.
    //
    // Like "messages/in_group", the "messages/in_thread" also has the problem of paging in
    // reverse chronological order.  But rather than tracking complicated gaps/spans, we 
    // simply sync the entire thread before moving on, since generally Yammer threads are
    // short.  A DbSyncingThread record tracks our progress for a given thread.  The
    // LastPulledMessageId walks backwards in time until it reaches StopMessageId, at
    // which point we are done, and the DbSyncingThread record is deleted, and we can
    // assume Yamster has a contiguous sequence of messages starting from the thread starter.
    [MappedTableIndex("FeedId", "ThreadId", Unique = true)]
    public class DbSyncingThread
    {
        // The Yammer message thread that we are downloading.  The thread ID is usually
        // the message ID of the threadstarter message.  However, there are cases
        // where they are different, e.g. if the original threadstarter message
        // was deleted (in which case the first reply becomes the new threadstarter
        // message).
        [SQLiteMapperProperty(PrimaryKey = true)]
        public long ThreadId;

        // Records the feed that introduced this record.  Currently this is only being
        // tracked for diagnostic purposes.
        [SQLiteMapperProperty]
        public long FeedId;

        // The end of the previous iteration, which is where we will pick up when fetching
        // the next batch of messages.
        [SQLiteMapperProperty]
        public long? LastPulledMessageId = null;  // null means start from newest

        // When we reach this message ID, the gap has been closed, and the thread again
        // has a contiguous set of messages, and the DbSyncingThread record can be deleted.
        [SQLiteMapperProperty]
        public long StopMessageId;

        [SQLiteMapperProperty]
        public int RetryCount;
    }

}
